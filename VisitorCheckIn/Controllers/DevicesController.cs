
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using VisitorCheckIn.Data;
using VisitorCheckIn.Services;
using VisitorCheckIn.ViewModels;

namespace VisitorCheckIn.Controllers;


[Authorize(Roles = "Admin")]
[AutoValidateAntiforgeryToken]
public class DevicesController : Controller

{
    private readonly AppDbContext _db;
    private readonly KioskService _kiosk;
    private readonly IConfiguration _cfg;

    public DevicesController(
        AppDbContext db,
        KioskService kiosk,
        IConfiguration cfg)
    {
        _db = db;
        _kiosk = kiosk;
        _cfg = cfg;
    }

    // ============================================
    // VALIDATION HELPERS
    // ============================================

    private static string CleanName(string? name)
    {
        return name?.Trim() ?? "";
    }

    private static string? DeviceNameError(string? name)
    {
        name = CleanName(name);

        if (name.Length < 2 || name.Length > 100)
        {
            return "Device name must be between 2 and 100 characters.";
        }

        if (name.Any(char.IsControl))
        {
            return "Device name contains invalid characters.";
        }

        if (!name.Any(char.IsLetterOrDigit))
        {
            return "Device name must contain letters or numbers.";
        }

        return null;
    }

    private static bool ValidPairCode(string? code)
    {
        return Regex.IsMatch(
            code?.Trim() ?? "",
            @"^[0-9]{6}$");
    }

    private async Task<bool> DeviceNameExistsAsync(
        string name,
        int? excludeId = null)
    {
        return await _db.KioskDevices.AnyAsync(
            d =>
                !d.IsRevoked &&
                (!excludeId.HasValue || d.Id != excludeId.Value) &&
                d.Name.ToLower() == name.ToLower());
    }

    // ============================================
    // DEVICE LIST
    // ============================================

    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow;

        return View(new DevicesIndexVm
        {
            Devices = await _db.KioskDevices
                .OrderByDescending(d => d.PairedUtc)
                .ToListAsync(),

            Pending = await _db.EnrollmentTokens
                .Where(e =>
                    e.ExpiresUtc > now &&
                    e.UseCount < e.MaxUses)
                .OrderByDescending(e => e.CreatedUtc)
                .ToListAsync()
        });
    }

    // ============================================
    // ADD DEVICE
    // ============================================

    [HttpGet]
    public IActionResult Add()
    {
        return View();
    }

    // ============================================
    // PAIR DEVICE PAGE
    // ============================================

    [HttpGet]
    public IActionResult Pair()
    {
        return View();
    }

    // ============================================
    // PAIR WITH SIX-DIGIT CODE
    // ============================================

    [HttpPost]
    public async Task<IActionResult> Pair(string code)
    {
        code = code?.Trim() ?? "";

        if (!ValidPairCode(code))
        {
            TempData["Error"] =
                "Please enter a valid 6-digit pairing code.";

            return RedirectToAction(nameof(Pair));
        }

        var enrollment =
            await _kiosk.FindByCodeAsync(code);

        if (enrollment == null)
        {
            TempData["Error"] =
                "Invalid or expired code. Generate a new code in Kiosk Devices.";

            return RedirectToAction(nameof(Pair));
        }

        var raw = await _kiosk.RedeemAsync(
            enrollment,
            "Code",
            null,
            Request.Headers.UserAgent.ToString());

        _kiosk.IssueCookie(HttpContext, raw);

        TempData["Ok"] =
            $"This browser is now paired as '{enrollment.DeviceName}'.";

        return RedirectToAction(nameof(Pair));
    }

    // ============================================
    // PAIR USING ENROLLMENT LINK
    // ============================================

    [HttpPost]
    public async Task<IActionResult> PairLink(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["Error"] = "Invalid setup link.";

            return RedirectToAction(nameof(Pair));
        }

        var enrollment =
            await _kiosk.FindByLinkAsync(token);

        if (enrollment == null)
        {
            TempData["Error"] =
                "This setup link is invalid, expired or already used.";

            return RedirectToAction(nameof(Pair));
        }

        var deviceName = enrollment.MaxUses > 1
            ? $"{enrollment.DeviceName} #{enrollment.UseCount + 1}"
            : enrollment.DeviceName;

        if (DeviceNameError(deviceName) != null)
        {
            TempData["Error"] =
                "Generated device name is invalid. Please create a new enrollment.";

            return RedirectToAction(nameof(Pair));
        }

        var raw = await _kiosk.RedeemAsync(
            enrollment,
            enrollment.MaxUses > 1 ? "Bulk" : "Link",
            deviceName,
            Request.Headers.UserAgent.ToString());

        _kiosk.IssueCookie(HttpContext, raw);

        TempData["Ok"] =
            $"This browser is now paired as '{deviceName}'.";

        return RedirectToAction(nameof(Pair));
    }

    // ============================================
    // PAIR CODE (AJAX)
    // ============================================

    [HttpPost]
    public async Task<IActionResult> PairCode(string code)
    {
        code = code?.Trim() ?? "";

        if (!ValidPairCode(code))
        {
            return Json(new
            {
                ok = false,
                message = "Enter a valid 6-digit pairing code."
            });
        }

        var enrollment =
            await _kiosk.FindByCodeAsync(code);

        if (enrollment == null)
        {
            return Json(new
            {
                ok = false,
                message = "Invalid or expired pairing code."
            });
        }

        var raw = await _kiosk.RedeemAsync(
            enrollment,
            "Code",
            null,
            Request.Headers.UserAgent.ToString());

        _kiosk.IssueCookie(HttpContext, raw);

        return Json(new
        {
            ok = true,
            message =
                $"Paired as '{enrollment.DeviceName}'. This browser is now a kiosk."
        });
    }

    // ============================================
    // DEVICE SETUP GUIDE
    // ============================================

    [HttpGet]
    public IActionResult Guide()
    {
        return View();
    }

    // ============================================
    // CREATE ENROLLMENT
    // ============================================

    [HttpPost]
    public async Task<IActionResult> Enroll(
        string deviceName,
        int maxUses = 1,
        int minutes = 15)
    {
        deviceName = CleanName(deviceName);

        // Validate device name.
        var nameError = DeviceNameError(deviceName);

        if (nameError != null)
        {
            TempData["Error"] = nameError;
            return RedirectToAction(nameof(Add));
        }

        // Prevent creating a single-device enrollment
        // with an already active device name.
        if (maxUses == 1 &&
            await DeviceNameExistsAsync(deviceName))
        {
            TempData["Error"] =
                "An active kiosk already uses this device name.";

            return RedirectToAction(nameof(Add));
        }

        // Validate maximum number of devices.
        if (maxUses < 1 || maxUses > 500)
        {
            TempData["Error"] =
                "Maximum uses must be between 1 and 500.";

            return RedirectToAction(nameof(Add));
        }

        // Validate enrollment expiry.
        // Minimum: 5 minutes
        // Maximum: 14 days
        if (minutes < 5 || minutes > 20160)
        {
            TempData["Error"] =
                "Enrollment expiry must be between 5 minutes and 14 days.";

            return RedirectToAction(nameof(Add));
        }

        // Reserve enough name length for suffixes
        // generated during bulk enrollment.
        if (maxUses > 1)
        {
            var longestName =
                $"{deviceName} #{maxUses}";

            if (DeviceNameError(longestName) != null)
            {
                TempData["Error"] =
                    "Device name is too long for bulk enrollment. Use a shorter name.";

                return RedirectToAction(nameof(Add));
            }
        }

        var (enrollment, linkToken) =
            await _kiosk.CreateEnrollmentAsync(
                deviceName,
                maxUses,
                TimeSpan.FromMinutes(minutes));

        // ========================================
        // GENERATE PUBLIC PAIRING URL
        // ========================================

        var baseUrl = _cfg["App:PublicBaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl =
                $"{Request.Scheme}://{Request.Host}";
        }

        baseUrl = baseUrl.TrimEnd('/');

        var link =
            $"{baseUrl}/Kiosk/Enroll?token={Uri.EscapeDataString(linkToken)}";

        // ========================================
        // GENERATE QR CODE
        // ========================================

        using var generator = new QRCodeGenerator();

        using var qrData = generator.CreateQrCode(
            link,
            QRCodeGenerator.ECCLevel.M);

        var png = new PngByteQRCode(qrData)
            .GetGraphic(10);

        // ========================================
        // SHOW ENROLLMENT RESULT
        // ========================================

        return View("Enrollment", new EnrollmentResultVm
        {
            DeviceName = enrollment.DeviceName,
            Code = enrollment.Code,
            Link = link,

            PairUrl = $"{baseUrl}/Kiosk/Pair",

            QrBase64 = Convert.ToBase64String(png),

            ExpiresUtc = enrollment.ExpiresUtc,

            MaxUses = maxUses,

            IsLocalhost =
                baseUrl.Contains(
                    "localhost",
                    StringComparison.OrdinalIgnoreCase)
                ||
                baseUrl.Contains(
                    "127.0.0.1",
                    StringComparison.OrdinalIgnoreCase)
        });
    }

    // ============================================
    // PAIR CURRENT BROWSER DIRECTLY
    // ============================================

    [HttpPost]
    public async Task<IActionResult> ThisDevice(
        string deviceName)
    {
        deviceName = CleanName(deviceName);

        var error = DeviceNameError(deviceName);

        if (error != null)
        {
            TempData["Error"] = error;

            return RedirectToAction(nameof(Add));
        }

        if (await DeviceNameExistsAsync(deviceName))
        {
            TempData["Error"] =
                "An active kiosk already uses this device name.";

            return RedirectToAction(nameof(Add));
        }

        var raw = await _kiosk.PairDirectAsync(
            deviceName,
            "AdminDevice",
            Request.Headers.UserAgent.ToString());

        _kiosk.IssueCookie(HttpContext, raw);

        return Redirect("/Kiosk");
    }

    // ============================================
    // RENAME DEVICE
    // ============================================

    [HttpPost]
    public async Task<IActionResult> Rename(
        int id,
        string name)
    {
        var device =
            await _db.KioskDevices.FindAsync(id);

        if (device == null)
        {
            TempData["Error"] =
                "Kiosk device was not found.";

            return RedirectToAction(nameof(Index));
        }

        name = CleanName(name);

        var error = DeviceNameError(name);

        if (error != null)
        {
            TempData["Error"] = error;

            return RedirectToAction(nameof(Index));
        }

        if (await DeviceNameExistsAsync(name, id))
        {
            TempData["Error"] =
                "Another active kiosk already uses this name.";

            return RedirectToAction(nameof(Index));
        }

        device.Name = name;

        await _db.SaveChangesAsync();

        TempData["Ok"] =
            "Kiosk device renamed successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ============================================
    // REVOKE DEVICE
    // ============================================

    [HttpPost]
    public async Task<IActionResult> Revoke(int id)
    {
        var device =
            await _db.KioskDevices.FindAsync(id);

        if (device == null)
        {
            TempData["Error"] =
                "Kiosk device was not found.";

            return RedirectToAction(nameof(Index));
        }

        device.IsRevoked = true;

        await _db.SaveChangesAsync();

        TempData["Ok"] =
            $"'{device.Name}' revoked. It will fall back to the pairing screen.";

        return RedirectToAction(nameof(Index));
    }

    // ============================================
    // RESTORE DEVICE
    // ============================================

    [HttpPost]
    public async Task<IActionResult> Restore(int id)
    {
        var device =
            await _db.KioskDevices.FindAsync(id);

        if (device == null)
        {
            TempData["Error"] =
                "Kiosk device was not found.";

            return RedirectToAction(nameof(Index));
        }

        if (await DeviceNameExistsAsync(device.Name, id))
        {
            TempData["Error"] =
                "Another active kiosk uses this name. Rename that device before restoring.";

            return RedirectToAction(nameof(Index));
        }

        device.IsRevoked = false;

        await _db.SaveChangesAsync();

        TempData["Ok"] =
            "Kiosk device restored successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ============================================
    // REMOVE DEVICE
    // ============================================

    [HttpPost]
    public async Task<IActionResult> Remove(int id)
    {
        var device =
            await _db.KioskDevices.FindAsync(id);

        if (device == null)
        {
            TempData["Error"] =
                "Kiosk device was not found.";

            return RedirectToAction(nameof(Index));
        }

        _db.KioskDevices.Remove(device);

        await _db.SaveChangesAsync();

        TempData["Ok"] =
            "Kiosk device removed.";

        return RedirectToAction(nameof(Index));
    }

    // ============================================
    // CANCEL PENDING ENROLLMENT
    // ============================================

    [HttpPost]
    public async Task<IActionResult> CancelEnrollment(int id)
    {
        var enrollment =
            await _db.EnrollmentTokens.FindAsync(id);

        if (enrollment == null)
        {
            TempData["Error"] =
                "Enrollment was not found.";

            return RedirectToAction(nameof(Index));
        }

        _db.EnrollmentTokens.Remove(enrollment);

        await _db.SaveChangesAsync();

        TempData["Ok"] =
            "Pending enrollment cancelled.";

        return RedirectToAction(nameof(Index));
    }
}
