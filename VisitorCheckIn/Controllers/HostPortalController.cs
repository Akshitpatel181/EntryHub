using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VisitorCheckIn.Data;
using VisitorCheckIn.Models;
using VisitorCheckIn.Services;
using VisitorCheckIn.ViewModels;

namespace VisitorCheckIn.Controllers;

/// <summary>Hosts log in here on every phone/laptop they want pinged, and click "Enable notifications".</summary>
[Authorize(Roles = "Host")]
public class HostPortalController : Controller
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notify;
    private readonly VapidKeyService _vapid;
    public HostPortalController(AppDbContext db, NotificationService notify, VapidKeyService vapid)
    { _db = db; _notify = notify; _vapid = vapid; }

    private string UserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    private Task<VisitHost> Me() => _db.Hosts.Include(h => h.Devices).FirstOrDefaultAsync(h => h.UserId == UserId);

    public async Task<IActionResult> Index()
    {
        var me = await Me();
        if (me == null) return Content("No host profile is linked to this login. Ask an admin.");
        var today = DateTime.UtcNow.Date.AddDays(-1);
        ViewBag.Visits = await _db.Visits.Where(v => v.HostId == me.Id && v.CheckInUtc >= today)
                                         .OrderByDescending(v => v.CheckInUtc).Take(30).ToListAsync();
        ViewBag.VapidPublic = _vapid.Public ?? "";
        return View(me);
    }

    [HttpPost]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeDto dto)
    {
        var me = await Me();
        if (me == null || string.IsNullOrEmpty(dto?.Endpoint)) return BadRequest();
        var existing = me.Devices.FirstOrDefault(d => d.Endpoint == dto.Endpoint);
        if (existing == null)
            _db.HostDevices.Add(new HostDevice { HostId = me.Id, Endpoint = dto.Endpoint, P256dh = dto.P256dh, Auth = dto.Auth, Label = dto.Label });
        else { existing.P256dh = dto.P256dh; existing.Auth = dto.Auth; existing.Label = dto.Label; }
        await _db.SaveChangesAsync();
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveDevice(int id)
    {
        var me = await Me();
        var d = me?.Devices.FirstOrDefault(x => x.Id == id);
        if (d != null) { _db.HostDevices.Remove(d); await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Test()
    {
        var me = await Me();
        if (me == null) return NotFound();
        var r = await _notify.NotifyAsync(me.Id, "Test notification", "Notifications are working on this device.");
        TempData[r.PushSent > 0 ? "Ok" : "Error"] = r.Describe();
        return RedirectToAction(nameof(Index));
    }
}
