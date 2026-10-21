using System.ComponentModel.DataAnnotations;

namespace VisitorCheckIn.Models;

public enum QuestionType { Text, YesNo, Dropdown, Checkbox, Phone, Email, Signature }

public static class VisitorTypes
{
    public static readonly string[] All = { "Visitor", "Contractor", "Interview", "Delivery" };
}

public class Question
{
    public int Id { get; set; }
    [Required, StringLength(300)] public string Text { get; set; }
    public QuestionType Type { get; set; }
    /// <summary>One option per line (Dropdown / Checkbox)</summary>
    public string OptionsText { get; set; }
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
    /// <summary>"All" or one of VisitorTypes.All</summary>
    public string VisitorType { get; set; } = "All";
    public bool IsActive { get; set; } = true;
}
