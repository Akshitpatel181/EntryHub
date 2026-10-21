using Microsoft.EntityFrameworkCore;
using VisitorCheckIn.Data;

namespace VisitorCheckIn.Services;

/// <summary>Visitors who forgot to sign out are auto checked-out after 16 hours. Also applies the optional photo retention rule.</summary>
public class AutoCheckoutService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    public AutoCheckoutService(IServiceScopeFactory scopes) => _scopes = scopes;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cutoff = DateTime.UtcNow.AddHours(-16);
                var stale = await db.Visits.Where(v => v.CheckOutUtc == null && v.CheckInUtc < cutoff).ToListAsync(ct);
                foreach (var v in stale) v.CheckOutUtc = v.CheckInUtc.AddHours(16);
                if (stale.Count > 0) await db.SaveChangesAsync(ct);

                // Optional privacy rule: Photos:RetentionDays (0 = keep forever)
                var days = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetValue("Photos:RetentionDays", 0);
                if (days > 0)
                {
                    var old = DateTime.UtcNow.AddDays(-days);
                    await db.VisitPhotos.Where(p => p.CreatedUtc < old).ExecuteDeleteAsync(ct);
                    var withToken = await db.Visits.Where(v => v.PhotoToken != null && v.CheckInUtc < old).ToListAsync(ct);
                    foreach (var v in withToken) v.PhotoToken = null;
                    if (withToken.Count > 0) await db.SaveChangesAsync(ct);
                }
            }
            catch { /* ignore, retry next hour */ }
            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }
}
