using System.Globalization;
using System.Text.RegularExpressions;

namespace Diten.Application.EnterpriseStrategy.Shared;

/// <summary>
/// Allocates sequential runtime identifiers (G-/O-/I-/P-) for enterprise strategy entities.
/// </summary>
public static class EnterpriseStrategyRuntimeIds
{
    private const int DefaultWidth = 6;

    public static string NextGoalId(IEnumerable<string> existingIds) => Next("G-", existingIds, DefaultWidth);

    public static string NextObjectiveId(IEnumerable<string> existingIds) => Next("O-", existingIds, DefaultWidth);

    public static string NextInitiativeId(IEnumerable<string> existingIds) => Next("I-", existingIds, DefaultWidth);

    public static string NextProjectId(IEnumerable<string> existingIds) => Next("P-", existingIds, DefaultWidth);

    public static string Next(string prefix, IEnumerable<string> existingIds, int width)
    {
        var escaped = Regex.Escape(prefix);
        var pattern = new Regex($"^{escaped}(?<n>\\d{{1,12}})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var max = 0;
        foreach (var raw in existingIds)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var m = pattern.Match(raw.Trim());
            if (m.Success && int.TryParse(m.Groups["n"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                max = Math.Max(max, n);
        }

        var next = max + 1;
        return $"{prefix}{next.ToString($"D{width}", CultureInfo.InvariantCulture)}";
    }
}
