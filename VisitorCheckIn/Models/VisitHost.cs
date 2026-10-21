namespace VisitorCheckIn.Models;

/// <summary>The employee a visitor comes to see. (Named VisitHost to avoid clashing with Microsoft.Extensions.Hosting.Host)</summary>
public class VisitHost
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string UserId { get; set; }          // Identity login for the Host Portal
    public bool IsActive { get; set; } = true;
    public List<HostDevice> Devices { get; set; } = new();
}

/// <summary>Every phone/laptop/browser a host enabled push notifications on.</summary>
public class HostDevice
{
    public int Id { get; set; }
    public int HostId { get; set; }
    public VisitHost Host { get; set; }
    public string Endpoint { get; set; }
    public string P256dh { get; set; }
    public string Auth { get; set; }
    public string Label { get; set; }           // e.g. "iPhone · Safari"
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
