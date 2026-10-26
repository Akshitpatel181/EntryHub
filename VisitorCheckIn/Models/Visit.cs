
namespace VisitorCheckIn.Models;

public class Visit
{
    public int Id { get; set; }

    public string VisitorName { get; set; } = "";

    public string? VisitorCompany { get; set; }

    public string VisitorType { get; set; } = "";

    public int HostId { get; set; }

    public VisitHost Host { get; set; } = null!;

    public DateTime CheckInUtc { get; set; }

    public DateTime? CheckOutUtc { get; set; }

    /// <summary>
    /// Random, unguessable identifier of the visitor's photo.
    /// Used by /p/{token}.
    /// </summary>
    public string? PhotoToken { get; set; }

    public int? KioskDeviceId { get; set; }

    public KioskDevice? KioskDevice { get; set; }

    /// <summary>
    /// Unique identifier for a check-in submission.
    /// Used to prevent processing the same submission twice.
    /// Null for existing visitor records.
    /// </summary>
    public Guid? SubmissionId { get; set; }

    public List<VisitAnswer> Answers { get; set; } = new();
}

public class VisitAnswer
{
    public int Id { get; set; }

    public int VisitId { get; set; }

    public Visit Visit { get; set; } = null!;

    public int QuestionId { get; set; }

    /// <summary>
    /// Snapshot of the question text at check-in.
    /// </summary>
    public string QuestionText { get; set; } = "";

    public string Answer { get; set; } = "";
}
