using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VisitorCheckIn.Data;
using VisitorCheckIn.Models;
using VisitorCheckIn.Services;
using VisitorCheckIn.ViewModels;

namespace VisitorCheckIn.Controllers;

[Authorize(Roles = "Admin")]
public class HostsController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _users;
    private readonly NotificationService _notify;
    public HostsController(AppDbContext db, UserManager<IdentityUser> users, NotificationService notify)
    { _db = db; _users = users; _notify = notify; }

    public async Task<IActionResult> Index() =>
        View(await _db.Hosts.Include(h => h.Devices).OrderBy(h => h.Name).ToListAsync());

    public IActionResult Create() => View("Form", new HostFormVm());

    [HttpPost]
    public async Task<IActionResult> Create(HostFormVm vm)
    {
        Validate(vm, isNew: true);
        if (!ModelState.IsValid) return View("Form", vm);

        var h = new VisitHost { Name = vm.Name.Trim(), Email = vm.Email?.Trim(), Phone = vm.Phone?.Trim(), IsActive = vm.IsActive };
        _db.Hosts.Add(h);
        await _db.SaveChangesAsync();

        if (vm.CreateLogin)
        {
            var err = await EnsureLoginAsync(h, vm.Password);
            if (err != null) TempData["Error"] = "Host saved, but login failed: " + err;
        }
        TempData["Ok"] = "Host added.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var h = await _db.Hosts.FindAsync(id);
        if (h == null) return NotFound();
        return View("Form", new HostFormVm
        {
            Id = h.Id, Name = h.Name, Email = h.Email, Phone = h.Phone, IsActive = h.IsActive,
            HasLogin = h.UserId != null, CreateLogin = h.UserId != null
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, HostFormVm vm)
    {
        var h = await _db.Hosts.FindAsync(id);
        if (h == null) return NotFound();
        vm.Id = id; vm.HasLogin = h.UserId != null;
        Validate(vm, isNew: false);
        if (!ModelState.IsValid) return View("Form", vm);

        h.Name = vm.Name.Trim(); h.Email = vm.Email?.Trim(); h.Phone = vm.Phone?.Trim(); h.IsActive = vm.IsActive;
        await _db.SaveChangesAsync();

        if (vm.CreateLogin && (h.UserId == null || !string.IsNullOrEmpty(vm.Password)))
        {
            var err = await EnsureLoginAsync(h, vm.Password);
            if (err != null) TempData["Error"] = "Saved, but login failed: " + err;
        }
        TempData["Ok"] = "Host updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var h = await _db.Hosts.FindAsync(id);
        if (h == null) return NotFound();
        if (await _db.Visits.AnyAsync(v => v.HostId == id))
        {
            h.IsActive = false;
            TempData["Ok"] = "Host has visit history, so they were deactivated instead of deleted.";
        }
        else
        {
            if (h.UserId != null) { var u = await _users.FindByIdAsync(h.UserId); if (u != null) await _users.DeleteAsync(u); }
            _db.Hosts.Remove(h);
            TempData["Ok"] = "Host deleted.";
        }
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Sends a test ping to ALL of this host's devices.</summary>
    [HttpPost]
    public async Task<IActionResult> TestNotify(int id)
    {
        var r = await _notify.NotifyAsync(id, "Test notification", "This is a test from the check-in admin.");
        TempData[r.PushSent > 0 ? "Ok" : "Error"] = r.Describe();
        return RedirectToAction(nameof(Index));
    }

    private void Validate(HostFormVm vm, bool isNew)
    {
        if (vm.CreateLogin && string.IsNullOrWhiteSpace(vm.Email))
            ModelState.AddModelError(nameof(vm.Email), "Email is required to create a login.");
        if (vm.CreateLogin && !vm.HasLogin && string.IsNullOrWhiteSpace(vm.Password))
            ModelState.AddModelError(nameof(vm.Password), "Set a password for the new login.");
    }

    private async Task<string> EnsureLoginAsync(VisitHost h, string password)
    {
        var user = h.UserId != null ? await _users.FindByIdAsync(h.UserId) : null;
        if (user == null)
        {
            user = new IdentityUser { UserName = h.Email, Email = h.Email, EmailConfirmed = true };
            var r = await _users.CreateAsync(user, password);
            if (!r.Succeeded) return string.Join("; ", r.Errors.Select(e => e.Description));
            await _users.AddToRoleAsync(user, "Host");
            h.UserId = user.Id;
            await _db.SaveChangesAsync();
        }
        else if (!string.IsNullOrEmpty(password))
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var r = await _users.ResetPasswordAsync(user, token, password);
            if (!r.Succeeded) return string.Join("; ", r.Errors.Select(e => e.Description));
        }
        return null;
    }
}
