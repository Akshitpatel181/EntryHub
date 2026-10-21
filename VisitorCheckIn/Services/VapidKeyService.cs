using Microsoft.EntityFrameworkCore;
using VisitorCheckIn.Data;
using VisitorCheckIn.Models;
using WebPush;

namespace VisitorCheckIn.Services;

/// <summary>
/// Holds the VAPID keys that Web Push needs. Nobody has to generate or paste keys any more:
/// keys from appsettings.json win; otherwise they are generated once and stored in the database.
/// </summary>
public class VapidKeyService
{
    public string Public { get; private set; } = "";
    public string Private { get; private set; } = "";
    public string Subject { get; private set; } = "mailto:admin@example.com";
    public bool Configured => !string.IsNullOrWhiteSpace(Public) && !string.IsNullOrWhiteSpace(Private);

    public async Task InitAsync(AppDbContext db, IConfiguration cfg)
    {
        var subject = cfg["Vapid:Subject"];
        if (!string.IsNullOrWhiteSpace(subject)) Subject = subject.Trim();
        else if (!string.IsNullOrWhiteSpace(cfg["Admin:Email"])) Subject = "mailto:" + cfg["Admin:Email"].Trim();

        var pub = cfg["Vapid:Public"];
        var priv = cfg["Vapid:Private"];
        if (!string.IsNullOrWhiteSpace(pub) && !string.IsNullOrWhiteSpace(priv))
        {
            Public = pub.Trim(); Private = priv.Trim();
            return;
        }

        var rows = await db.Settings.Where(x => x.Key == "Vapid:Public" || x.Key == "Vapid:Private").ToListAsync();
        pub = rows.FirstOrDefault(x => x.Key == "Vapid:Public")?.Value;
        priv = rows.FirstOrDefault(x => x.Key == "Vapid:Private")?.Value;

        if (string.IsNullOrWhiteSpace(pub) || string.IsNullOrWhiteSpace(priv))
        {
            var keys = VapidHelper.GenerateVapidKeys();
            pub = keys.PublicKey; priv = keys.PrivateKey;
            if (rows.Count > 0) db.Settings.RemoveRange(rows);
            db.Settings.Add(new AppSetting { Key = "Vapid:Public", Value = pub });
            db.Settings.Add(new AppSetting { Key = "Vapid:Private", Value = priv });
            await db.SaveChangesAsync();
        }
        Public = pub; Private = priv;
    }
}
