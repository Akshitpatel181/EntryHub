
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VisitorCheckIn.Data;
using VisitorCheckIn.ViewModels;

namespace VisitorCheckIn.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<IdentityUser> _signIn;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly AppDbContext _db;

    public AccountController(
        SignInManager<IdentityUser> signIn,
        UserManager<IdentityUser> userManager,
        AppDbContext db)
    {
        _signIn = signIn;
        _userManager = userManager;
        _db = db;
    }

    // ============================================
    // LOGIN PAGE
    // ============================================

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;

        return View(new LoginVm());
    }

    // ============================================
    // LOGIN SUBMISSION
    // ============================================

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginVm vm,
        string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        var email = vm.Email?.Trim() ?? "";

        // ----------------------------------------
        // CHECK ACCOUNT STATUS
        // ----------------------------------------

        var loginUser =
            await _userManager.FindByNameAsync(email);

        if (loginUser != null)
        {
            var isAdmin =
                await _userManager.IsInRoleAsync(
                    loginUser, "Admin");

            var isHost =
                await _userManager.IsInRoleAsync(
                    loginUser, "Host");

            // Do not allow inactive or unlinked
            // Host accounts to access EntryHub.
            if (isHost && !isAdmin)
            {
                var activeHost = await _db.Hosts
                    .AsNoTracking()
                    .AnyAsync(h =>
                        h.UserId == loginUser.Id &&
                        h.IsActive);

                if (!activeHost)
                {
                    ModelState.AddModelError(
                        "",
                        "Invalid email or password.");

                    return View(vm);
                }
            }
        }

        // ----------------------------------------
        // SIGN IN WITH ASP.NET IDENTITY
        // ----------------------------------------

        var result = await _signIn.PasswordSignInAsync(
            email,
            vm.Password,
            vm.Remember,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var user = loginUser ??
                await _userManager.FindByNameAsync(email);

            // ------------------------------------
            // DETERMINE USER ROLE
            // ------------------------------------

            var isAdmin = user != null &&
                await _userManager.IsInRoleAsync(
                    user, "Admin");

            var isHost = user != null &&
                await _userManager.IsInRoleAsync(
                    user, "Host");

            // ------------------------------------
            // PRESERVE LOCAL RETURN URL
            // ------------------------------------

            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            // ------------------------------------
            // ADMIN DASHBOARD
            // ------------------------------------

            if (isAdmin)
            {
                return RedirectToAction(
                    "Index",
                    "Dashboard");
            }

            // ------------------------------------
            // HOST PORTAL
            // ------------------------------------

            if (isHost)
            {
                return RedirectToAction(
                    "Index",
                    "HostPortal");
            }

            return RedirectToAction(
                "Index",
                "Home");
        }

        // ----------------------------------------
        // LOGIN ERROR MESSAGES
        // ----------------------------------------

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(
                "",
                "Account temporarily locked. Please try again later.");
        }
        else if (result.IsNotAllowed)
        {
            ModelState.AddModelError(
                "",
                "This account is not currently permitted to sign in.");
        }
        else
        {
            ModelState.AddModelError(
                "",
                "Invalid email or password.");
        }

        return View(vm);
    }

    // ============================================
    // LOGOUT
    // ============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();

        return RedirectToAction(
            nameof(Login));
    }

    // ============================================
    // CHANGE PASSWORD PAGE
    // ============================================

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View();
    }

    // ============================================
    // CHANGE PASSWORD SUBMISSION
    // ============================================

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        string currentPassword,
        string newPassword,
        string confirmPassword)
    {
        // ----------------------------------------
        // REQUIRED FIELDS
        // ----------------------------------------

        if (string.IsNullOrEmpty(currentPassword) ||
            string.IsNullOrEmpty(newPassword) ||
            string.IsNullOrEmpty(confirmPassword))
        {
            ModelState.AddModelError(
                "",
                "All password fields are required.");

            return View();
        }

        // ----------------------------------------
        // NEW PASSWORD LENGTH
        // ----------------------------------------

        if (newPassword.Length < 12 ||
            newPassword.Length > 128)
        {
            ModelState.AddModelError(
                "",
                "New password must be between 12 and 128 characters.");

            return View();
        }

        // ----------------------------------------
        // CONFIRM PASSWORD
        // ----------------------------------------

        if (newPassword != confirmPassword)
        {
            ModelState.AddModelError(
                "",
                "New password and confirmation do not match.");

            return View();
        }

        // ----------------------------------------
        // GET CURRENT USER
        // ----------------------------------------

        var user =
            await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return Challenge();
        }

        // ----------------------------------------
        // VERIFY CURRENT PASSWORD
        // ----------------------------------------

        var currentIsCorrect =
            await _userManager.CheckPasswordAsync(
                user,
                currentPassword);

        if (!currentIsCorrect)
        {
            ModelState.AddModelError(
                "",
                "Current password is incorrect.");

            return View();
        }

        // ----------------------------------------
        // PREVENT REUSING CURRENT PASSWORD
        // ----------------------------------------

        if (currentPassword == newPassword)
        {
            ModelState.AddModelError(
                "",
                "Your new password must be different from your current password.");

            return View();
        }

        // ----------------------------------------
        // CHANGE PASSWORD USING IDENTITY
        // ----------------------------------------

        var result =
            await _userManager.ChangePasswordAsync(
                user,
                currentPassword,
                newPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    "",
                    error.Description);
            }

            return View();
        }

        // ----------------------------------------
        // REFRESH LOGIN SESSION
        // ----------------------------------------

        await _signIn.RefreshSignInAsync(user);

        TempData["SuccessMessage"] =
            "Your password has been changed successfully.";

        return RedirectToAction(
            nameof(ChangePassword));
    }

    // ============================================
    // ACCESS DENIED
    // ============================================

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Denied()
    {
        return View();
    }
}
