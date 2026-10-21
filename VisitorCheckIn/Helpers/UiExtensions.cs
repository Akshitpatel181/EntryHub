using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace VisitorCheckIn.Helpers;

/// <summary>Small UI helpers used by the Razor views (icons, avatars).</summary>
public static class UiExtensions
{
    /// <summary>Inline SVG icon from /wwwroot/img/icons.svg. <c>name</c> is always a literal from the views.</summary>
    public static IHtmlContent Icon(this IHtmlHelper html, string name, string cssClass = null) =>
        new HtmlString($"<svg class=\"i{(string.IsNullOrEmpty(cssClass) ? "" : " " + cssClass)}\" aria-hidden=\"true\"><use href=\"/img/icons.svg#{name}\"/></svg>");

    /// <summary>"Jane van Doe" -> "JD". Falls back to "?".</summary>
    public static string Initials(this string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(new[] { ' ', '@', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0].Substring(0, 1).ToUpperInvariant();
        return (parts[0].Substring(0, 1) + parts[^1].Substring(0, 1)).ToUpperInvariant();
    }

    /// <summary>Stable avatar colour class (c0..c4) from a name.</summary>
    public static string AvatarClass(this string name)
    {
        if (string.IsNullOrEmpty(name)) return "c0";
        var sum = 0;
        foreach (var ch in name) sum += ch;
        return "c" + (sum % 5);
    }
}
