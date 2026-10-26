
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using VisitorCheckIn.Data;
using VisitorCheckIn.Filters;
using VisitorCheckIn.Models;
using VisitorCheckIn.Services;
using VisitorCheckIn.ViewModels;

namespace VisitorCheckIn.Controllers;

public class KioskController : Controller
{
    private readonly AppDbContext _db;
    private readonly KioskService _kiosk;
    private readonly NotificationService _notify;
    private readonly IConfiguration _cfg;

    public KioskController(
        AppDbContext db,
        KioskService kiosk,
        NotificationService notify,
        IConfiguration cfg)
    {
        _db = db;
        _kiosk = kiosk;
        _notify = notify;
        _cfg = cfg;
    }

    private string UA => Request.Headers.UserAgent.ToString();

    // ============================================
    // KIOSK PAIRING
    // ============================================

    [HttpGet]
    public IActionResult Pair() => View();

    [HttpPost]
    [EnableRateLimiting("pair")]
    public async Task<IActionResult> Pair(string code)
    {
        code = code?.Trim() ?? "";

        if (!Regex.IsMatch(code, @"^\d{6}$"))
        {
            ModelState.AddModelError(
                "",
                "Enter a valid 6-digit pairing code.");

            return View();
        }

        var enrollment = await _kiosk.FindByCodeAsync(code);

        if (enrollment == null)
        {
            ModelState.AddModelError(
                "",
                "Invalid or expired code.");

            return View();
        }

        var raw = await _kiosk.RedeemAsync(
            enrollment,
            "Code",
            null,
            UA);

        _kiosk.IssueCookie(HttpContext, raw);

        return RedirectToAction(nameof(Index));
    }

    // ============================================
    // QR CODE / LINK ENROLLMENT
    // ============================================

    [HttpGet]
    [EnableRateLimiting("pair")]
    public async Task<IActionResult> Enroll(string token)
    {
        var enrollment = await _kiosk.FindByLinkAsync(token);

        if (enrollment == null)
        {
            ModelState.AddModelError(
                "",
                "This setup link is invalid, expired or already used.");

            return View("Pair");
        }

        return View(new EnrollVm
        {
            Token = token,
            DeviceName = enrollment.MaxUses > 1
                ? $"{enrollment.DeviceName} #{enrollment.UseCount + 1}"
                : enrollment.DeviceName
        });
    }

    [HttpPost]
    [EnableRateLimiting("pair")]
    public async Task<IActionResult> Enroll(EnrollVm vm)
    {
        if (vm == null)
            return BadRequest("Invalid enrollment request.");

        vm.DeviceName = vm.DeviceName?.Trim() ?? "";

        if (!ModelState.IsValid)
            return View(vm);

        var enrollment = await _kiosk.FindByLinkAsync(vm.Token);

        if (enrollment == null)
        {
            ModelState.AddModelError(
                "",
                "This setup link is invalid, expired or already used.");

            return View("Pair");
        }

        var raw = await _kiosk.RedeemAsync(
            enrollment,
            enrollment.MaxUses > 1 ? "Bulk" : "Link",
            vm.DeviceName,
            UA);

        _kiosk.IssueCookie(HttpContext, raw);

        return RedirectToAction(nameof(Index));
    }

    // ============================================
    // KIOSK HOME
    // ============================================

    [KioskAuth]
    public async Task<IActionResult> Index()
    {
        var device =
            (KioskDevice)HttpContext.Items["KioskDevice"]!;

        var questions = await _db.Questions
            .Where(q => q.IsActive)
            .OrderBy(q => q.SortOrder)
            .ToListAsync();

        var hosts = await _db.Hosts
            .Where(h => h.IsActive)
            .OrderBy(h => h.Name)
            .ToListAsync();

        return View(new KioskVm
        {
            CompanyName = _cfg["App:CompanyName"] ?? "",
            WelcomeText = _cfg["App:WelcomeText"] ?? "",
            DeviceName = device.Name,

            RequirePhoto =
                _cfg.GetValue("Kiosk:RequirePhoto", true),

            VisitorTypes = VisitorTypes.All,

            Hosts = hosts.Select(h => new KioskHost
            {
                Id = h.Id,
                Name = h.Name
            }).ToList(),

            Questions = questions.Select(q => new KioskQuestion
            {
                Id = q.Id,
                Text = q.Text,
                Type = q.Type.ToString(),
                IsRequired = q.IsRequired,
                VisitorType = q.VisitorType,

                Options = (q.OptionsText ?? "")
                    .Split(
                        '\n',
                        StringSplitOptions.RemoveEmptyEntries |
                        StringSplitOptions.TrimEntries)
            }).ToList()
        });
    }

    [KioskAuth]
    public IActionResult Ping() => Ok();

    // ============================================
    // VISITOR CHECK-IN
    // ============================================

    [KioskAuth]
    [HttpPost]
    public async Task<IActionResult> CheckIn(
        [FromBody] CheckInDto dto)
    {
        if (dto == null)
            return BadRequest("Invalid check-in request.");

        // ----------------------------------------
        // VALIDATE VISITOR NAME
        // ----------------------------------------

        var name = dto.Name?.Trim() ?? "";

        if (name.Length < 2 || name.Length > 120)
        {
            return BadRequest(
                "Please enter your full name (2–120 characters).");
        }

        if (!IsValidPersonName(name))
        {
            return BadRequest(
                "Enter a valid name using letters, not just numbers or symbols.");
        }

        // ----------------------------------------
        // VALIDATE COMPANY
        // ----------------------------------------

        var company = dto.Company?.Trim() ?? "";

        if (company.Length > 150)
        {
            return BadRequest(
                "Company name cannot exceed 150 characters.");
        }

        if (company.Any(char.IsControl))
        {
            return BadRequest(
                "Company name contains invalid characters.");
        }

        // ----------------------------------------
        // VALIDATE VISITOR TYPE
        // ----------------------------------------

        var visitorType = dto.VisitorType?.Trim() ?? "";

        if (!VisitorTypes.All.Contains(visitorType))
        {
            return BadRequest(
                "Please select a valid visitor type.");
        }

        // ----------------------------------------
        // VALIDATE HOST
        // ----------------------------------------

        var host = await _db.Hosts.FirstOrDefaultAsync(
            h => h.Id == dto.HostId && h.IsActive);

        if (host == null)
        {
            return BadRequest(
                "Please select an available host.");
        }

        // ----------------------------------------
        // LOAD RELEVANT QUESTIONS
        // ----------------------------------------

        var questions = await _db.Questions
            .Where(q =>
                q.IsActive &&
                (q.VisitorType == "All" ||
                 q.VisitorType == visitorType))
            .ToListAsync();

        if ((dto.Answers?.Count ?? 0) > 100)
        {
            return BadRequest(
                "Too many question answers were submitted.");
        }

        var given = (dto.Answers ?? new())
            .GroupBy(a => a.QuestionId)
            .ToDictionary(
                g => g.Key,
                g => g.Last().Value?.Trim() ?? "");

        var allowedQuestionIds = questions
            .Select(q => q.Id)
            .ToHashSet();

        // Reject answers for questions the kiosk did
        // not display for this visitor type.
        if (given.Keys.Any(id => !allowedQuestionIds.Contains(id)))
        {
            return BadRequest(
                "Invalid check-in question. Please refresh and try again.");
        }

        // ----------------------------------------
        // VALIDATE REQUIRED QUESTIONS
        // ----------------------------------------

        foreach (var question in questions)
        {
            given.TryGetValue(
                question.Id,
                out var answer);

            answer ??= "";

            if (question.IsRequired &&
                string.IsNullOrWhiteSpace(answer))
            {
                return BadRequest(
                    $"Please answer: {question.Text}");
            }

            if (string.IsNullOrWhiteSpace(answer))
                continue;

            // Limit stored answer sizes.

            // ----------------------------------------
            // VALIDATE SIGNATURE AND TEXT ANSWERS
            // ----------------------------------------

            if (question.Type.ToString() == "Signature")
            {
                const string prefix = "data:image/png;base64,";

                if (!answer.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(
                        "Please provide a valid signature.");
                }

                // Allow up to 200,000 characters for a signature.
                if (answer.Length > 200_000)
                {
                    return BadRequest(
                        "Signature is too large. Please clear it and sign again.");
                }

                try
                {
                    var signatureBytes =
                        Convert.FromBase64String(answer[prefix.Length..]);

                    // Check the PNG file signature.
                    byte[] pngHeader =
                    {
            137, 80, 78, 71, 13, 10, 26, 10
        };

                    if (signatureBytes.Length < 8 ||
                        !signatureBytes.Take(8).SequenceEqual(pngHeader))
                    {
                        return BadRequest(
                            "Invalid signature image. Please sign again.");
                    }
                }
                catch (FormatException)
                {
                    return BadRequest(
                        "Signature could not be read. Please sign again.");
                }
            }
            else
            {
                // Ordinary text answers remain limited to 2,000 characters.
                if (answer.Length > 2000)
                {
                    return BadRequest(
                        $"Your answer to '{question.Text}' is too long.");
                }
            }


            if (answer.Any(char.IsControl) &&
                answer.Any(c =>
                    char.IsControl(c) &&
                    c != '\r' &&
                    c != '\n' &&
                    c != '\t'))
            {
                return BadRequest(
                    $"Your answer to '{question.Text}' contains invalid characters.");
            }


            // ============================================
            // VALIDATE EMAIL AND PHONE QUESTIONS
            // ============================================

            // Recognise the actual question type
            // as well as the question's wording.

            bool isEmail =
                question.Type.ToString() == "Email" ||
                IsEmailQuestion(question.Text);

            bool isPhone =
                question.Type.ToString() == "Phone" ||
                IsPhoneQuestion(question.Text);

            // Validate email
            if (isEmail)
            {
                if (answer.Length > 254 ||
                    !new EmailAddressAttribute().IsValid(answer))
                {
                    return BadRequest(
                        "Please enter a valid email address.");
                }
            }


            // Validate Australian phone number
            if (isPhone)
            {
                if (!IsValidPhoneNumber(answer))
                {
                    return BadRequest(
                        "Please enter a valid Australian mobile or landline number.");
                }
            }

        } // End of foreach (var question in questions)


        // ============================================
        // DUPLICATE VISITOR CHECK-IN DETECTION
        // ============================================

        // Look for an existing visitor who has not
        // checked out yet and has the same name.

        if (!dto.ConfirmDuplicate)
        {
            var existingVisit = await _db.Visits
                .AsNoTracking()
                .AnyAsync(v =>
                    v.CheckOutUtc == null &&
                    v.VisitorName.ToLower() == name.ToLower());

            if (existingVisit)
            {
                return Conflict(new
                {
                    duplicate = true,
                    message =
                        "A visitor with this name is already checked in. " +
                        "If this is you, please check out first. " +
                        "Otherwise, confirm that you are a different person."
                });
            }
        }


        // ----------------------------------------
        // VALIDATE PHOTO
        // ----------------------------------------

        byte[]? photoBytes = null;

        if (!string.IsNullOrEmpty(dto.Photo))
        {
            if (!TryParsePhoto(dto.Photo, out photoBytes))
            {
                return BadRequest(
                    "That photo could not be read. Please retake it.");
            }
        }
        else if (_cfg.GetValue("Kiosk:RequirePhoto", true))
        {
            return BadRequest(
                "Please take your photo.");
        }

        // ----------------------------------------
        // SAVE VISITOR
        // ----------------------------------------

        var device =
            (KioskDevice)HttpContext.Items["KioskDevice"]!;

        var visit = new Visit
        {
            VisitorName = name,
            VisitorCompany = company,
            VisitorType = visitorType,
            HostId = host.Id,
            CheckInUtc = DateTime.UtcNow,
            KioskDeviceId = device.Id,

            Answers = questions
                .Where(q =>
                    given.TryGetValue(q.Id, out var answer) &&
                    !string.IsNullOrWhiteSpace(answer))
                .Select(q => new VisitAnswer
                {
                    QuestionId = q.Id,
                    QuestionText = q.Text,
                    Answer = given[q.Id]
                })
                .ToList()
        };

        _db.Visits.Add(visit);

        // ----------------------------------------
        // SAVE PHOTO
        // ----------------------------------------

        if (photoBytes != null)
        {
            visit.PhotoToken = Convert.ToHexString(
                RandomNumberGenerator.GetBytes(24))
                .ToLowerInvariant();

            _db.VisitPhotos.Add(new VisitPhoto
            {
                Visit = visit,
                Token = visit.PhotoToken,
                Data = photoBytes,
                ContentType = "image/jpeg",
                CreatedUtc = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        // ----------------------------------------
        // PREPARE HOST NOTIFICATION
        // ----------------------------------------

        NotifyPhoto? photo = null;

        if (visit.PhotoToken != null)
        {
            var baseUrl = _cfg["App:PublicBaseUrl"];

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl =
                    $"{Request.Scheme}://{Request.Host}";
            }

            var path = "/p/" + visit.PhotoToken;

            photo = new NotifyPhoto(
                path,
                baseUrl.TrimEnd('/') + path,
                photoBytes!);
        }

        var who = string.IsNullOrWhiteSpace(
            visit.VisitorCompany)
                ? visit.VisitorName
                : $"{visit.VisitorName} ({visit.VisitorCompany})";

        // ----------------------------------------
        // NOTIFY HOST
        // ----------------------------------------

        try
        {
            await _notify.NotifyAsync(
                host.Id,
                "Visitor arrived",
                $"{who} is here to see you.",
                "/HostPortal",
                photo);
        }
        catch (Exception ex)
        {
            HttpContext.RequestServices
                .GetRequiredService<ILogger<KioskController>>()
                .LogWarning(
                    ex,
                    "Notification failed for visit {Id}",
                    visit.Id);
        }

        return Json(new
        {
            ok = true,
            host = host.Name
        });
    }

    // ============================================
    // VALIDATION HELPERS
    // ============================================

    private static bool IsValidPersonName(string name)
    {
        // Supports international letters, spaces,
        // hyphens, apostrophes and periods.
        return Regex.IsMatch(
            name,
            @"^[\p{L}\p{M}]+(?:[ '\u2019.-][\p{L}\p{M}]+)*$");
    }

    private static bool IsEmailQuestion(string text)
    {
        var value = text?.Trim() ?? "";

        return Regex.IsMatch(
            value,
            @"\b(e-?mail)\b",
            RegexOptions.IgnoreCase);
    }

    private static bool IsPhoneQuestion(string text)
    {
        var value = text?.Trim() ?? "";

        // Keep this restricted to contact-number
        // questions, not questions about phone devices.
        return Regex.IsMatch(
            value,
            @"^(phone number|mobile number|contact number|telephone number)\s*\??$",
            RegexOptions.IgnoreCase);
    }

    private static bool IsValidPhoneNumber(string phone)
    {
        var value = Regex.Replace(
            phone,
            @"[\s()-]",
            "");

        // Australian mobile or geographic landline.
        return Regex.IsMatch(
            value,
            @"^(?:04\d{8}|\+614\d{8}|0[2378]\d{8}|\+61[2378]\d{8})$");
    }

    // ============================================
    // PHOTO VALIDATION
    // ============================================

    private static bool TryParsePhoto(
        string dataUrl,
        out byte[]? bytes)
    {
        bytes = null;

        const string prefix = "data:image/jpeg;base64,";

        if (dataUrl == null ||
            dataUrl.Length > 2_800_000 ||
            !dataUrl.StartsWith(
                prefix,
                StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(
                dataUrl.Substring(prefix.Length));
        }
        catch (FormatException)
        {
            return false;
        }

        return bytes.Length > 100 &&
               bytes.Length <= 2_000_000 &&
               bytes[0] == 0xFF &&
               bytes[1] == 0xD8 &&
               bytes[2] == 0xFF;
    }

    // ============================================
    // VISITOR CHECK-OUT SEARCH
    // ============================================

    [KioskAuth]
    public async Task<IActionResult> OnSite(string q)
    {
        if (string.IsNullOrWhiteSpace(q) ||
            q.Trim().Length < 2)
        {
            return Json(Array.Empty<object>());
        }

        q = q.Trim();

        var since = DateTime.UtcNow.AddHours(-24);

        var rows = await _db.Visits
            .Include(v => v.Host)
            .Where(v =>
                v.CheckOutUtc == null &&
                v.CheckInUtc > since &&
                v.VisitorName.Contains(q))
            .OrderBy(v => v.VisitorName)
            .Take(8)
            .Select(v => new
            {
                v.Id,
                Name = v.VisitorName,
                Company = v.VisitorCompany,
                Host = v.Host.Name
            })
            .ToListAsync();

        return Json(rows);
    }

    // ============================================
    // VISITOR CHECK-OUT
    // ============================================

    [KioskAuth]
    [HttpPost]
    public async Task<IActionResult> CheckOut(
        [FromBody] IdDto dto)
    {
        if (dto == null || dto.Id <= 0)
        {
            return BadRequest(
                "Invalid visitor selection.");
        }

        var visit = await _db.Visits.FindAsync(dto.Id);

        if (visit == null || visit.CheckOutUtc != null)
        {
            return BadRequest("Not found.");
        }

        visit.CheckOutUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Json(new { ok = true });
    }
}
