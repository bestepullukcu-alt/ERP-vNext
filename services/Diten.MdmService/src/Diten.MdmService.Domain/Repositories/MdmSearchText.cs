using System.Text;
using System.Text.RegularExpressions;

namespace Diten.MdmService.Domain.Repositories;

/// <summary>
/// WP-VW-W4 (W4-FIX-d, G-8) — the ONE "contains" pattern of an MDM list search: the text is upper-cased the way names are
/// stored (<c>GlobalProductNameRules</c>: invariant upper case) and the Turkish dotted / dotless I are folded both ways
/// (<c>konset</c>, <c>KONSET</c>, <c>Kİnset</c> and <c>kınset</c> all find "KİNSET" / "KINSET"); everything else is matched
/// literally (regex-escaped).
/// </summary>
public static class MdmSearchText
{
    /// <summary>A regex (no anchors) matching values that contain <paramref name="search"/>; null for a blank search.</summary>
    public static string? ContainsPattern(string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return null;
        var upper = search.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        var pattern = new StringBuilder();
        foreach (var ch in upper)
        {
            pattern.Append(ch is 'I' or 'İ' ? "[Iİ]" : Regex.Escape(ch.ToString()));
        }

        return pattern.ToString();
    }
}
