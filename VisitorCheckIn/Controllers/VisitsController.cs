using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VisitorCheckIn.Data;
using VisitorCheckIn.Models;

namespace VisitorCheckIn.Controllers;

[Authorize(Roles = "Admin")]
public class VisitsController : Controller
{
    private readonly AppDbContext _db;
    public VisitsController(AppDbContext db) => _db = db;

    private IQueryable<Visit> Query(DateTime? from, DateTime? to, string q, bool onsite)
    {
        var f = (from ?? DateTime.UtcNow.Date.AddDays(-7)).Date;
        var t = (to ?? DateTime.UtcNow.Date).Date.AddDays(1);
        var query = _db.Visits.Include(v => v.Host).Include(v => v.KioskDevice).Include(v => v.Answers)
                       .Where(v => v.CheckInUtc >= f && v.CheckInUtc < t);
        if (onsite) query = query.Where(v => v.CheckOutUtc == null);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(v => v.VisitorName.Contains(q) || v.VisitorCompany.Contains(q) || v.Host.Name.Contains(q));
        return query.OrderByDescending(v => v.CheckInUtc);
    }

    public async Task<IActionResult> Index(DateTime? from, DateTime? to, string q, bool onsite = false)
    {
        ViewBag.From = (from ?? DateTime.UtcNow.Date.AddDays(-7)).ToString("yyyy-MM-dd");
        ViewBag.To = (to ?? DateTime.UtcNow.Date).ToString("yyyy-MM-dd");
        ViewBag.Q = q; ViewBag.OnSite = onsite;
        return View(await Query(from, to, q, onsite).Take(500).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> CheckOut(int id)
    {
        var v = await _db.Visits.FindAsync(id);
        if (v != null && v.CheckOutUtc == null) { v.CheckOutUtc = DateTime.UtcNow; await _db.SaveChangesAsync(); }
        return Redirect(Request.Headers.Referer.ToString() is { Length: > 0 } r ? r : "/Visits");
    }

    public async Task<IActionResult> Export(DateTime? from, DateTime? to, string q, bool onsite = false)
    {
        var rows = await Query(from, to, q, onsite).ToListAsync();
        var sb = new StringBuilder("Visitor,Company,Type,Host,CheckInUTC,CheckOutUTC,Kiosk,Answers\n");
        static string C(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        foreach (var v in rows)
        {
            var answers = string.Join(" | ", v.Answers.Where(a => a.Answer?.StartsWith("data:image") != true)
                                                       .Select(a => $"{a.QuestionText}: {a.Answer}"));
            sb.AppendLine(string.Join(",", C(v.VisitorName), C(v.VisitorCompany), C(v.VisitorType), C(v.Host?.Name),
                C(v.CheckInUtc.ToString("u")), C(v.CheckOutUtc?.ToString("u")), C(v.KioskDevice?.Name), C(answers)));
        }
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"visits-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
