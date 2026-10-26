
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VisitorCheckIn.ViewModels;

namespace VisitorCheckIn.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<IdentityUser> _signIn;
    private readonly UserManager<IdentityUser> _userManager;

    public AccountController(
        SignInManager<IdentityUser> signIn,
        UserManager<IdentityUser> userManager)
    {
        _signIn = signIn;
        _userManager = userManager;
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
            return View(vm);

        var email = vm.Email.Trim();

        var result = await _signIn.PasswordSignInAsync(
            email,
            vm.Password,
            vm.Remember,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            // Preserve valid return URLs, such as a
            // page the user originally tried to access.
            if (!string.IsNullOrEmpty(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            // Automatically direct hosts to their portal.
            if (User.Identity?.IsAuthenticated == true)
            {
                // The authentication principal may not
                // be refreshed on this same request,
                // so role lookup below uses UserManager.
            }

            var user = await _userManager.FindByNameAsync(email);

            if (user != null &&
                await _userManager.IsInRoleAsync(user, "Admin"))
            {
                return RedirectToAction(
                    "Index",
                    "Dashboard");
            }

            if (user != null &&
                await _userManager.IsInRoleAsync(user, "Host"))
            {
                return RedirectToAction(
                    "Index",
                    "HostPortal");
            }

            return RedirectToAction("Index", "Home");
        }

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

        return RedirectToAction("Login");
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
        // PASSWORD LENGTH
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
        // GET LOGGED-IN USER
        // ----------------------------------------

        var user = await _userManager.GetUserAsync(User);

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

        var result = await _userManager.ChangePasswordAsync(
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

        return RedirectToAction(nameof(ChangePassword));
    }

    // ============================================
    // ACCESS DENIED
    // ============================================

    [AllowAnonymous]
    public IActionResult Denied()
    {
        return View();
    }
}
