using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using VisitorCheckIn.Data;
using VisitorCheckIn.Services;

namespace VisitorCheckIn.Filters;

/// <summary>Only a browser holding a valid, non-revoked kiosk cookie may use kiosk actions.</summary>
public class KioskAuthAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        var http = ctx.HttpContext;
        var db = http.RequestServices.GetRequiredService<AppDbContext>();
        var isAjax = http.Request.Headers.ContainsKey("X-Requested-With");

        void Deny() => ctx.Result = isAjax ? (IActionResult)new UnauthorizedResult() : new RedirectToActionResult("Pair", "Kiosk", null);

        var token = http.Request.Cookies[KioskService.CookieName];
        if (string.IsNullOrEmpty(token)) { Deny(); return; }

        var hash = KioskService.Hash(token);
        var dev = await db.KioskDevices.FirstOrDefaultAsync(d => d.TokenHash == hash && !d.IsRevoked);
        if (dev == null)
        {
            http.Response.Cookies.Delete(KioskService.CookieName);
            Deny(); return;
        }

        dev.LastSeenUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        http.Items["KioskDevice"] = dev;
        await next();
    }
}
