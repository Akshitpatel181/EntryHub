using Microsoft.AspNetCore.Mvc;

namespace VisitorCheckIn.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.IsInRole("Admin")) return RedirectToAction("Index", "Dashboard");
        if (User.IsInRole("Host")) return RedirectToAction("Index", "HostPortal");
        return View();
    }

    public IActionResult Error() => View();
}
