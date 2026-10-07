using System.Text;
using System.Text.RegularExpressions;

namespace Diten.CrmService.Application.Common;

/// <summary>
/// WP-VP-FIX-2 (F-1) — a "contains" search pattern that is Turkish-insensitive. Mongo's <c>i</c> option folds ş/Ş, ğ/Ğ,
/// ü/Ü, ö/Ö, ç/Ç, but NOT the four Turkish i letters: <c>İ</c> (U+0130) folds to "i̇", not "i", and <c>ı</c> (U+0131)
/// has no ASCII partner. So "Hamidiye" never found "HAMİDİYE". This helper:
/// <list type="number">
/// <item>escapes the user's term (<see cref="Regex.Escape"/>) — a search is literal text, never a regex, so "a.b(" is safe;</item>
/// <item>turns every i / ı / I / İ into the class <c>[iıIİ]</c>.</item>
/// </list>
/// Use it with the <c>i</c> option (it does the rest). The pattern is unanchored, i.e. "contains", as before.
/// </summary>
public static class TurkishInsensitivePattern
{
    /// <summary>The character class every Turkish / Latin i letter becomes.</summary>
    public const string ILetterClass = "[iıIİ]";

    public static string Build(string term)
    {
        var escaped = Regex.Escape(term ?? string.Empty);
        var sb = new StringBuilder(escaped.Length + 16);
        foreach (var ch in escaped)
        {
            sb.Append(ch is 'i' or 'ı' or 'I' or 'İ' ? ILetterClass : ch.ToString());
        }

        return sb.ToString();
    }
}
