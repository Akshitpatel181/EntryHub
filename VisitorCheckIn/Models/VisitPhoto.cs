namespace VisitorCheckIn.Models;

/// <summary>The photo taken at the kiosk. Bytes live in their own table so visit lists never load images.</summary>
public class VisitPhoto
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public Visit Visit { get; set; }
    public string Token { get; set; }
    public byte[] Data { get; set; }
    public string ContentType { get; set; } = "image/jpeg";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
