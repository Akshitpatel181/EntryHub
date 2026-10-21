using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VisitorCheckIn.ViewModels;

namespace VisitorCheckIn.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<IdentityUser> _signIn;
    public AccountController(SignInManager<IdentityUser> signIn) => _signIn = signIn;

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

    [AllowAnonymous] public IActionResult Denied() => View();
}
