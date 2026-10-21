using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VisitorCheckIn.Data;

namespace VisitorCheckIn.Hubs;

/// <summary>Each logged-in host joins a private group automatically; no client-supplied host id.</summary>
[Authorize(Roles = "Host")]
public class HostHub : Hub
{
    private readonly AppDbContext _db;
    public HostHub(AppDbContext db) => _db = db;

    public override async Task OnConnectedAsync()
    {
        var host = await _db.Hosts.AsNoTracking().FirstOrDefaultAsync(h => h.UserId == Context.UserIdentifier);
        if (host != null) await Groups.AddToGroupAsync(Context.ConnectionId, $"host-{host.Id}");
        await base.OnConnectedAsync();
    }
}
