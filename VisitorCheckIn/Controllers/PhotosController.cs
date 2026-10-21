using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VisitorCheckIn.Data;

namespace VisitorCheckIn.Controllers;

/// <summary>
/// Serves visitor photos at /p/{token}. The token is 192 random bits, so the link cannot be guessed.
/// It has to work without a login because phone / desktop notifications and email clients fetch the picture themselves.
/// </summary>
[AllowAnonymous]
public class PhotosController : Controller
{
    private readonly AppDbContext _db;
    public PhotosController(AppDbContext db) => _db = db;

    [HttpGet("p/{token}")]
    public async Task<IActionResult> Get(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 64) return NotFound();
        var photo = await _db.VisitPhotos.AsNoTracking().Where(p => p.Token == token)
            .Select(p => new { p.Data, p.ContentType }).FirstOrDefaultAsync();
        if (photo == null) return NotFound();
        Response.Headers["Cache-Control"] = "private, max-age=86400";
        Response.Headers["X-Robots-Tag"] = "noindex";
        return File(photo.Data, photo.ContentType);
    }
}
