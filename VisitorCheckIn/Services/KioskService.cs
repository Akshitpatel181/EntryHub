using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VisitorCheckIn.Data;
using VisitorCheckIn.Models;

namespace VisitorCheckIn.Services;

/// <summary>All the ways of turning a browser into a kiosk end up here.</summary>
public class KioskService
{
    public const string CookieName = "kiosk_token";
    private readonly AppDbContext _db;
    public KioskService(AppDbContext db) => _db = db;

    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Creates a pending enrolment (6-digit code + one-time link). maxUses &gt; 1 = bulk enrolment.</summary>
    public async Task<(EnrollmentToken enrollment, string linkToken)> CreateEnrollmentAsync(string name, int maxUses, TimeSpan ttl)
    {
        string code;
        do
        {
            code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        } while (await _db.EnrollmentTokens.AnyAsync(x => x.Code == code && x.ExpiresUtc > DateTime.UtcNow && x.UseCount < x.MaxUses));

        var linkToken = NewToken();
        var e = new EnrollmentToken
        {
            DeviceName = string.IsNullOrWhiteSpace(name) ? "Kiosk" : name.Trim(),
            Code = code,
            LinkTokenHash = Hash(linkToken),
            MaxUses = maxUses,
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.Add(ttl)
        };
        _db.EnrollmentTokens.Add(e);
        await _db.SaveChangesAsync();
        return (e, linkToken);
    }

    public Task<EnrollmentToken> FindByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return Task.FromResult<EnrollmentToken>(null);
        var now = DateTime.UtcNow;
        return _db.EnrollmentTokens.FirstOrDefaultAsync(e => e.Code == code && e.ExpiresUtc > now && e.UseCount < e.MaxUses);
    }

    public Task<EnrollmentToken> FindByLinkAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return Task.FromResult<EnrollmentToken>(null);
        var h = Hash(token);
        var now = DateTime.UtcNow;
        return _db.EnrollmentTokens.FirstOrDefaultAsync(e => e.LinkTokenHash == h && e.ExpiresUtc > now && e.UseCount < e.MaxUses);
    }

    /// <summary>Consumes one use of the enrolment and registers the device. Returns the raw cookie token.</summary>
    public async Task<string> RedeemAsync(EnrollmentToken e, string method, string deviceName, string userAgent)
    {
        e.UseCount++;
        var name = !string.IsNullOrWhiteSpace(deviceName) ? deviceName.Trim()
                 : e.MaxUses > 1 ? $"{e.DeviceName} #{e.UseCount}" : e.DeviceName;
        return await CreateDeviceAsync(name, method, userAgent);
    }

    public Task<string> PairDirectAsync(string name, string method, string userAgent) =>
        CreateDeviceAsync(string.IsNullOrWhiteSpace(name) ? "This device" : name.Trim(), method, userAgent);

    private async Task<string> CreateDeviceAsync(string name, string method, string userAgent)
    {
        var raw = NewToken();
        _db.KioskDevices.Add(new KioskDevice
        {
            Name = name,
            TokenHash = Hash(raw),
            PairMethod = method,
            UserAgent = userAgent?.Length > 300 ? userAgent[..300] : userAgent,
            PairedUtc = DateTime.UtcNow,
            LastSeenUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return raw;
    }

    public void IssueCookie(HttpContext ctx, string rawToken) =>
        ctx.Response.Cookies.Append(CookieName, rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true
        });
}
