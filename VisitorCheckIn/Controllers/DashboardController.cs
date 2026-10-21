using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VisitorCheckIn.Data;
using VisitorCheckIn.ViewModels;

namespace VisitorCheckIn.Controllers;

[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    public DashboardController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var today = DateTime.UtcNow.Date;
        var online = DateTime.UtcNow.AddMinutes(-3);
        var vm = new DashboardVm
        {
            HostCount = await _db.Hosts.CountAsync(h => h.IsActive),
            QuestionCount = await _db.Questions.CountAsync(q => q.IsActive),
            DeviceCount = await _db.KioskDevices.CountAsync(d => !d.IsRevoked),
            OnlineDevices = await _db.KioskDevices.CountAsync(d => !d.IsRevoked && d.LastSeenUtc > online),
            TodayVisits = await _db.Visits.CountAsync(v => v.CheckInUtc >= today),
            OnSite = await _db.Visits.Include(v => v.Host).Where(v => v.CheckOutUtc == null)
                        .OrderByDescending(v => v.CheckInUtc).Take(50).ToListAsync()
        };
        return View(vm);
    }
}
