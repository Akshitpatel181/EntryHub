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

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string returnUrl = null) { ViewBag.ReturnUrl = returnUrl; return View(new LoginVm()); }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Login(LoginVm vm, string returnUrl = null)
    {
        if (!ModelState.IsValid) return View(vm);
        var r = await _signIn.PasswordSignInAsync(vm.Email, vm.Password, vm.Remember, lockoutOnFailure: true);
        if (r.Succeeded)
            return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Home");
        ModelState.AddModelError("", r.IsLockedOut ? "Account locked. Try again later." : "Invalid email or password.");
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Logout() { await _signIn.SignOutAsync(); return RedirectToAction("Login"); }


    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View();
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        string currentPassword,
        string newPassword,
        string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) ||
            string.IsNullOrWhiteSpace(newPassword) ||
            string.IsNullOrWhiteSpace(confirmPassword))
        {
            ModelState.AddModelError("", "All fields are required.");
            return View();
        }

        if (newPassword != confirmPassword)
        {
            ModelState.AddModelError("", "New passwords do not match.");
            return View();
        }

        var user = await _userManager.GetUserAsync(User);

        if (user == null)
            return Challenge();

        var result = await _userManager.ChangePasswordAsync(
            user, currentPassword, newPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View();
        }

        await _signIn.RefreshSignInAsync(user);

        TempData["SuccessMessage"] = "Password changed successfully.";

        return RedirectToAction(nameof(ChangePassword));
    }

    [AllowAnonymous]
    public IActionResult Denied() => View();
}