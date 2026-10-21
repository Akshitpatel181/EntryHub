namespace VisitorCheckIn.Models;

public class Visit
{
    public int Id { get; set; }
    public string VisitorName { get; set; }
    public string VisitorCompany { get; set; }
    public string VisitorType { get; set; }
    public int HostId { get; set; }
    public VisitHost Host { get; set; }
    public DateTime CheckInUtc { get; set; }
    public DateTime? CheckOutUtc { get; set; }
    /// <summary>Random, unguessable id of the visitor's photo (served at /p/{token}). Null if no photo.</summary>
    public string PhotoToken { get; set; }
    public int? KioskDeviceId { get; set; }
    public KioskDevice KioskDevice { get; set; }
    public List<VisitAnswer> Answers { get; set; } = new();
}

public class VisitAnswer
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public Visit Visit { get; set; }
    public int QuestionId { get; set; }
    public string QuestionText { get; set; }    // snapshot, so edits later don't rewrite history
    public string Answer { get; set; }
}
