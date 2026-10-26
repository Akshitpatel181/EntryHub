
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

    public HostsController(
        AppDbContext db,
        UserManager<IdentityUser> users,
        NotificationService notify)
    {
        _db = db;
        _users = users;
        _notify = notify;
    }

    // ============================================
    // HOST LIST
    // ============================================

    public async Task<IActionResult> Index()
    {
        var hosts = await _db.Hosts
            .Include(h => h.Devices)
            .OrderBy(h => h.Name)
            .ToListAsync();

        return View(hosts);
    }

    // ============================================
    // CREATE HOST
    // ============================================

    [HttpGet]
    public IActionResult Create()
    {
        return View("Form", new HostFormVm());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(HostFormVm vm)
    {
        Normalize(vm);

        await ValidateHostAsync(vm, isNew: true);

        if (!ModelState.IsValid)
            return View("Form", vm);

        // Use one transaction so a failed login creation
        // does not leave behind a partially created host.
        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var host = new VisitHost
        {
            Name = vm.Name.Trim(),
            Email = vm.Email,
            Phone = vm.Phone,
            IsActive = vm.IsActive
        };

        _db.Hosts.Add(host);

        await _db.SaveChangesAsync();

        if (vm.CreateLogin)
        {
            var error = await EnsureLoginAsync(
                host,
                vm.Password);

            if (error != null)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    "",
                    "Unable to create host login: " + error);

                return View("Form", vm);
            }
        }

        await transaction.CommitAsync();

        TempData["Ok"] = "Host added successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ============================================
    // EDIT HOST
    // ============================================

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var host = await _db.Hosts.FindAsync(id);

        if (host == null)
            return NotFound();

        return View("Form", new HostFormVm
        {
            Id = host.Id,
            Name = host.Name,
            Email = host.Email,
            Phone = host.Phone,
            IsActive = host.IsActive,
            HasLogin = host.UserId != null,
            CreateLogin = host.UserId != null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        HostFormVm vm)
    {
        var host = await _db.Hosts.FindAsync(id);

        if (host == null)
            return NotFound();

        vm.Id = id;
        vm.HasLogin = host.UserId != null;

        Normalize(vm);

        await ValidateHostAsync(vm, isNew: false);

        if (!ModelState.IsValid)
            return View("Form", vm);

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        // Update the Identity login first if one exists.
        // This also synchronises the username and email.
        if (host.UserId != null)
        {
            var identityUser =
                await _users.FindByIdAsync(host.UserId);

            if (identityUser == null)
            {
                ModelState.AddModelError(
                    "",
                    "The host login account could not be found.");

                return View("Form", vm);
            }

            if (!string.IsNullOrWhiteSpace(vm.Email) &&
                !string.Equals(
                    identityUser.Email,
                    vm.Email,
                    StringComparison.OrdinalIgnoreCase))
            {
                identityUser.Email = vm.Email;
                identityUser.UserName = vm.Email;

                identityUser.NormalizedEmail =
                    _users.NormalizeEmail(vm.Email);

                identityUser.NormalizedUserName =
                    _users.NormalizeName(vm.Email);

                // An administrator changing a login email
                // makes the new email confirmed for this account.
                // We can add actual email verification later.
                identityUser.EmailConfirmed = true;

                var update = await _users.UpdateAsync(identityUser);

                if (!update.Succeeded)
                {
                    AddIdentityErrors(update);

                    await transaction.RollbackAsync();

                    return View("Form", vm);
                }
            }
        }

        host.Name = vm.Name.Trim();
        host.Email = vm.Email;
        host.Phone = vm.Phone;
        host.IsActive = vm.IsActive;

        await _db.SaveChangesAsync();

        // Create a missing login, or reset an existing
        // password only when a new password is provided.
        if (vm.CreateLogin &&
            (host.UserId == null ||
             !string.IsNullOrWhiteSpace(vm.Password)))
        {
            var error = await EnsureLoginAsync(
                host,
                vm.Password);

            if (error != null)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    "",
                    "Unable to update host login: " + error);

                return View("Form", vm);
            }
        }

        await transaction.CommitAsync();

        TempData["Ok"] = "Host updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ============================================
    // DELETE OR DEACTIVATE HOST
    // ============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var host = await _db.Hosts.FindAsync(id);

        if (host == null)
            return NotFound();

        if (await _db.Visits.AnyAsync(v => v.HostId == id))
        {
            host.IsActive = false;

            TempData["Ok"] =
                "Host has visit history, so they were deactivated instead of deleted.";
        }
        else
        {
            if (host.UserId != null)
            {
                var user =
                    await _users.FindByIdAsync(host.UserId);

                if (user != null)
                {
                    var result = await _users.DeleteAsync(user);

                    if (!result.Succeeded)
                    {
                        TempData["Error"] =
                            "Unable to delete the host login.";

                        return RedirectToAction(nameof(Index));
                    }
                }
            }

            _db.Hosts.Remove(host);

            TempData["Ok"] = "Host deleted.";
        }

        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // ============================================
    // TEST HOST NOTIFICATION
    // ============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestNotify(int id)
    {
        var result = await _notify.NotifyAsync(
            id,
            "Test notification",
            "This is a test from the check-in admin.");

        TempData[result.PushSent > 0 ? "Ok" : "Error"] =
            result.Describe();

        return RedirectToAction(nameof(Index));
    }

    // ============================================
    // NORMALISE CONTACT DETAILS
    // ============================================

    private static void Normalize(HostFormVm vm)
    {
        vm.Name = vm.Name?.Trim() ?? "";

        vm.Email = string.IsNullOrWhiteSpace(vm.Email)
            ? null
            : vm.Email.Trim();

        vm.Phone = string.IsNullOrWhiteSpace(vm.Phone)
            ? null
            : vm.Phone.Trim();
    }

    // ============================================
    // SERVER-SIDE VALIDATION
    // ============================================

    private async Task ValidateHostAsync(
        HostFormVm vm,
        bool isNew)
    {
        // Email is required when creating a login,
        // or when a login already exists.
        if ((vm.CreateLogin || vm.HasLogin) &&
            string.IsNullOrWhiteSpace(vm.Email))
        {
            ModelState.AddModelError(
                nameof(vm.Email),
                "An email address is required for a host login.");
        }

        // Check duplicate host email addresses.
        if (!string.IsNullOrWhiteSpace(vm.Email))
        {
            var duplicateHost = await _db.Hosts.AnyAsync(
                h => h.Id != vm.Id &&
                     h.Email != null &&
                     h.Email.ToLower() == vm.Email.ToLower());

            if (duplicateHost)
            {
                ModelState.AddModelError(
                    nameof(vm.Email),
                    "Another host already uses this email address.");
            }

            // Check if another ASP.NET Identity account
            // already owns this login email.
            var existingUser =
                await _users.FindByEmailAsync(vm.Email);

            if (existingUser != null)
            {
                string? linkedUserId = null;

                if (!isNew)
                {
                    linkedUserId = await _db.Hosts
                        .Where(h => h.Id == vm.Id)
                        .Select(h => h.UserId)
                        .FirstOrDefaultAsync();
                }

                if (existingUser.Id != linkedUserId)
                {
                    ModelState.AddModelError(
                        nameof(vm.Email),
                        "This email address is already used by another login account.");
                }
            }
        }

        // New login requires a password.
        if (vm.CreateLogin &&
            !vm.HasLogin &&
            string.IsNullOrWhiteSpace(vm.Password))
        {
            ModelState.AddModelError(
                nameof(vm.Password),
                "Please create a password for this host.");
        }

        // The ViewModel enforces minimum password
        // length when a value is supplied.
        // Also validate it explicitly on the server.
        if (!string.IsNullOrEmpty(vm.Password))
        {
            if (vm.Password.Length < 12 ||
                vm.Password.Length > 128)
            {
                ModelState.AddModelError(
                    nameof(vm.Password),
                    "Password must be between 12 and 128 characters.");
            }
        }

        // Do not require a new password when
        // editing an existing host.
    }

    // ============================================
    // CREATE OR RESET IDENTITY LOGIN
    // ============================================

    private async Task<string?> EnsureLoginAsync(
        VisitHost host,
        string? password)
    {
        var user = host.UserId != null
            ? await _users.FindByIdAsync(host.UserId)
            : null;

        if (user == null)
        {
            if (string.IsNullOrWhiteSpace(host.Email))
                return "Host email is required.";

            if (string.IsNullOrWhiteSpace(password))
                return "A password is required.";

            user = new IdentityUser
            {
                UserName = host.Email,
                Email = host.Email,
                EmailConfirmed = true
            };

            var createResult = await _users.CreateAsync(
                user,
                password);

            if (!createResult.Succeeded)
            {
                return string.Join(
                    "; ",
                    createResult.Errors.Select(e => e.Description));
            }

            var roleResult = await _users.AddToRoleAsync(
                user,
                "Host");

            if (!roleResult.Succeeded)
            {
                return string.Join(
                    "; ",
                    roleResult.Errors.Select(e => e.Description));
            }

            host.UserId = user.Id;

            await _db.SaveChangesAsync();
        }
        else if (!string.IsNullOrWhiteSpace(password))
        {
            var token =
                await _users.GeneratePasswordResetTokenAsync(user);

            var resetResult =
                await _users.ResetPasswordAsync(
                    user,
                    token,
                    password);

            if (!resetResult.Succeeded)
            {
                return string.Join(
                    "; ",
                    resetResult.Errors.Select(e => e.Description));
            }
        }

        return null;
    }

    // ============================================
    // IDENTITY ERROR MESSAGES
    // ============================================

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(
                "",
                error.Description);
        }
    }
}
