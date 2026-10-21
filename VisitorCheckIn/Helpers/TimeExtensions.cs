namespace VisitorCheckIn.Helpers;

public static class TimeExtensions
{
    public static string ToIso(this DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("o");
    public static string ToIso(this DateTime? d) => d.HasValue ? d.Value.ToIso() : "";
}
