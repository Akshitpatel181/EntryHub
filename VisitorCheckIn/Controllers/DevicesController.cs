using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using VisitorCheckIn.Data;
using VisitorCheckIn.Services;
using VisitorCheckIn.ViewModels;

namespace VisitorCheckIn.Controllers;

/// <summary>Admin: add / revoke kiosk devices. Five enrolment methods, all end up in KioskService.</summary>
[Authorize(Roles = "Admin")]
public class DevicesController : Controller
{
    private readonly AppDbContext _db;
    private readonly KioskService _kiosk;
    private readonly IConfiguration _cfg;
    public DevicesController(AppDbContext db, KioskService kiosk, IConfiguration cfg)
    { _db = db; _kiosk = kiosk; _cfg = cfg; }

    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow;
        return View(new DevicesIndexVm
        {
            Devices = await _db.KioskDevices.OrderByDescending(d => d.PairedUtc).ToListAsync(),
            Pending = await _db.EnrollmentTokens.Where(e => e.ExpiresUtc > now && e.UseCount < e.MaxUses)
                               .OrderByDescending(e => e.CreatedUtc).ToListAsync()
        });
    }

    public IActionResult Add() => View();
    public IActionResult Guide() => View();

    /// <summary>Methods A, B, C (single device: code + QR + link) and E (bulk / MDM link: maxUses &gt; 1).</summary>
    [HttpPost]
    public async Task<IActionResult> Enroll(string deviceName, int maxUses = 1, int minutes = 15)
    {
        maxUses = Math.Clamp(maxUses, 1, 500);
        minutes = Math.Clamp(minutes, 5, 60 * 24 * 14);
        var (e, linkToken) = await _kiosk.CreateEnrollmentAsync(deviceName, maxUses, TimeSpan.FromMinutes(minutes));

        var baseUrl = _cfg["App:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = $"{Request.Scheme}://{Request.Host}";
        baseUrl = baseUrl.TrimEnd('/');
        var link = $"{baseUrl}/Kiosk/Enroll?token={linkToken}";

        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(link, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(10);

        return View("Enrollment", new EnrollmentResultVm
        {
            DeviceName = e.DeviceName, Code = e.Code, Link = link, PairUrl = $"{baseUrl}/Kiosk/Pair",
            QrBase64 = Convert.ToBase64String(png), ExpiresUtc = e.ExpiresUtc, MaxUses = maxUses,
            IsLocalhost = baseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) || baseUrl.Contains("127.0.0.1")
        });
    }

    /// <summary>Method D: turn the browser the admin is using right now into a kiosk.</summary>
    [HttpPost]
    public async Task<IActionResult> ThisDevice(string deviceName)
    {
        var raw = await _kiosk.PairDirectAsync(deviceName, "AdminDevice", Request.Headers.UserAgent.ToString());
        _kiosk.IssueCookie(HttpContext, raw);
        return Redirect("/Kiosk");
    }

    [HttpPost]
    public async Task<IActionResult> Rename(int id, string name)
    {
        var d = await _db.KioskDevices.FindAsync(id);
        if (d != null && !string.IsNullOrWhiteSpace(name)) { d.Name = name.Trim(); await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Revoke(int id)
    {
        var d = await _db.KioskDevices.FindAsync(id);
        if (d != null) { d.IsRevoked = true; await _db.SaveChangesAsync(); TempData["Ok"] = $"'{d.Name}' revoked. It will fall back to the pairing screen."; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Restore(int id)
    {
        var d = await _db.KioskDevices.FindAsync(id);
        if (d != null) { d.IsRevoked = false; await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Remove(int id)
    {
        var d = await _db.KioskDevices.FindAsync(id);
        if (d != null) { _db.KioskDevices.Remove(d); await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> CancelEnrollment(int id)
    {
        var e = await _db.EnrollmentTokens.FindAsync(id);
        if (e != null) { _db.EnrollmentTokens.Remove(e); await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }
}
