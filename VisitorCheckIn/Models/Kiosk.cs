namespace VisitorCheckIn.Models;

public class KioskDevice
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string TokenHash { get; set; }       // SHA-256 of the cookie token; raw token is never stored
    public string PairMethod { get; set; }      // Code | Link | Bulk | AdminDevice
    public string UserAgent { get; set; }
    public DateTime PairedUtc { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public bool IsRevoked { get; set; }
}

/// <summary>A pending enrolment: gives a 6-digit code AND a one-time link/QR. MaxUses > 1 = bulk/MDM enrolment.</summary>
public class EnrollmentToken
{
    public int Id { get; set; }
    public string DeviceName { get; set; }
    public string Code { get; set; }
    public string LinkTokenHash { get; set; }
    public int MaxUses { get; set; } = 1;
    public int UseCount { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
}
