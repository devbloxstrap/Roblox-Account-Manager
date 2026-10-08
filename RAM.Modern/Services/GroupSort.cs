using System.Text.RegularExpressions;
using RAM.Modern.Models;

namespace RAM.Modern.Services;

/// <summary>Optional numeric group prefix: "001 Main", "020 Storage".
/// Sorting is numeric; the prefix is hidden on list cards.</summary>
public static partial class GroupSort
{
    [GeneratedRegex(@"^\s*(\d{1,3})[.\s_-]+(.+)$")]
    private static partial Regex GroupPrefix();

    public static (int Priority, string DisplayName) Parse(string? value)
    {
        string text = string.IsNullOrWhiteSpace(value) ? "General" : value.Trim();
        var match = GroupPrefix().Match(text);
        if (!match.Success) return (500, text);
        return (Math.Min(999, int.Parse(match.Groups[1].Value)), match.Groups[2].Value.Trim());
    }

    public static IOrderedEnumerable<AccountProfile> Sort(IEnumerable<AccountProfile> profiles) =>
        profiles.OrderBy(p => Parse(p.Group).Priority)
                .ThenBy(p => Parse(p.Group).DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(p => p.Favorite)
                .ThenBy(p => p.SortOrder)
                .ThenBy(p => p.Username, StringComparer.OrdinalIgnoreCase);
}
