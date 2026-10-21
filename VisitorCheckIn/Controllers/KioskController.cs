using System.Security.Cryptography;
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
    public KioskController(AppDbContext db, KioskService kiosk, NotificationService notify, IConfiguration cfg)
    { _db = db; _kiosk = kiosk; _notify = notify; _cfg = cfg; }

    private string UA => Request.Headers.UserAgent.ToString();

    // ---------- Enrolment (anonymous, rate-limited) ----------

    /// <summary>Method A: type the 6-digit code.</summary>
    [HttpGet] public IActionResult Pair() => View();

    [HttpPost, EnableRateLimiting("pair")]
    public async Task<IActionResult> Pair(string code)
    {
        var e = await _kiosk.FindByCodeAsync(code?.Trim());
        if (e == null) { ModelState.AddModelError("", "Invalid or expired code."); return View(); }
        var raw = await _kiosk.RedeemAsync(e, "Code", null, UA);
        _kiosk.IssueCookie(HttpContext, raw);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Methods B/C/E: QR code, link, or bulk link. GET only shows a confirm page, so link
    /// previews (WhatsApp, Slack, iMessage) cannot burn a one-time link.</summary>
    [HttpGet, EnableRateLimiting("pair")]
    public async Task<IActionResult> Enroll(string token)
    {
        var e = await _kiosk.FindByLinkAsync(token);
        if (e == null) { ModelState.AddModelError("", "This setup link is invalid, expired or already used."); return View("Pair"); }
        return View(new EnrollVm { Token = token, DeviceName = e.MaxUses > 1 ? $"{e.DeviceName} #{e.UseCount + 1}" : e.DeviceName });
    }

    [HttpPost, EnableRateLimiting("pair")]
    public async Task<IActionResult> Enroll(EnrollVm vm)
    {
        var e = await _kiosk.FindByLinkAsync(vm.Token);
        if (e == null) { ModelState.AddModelError("", "This setup link is invalid, expired or already used."); return View("Pair"); }
        var raw = await _kiosk.RedeemAsync(e, e.MaxUses > 1 ? "Bulk" : "Link", vm.DeviceName, UA);
        _kiosk.IssueCookie(HttpContext, raw);
        return RedirectToAction(nameof(Index));
    }

    // ---------- Kiosk (paired devices only) ----------

    [KioskAuth]
    public async Task<IActionResult> Index()
    {
        var device = (KioskDevice)HttpContext.Items["KioskDevice"];
        var qs = await _db.Questions.Where(q => q.IsActive).OrderBy(q => q.SortOrder).ToListAsync();
        var hosts = await _db.Hosts.Where(h => h.IsActive).OrderBy(h => h.Name).ToListAsync();
        return View(new KioskVm
        {
            CompanyName = _cfg["App:CompanyName"], WelcomeText = _cfg["App:WelcomeText"], DeviceName = device.Name,
            RequirePhoto = _cfg.GetValue("Kiosk:RequirePhoto", true),
            VisitorTypes = VisitorTypes.All,
            Hosts = hosts.Select(h => new KioskHost { Id = h.Id, Name = h.Name }).ToList(),
            Questions = qs.Select(q => new KioskQuestion
            {
                Id = q.Id, Text = q.Text, Type = q.Type.ToString(), IsRequired = q.IsRequired, VisitorType = q.VisitorType,
                Options = (q.OptionsText ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            }).ToList()
        });
    }

    [KioskAuth] public IActionResult Ping() => Ok();

    [KioskAuth, HttpPost]
    public async Task<IActionResult> CheckIn([FromBody] CheckInDto dto)
    {
        var device = (KioskDevice)HttpContext.Items["KioskDevice"];
        if (dto == null || string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 100) return BadRequest("Please enter your name.");
        var host = await _db.Hosts.FirstOrDefaultAsync(h => h.Id == dto.HostId && h.IsActive);
        if (host == null) return BadRequest("Please choose who you are visiting.");

        var type = VisitorTypes.All.Contains(dto.VisitorType) ? dto.VisitorType : "Visitor";
        var questions = await _db.Questions
            .Where(q => q.IsActive && (q.VisitorType == "All" || q.VisitorType == type)).ToListAsync();
        var given = (dto.Answers ?? new()).GroupBy(a => a.QuestionId).ToDictionary(g => g.Key, g => g.Last().Value ?? "");

        foreach (var q in questions.Where(q => q.IsRequired))
            if (!given.TryGetValue(q.Id, out var v) || string.IsNullOrWhiteSpace(v))
                return BadRequest($"Please answer: {q.Text}");

        // Photo: required unless Kiosk:RequirePhoto is set to false in appsettings.json
        byte[] photoBytes = null;
        if (!string.IsNullOrEmpty(dto.Photo))
        {
            if (!TryParsePhoto(dto.Photo, out photoBytes)) return BadRequest("That photo could not be read. Please retake it.");
        }
        else if (_cfg.GetValue("Kiosk:RequirePhoto", true))
            return BadRequest("Please take your photo.");

        var visit = new Visit
        {
            VisitorName = dto.Name.Trim(), VisitorCompany = dto.Company?.Trim(), VisitorType = type,
            HostId = host.Id, CheckInUtc = DateTime.UtcNow, KioskDeviceId = device.Id,
            Answers = questions.Where(q => given.ContainsKey(q.Id) && !string.IsNullOrWhiteSpace(given[q.Id]))
                .Select(q => new VisitAnswer
                {
                    QuestionId = q.Id, QuestionText = q.Text,
                    Answer = given[q.Id].Length > 200_000 ? given[q.Id][..200_000] : given[q.Id]
                }).ToList()
        };
        _db.Visits.Add(visit);
        if (photoBytes != null)
        {
            visit.PhotoToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            _db.VisitPhotos.Add(new VisitPhoto { Visit = visit, Token = visit.PhotoToken, Data = photoBytes, ContentType = "image/jpeg", CreatedUtc = DateTime.UtcNow });
        }
        await _db.SaveChangesAsync();

        NotifyPhoto photo = null;
        if (visit.PhotoToken != null)
        {
            var baseUrl = _cfg["App:PublicBaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = $"{Request.Scheme}://{Request.Host}";
            var path = "/p/" + visit.PhotoToken;
            photo = new NotifyPhoto(path, baseUrl.TrimEnd('/') + path, photoBytes);
        }

        var who = string.IsNullOrWhiteSpace(visit.VisitorCompany) ? visit.VisitorName : $"{visit.VisitorName} ({visit.VisitorCompany})";
        try { await _notify.NotifyAsync(host.Id, "Visitor arrived", $"{who} is here to see you.", "/HostPortal", photo); }
        catch (Exception ex) { HttpContext.RequestServices.GetRequiredService<ILogger<KioskController>>().LogWarning(ex, "Notification failed for visit {Id}", visit.Id); }
        return Json(new { ok = true, host = host.Name });
    }

    /// <summary>Accepts only a real JPEG sent as a data URL, up to about 2 MB.</summary>
    private static bool TryParsePhoto(string dataUrl, out byte[] bytes)
    {
        bytes = null;
        const string prefix = "data:image/jpeg;base64,";
        if (dataUrl == null || dataUrl.Length > 2_800_000 || !dataUrl.StartsWith(prefix, StringComparison.Ordinal)) return false;
        try { bytes = Convert.FromBase64String(dataUrl.Substring(prefix.Length)); }
        catch (FormatException) { return false; }
        return bytes.Length > 100 && bytes.Length <= 2_000_000 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
    }

    /// <summary>Check-out search. Needs 2+ characters so the screen never lists everyone.</summary>
    [KioskAuth]
    public async Task<IActionResult> OnSite(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Json(Array.Empty<object>());
        q = q.Trim();
        var since = DateTime.UtcNow.AddHours(-24);
        var rows = await _db.Visits.Include(v => v.Host)
            .Where(v => v.CheckOutUtc == null && v.CheckInUtc > since && v.VisitorName.Contains(q))
            .OrderBy(v => v.VisitorName).Take(8)
            .Select(v => new { v.Id, Name = v.VisitorName, Company = v.VisitorCompany, Host = v.Host.Name }).ToListAsync();
        return Json(rows);
    }

    [KioskAuth, HttpPost]
    public async Task<IActionResult> CheckOut([FromBody] IdDto dto)
    {
        var v = await _db.Visits.FindAsync(dto.Id);
        if (v == null || v.CheckOutUtc != null) return BadRequest("Not found.");
        v.CheckOutUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Json(new { ok = true });
    }
}
