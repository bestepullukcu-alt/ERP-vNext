using System.Text.RegularExpressions;

namespace Diten.InventoryService.Domain.WavePlanning.Rules;

/// <summary>
/// The bin code as LOCATION v1.1's bulk code format (pack §8.7): <c>{aisle}-{rack}-{level}-{position}</c>, the aisle a letter or 1–3 digits.
/// A code in another format has no aisle (the route falls back to code order, DL-2).
/// </summary>
public static partial class WaveLocationRules
{
    /// <summary>The aisle of a bin code, or null when the code is not in the bulk format.</summary>
    public static string? Aisle(string? binCode)
    {
        var match = binCode is null ? Match.Empty : BinCode().Match(binCode);
        return match.Success ? match.Groups["aisle"].Value : null;
    }

    [GeneratedRegex("^(?<aisle>[A-Za-z]|[0-9]{1,3})-(?<rack>[0-9]{2})-(?<level>[0-9]{2})-(?<pos>[0-9]{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex BinCode();
}
