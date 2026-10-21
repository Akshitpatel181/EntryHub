namespace VisitorCheckIn.Models;

/// <summary>Small key/value store (used to keep the auto-generated push keys).</summary>
public class AppSetting
{
    public string Key { get; set; }
    public string Value { get; set; }
}
