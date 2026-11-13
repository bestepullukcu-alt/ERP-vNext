using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// WP-VW-W3E (W3E-CITY, K-CITY a) — the ONE rule that matches a city filter against an account's <see cref="Account.CityRef"/>.
/// Two vocabularies meet here: the account keeps <c>TR-42-KONYA</c> ("&lt;COUNTRY&gt;-&lt;NN&gt;-&lt;NAME&gt;"), the reference-data
/// city set (and so the Web filter) sends <c>konya</c>. The rule accepts both:
/// <list type="bullet">
/// <item>a full reference (<c>TR-42-KONYA</c>) matches exactly, case-insensitive — as before;</item>
/// <item>a reference-data code (<c>konya</c>, <c>İstanbul</c>, <c>sanliurfa</c>) matches the NAME after the
/// "&lt;COUNTRY&gt;-&lt;NN&gt;-" prefix: the input is folded to ASCII lower case (İ/ı/ş/ğ/ü/ö/ç), each folded letter also
/// accepts its Turkish forms in the stored NAME, a hyphen inside NAME is optional (<c>afyonkarahisar</c> ↔
/// <c>AFYON-KARAHISAR</c>, the twin of Web's qr-core cityLabel), never a part of a name (<c>kon</c> ≠ <c>KONYA</c>).</item>
/// </list>
/// <see cref="Pattern"/> is anchored and every input character is escaped, so the Mongo regex built from it cannot be
/// steered by the input; <see cref="Matches"/> runs the very same pattern in memory (the repository default + tests).
/// </summary>
public static class CityRefMatcher
{
    private static readonly Regex FullRef = new("^[A-Za-z]{2}-[0-9]{1,3}-.+$", RegexOptions.CultureInvariant);
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>The prefix of a stored reference before its NAME ("TR-42-").</summary>
    public const string RefPrefixPattern = "[A-Za-z]{2}-[0-9]{1,3}-";

    /// <summary>True when <paramref name="input"/> is a whole "&lt;COUNTRY&gt;-&lt;NN&gt;-&lt;NAME&gt;" reference.</summary>
    public static bool IsFullRef(string input) => FullRef.IsMatch((input ?? string.Empty).Trim());

    /// <summary>Turkish-aware ASCII lower case: "İstanbul" → "istanbul", "ŞANLIURFA" → "sanliurfa".</summary>
    public static string Fold(string input)
    {
        var lower = (input ?? string.Empty).Trim().ToLower(Turkish);
        var sb = new StringBuilder(lower.Length);
        foreach (var ch in lower)
        {
            sb.Append(ch switch { 'ı' => 'i', 'ş' => 's', 'ğ' => 'g', 'ü' => 'u', 'ö' => 'o', 'ç' => 'c', '̇' => '\0', _ => ch });
        }

        return sb.ToString().Replace("\0", string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// The anchored regex (use it case-insensitive) for <paramref name="input"/>: <c>^TR-42-KONYA$</c> for a full
    /// reference, <c>^[A-Za-z]{2}-[0-9]{1,3}-k-?[oOöÖ]-?n-?y-?a$</c> for the code <c>konya</c>. Null for an empty input.
    /// </summary>
    public static string? Pattern(string input)
    {
        var trimmed = (input ?? string.Empty).Trim();
        if (trimmed.Length == 0) return null;
        if (IsFullRef(trimmed)) return "^" + Regex.Escape(trimmed) + "$";

        var letters = Fold(trimmed).Replace("-", string.Empty, StringComparison.Ordinal);
        if (letters.Length == 0) return null;
        var sb = new StringBuilder("^" + RefPrefixPattern);
        for (var i = 0; i < letters.Length; i++)
        {
            if (i > 0) sb.Append("-?");
            sb.Append(Letter(letters[i]));
        }

        return sb.Append('$').ToString();
    }

    /// <summary>The in-memory twin of the Mongo filter: does <paramref name="cityRef"/> match the filter <paramref name="input"/>?</summary>
    public static bool Matches(string input, string? cityRef)
    {
        var pattern = Pattern(input);
        return pattern is not null && cityRef is not null
            && Regex.IsMatch(cityRef.Trim(), pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    }

    private static string Letter(char c) => c switch
    {
        'i' => "[iIıİ]",
        's' => "[sSşŞ]",
        'g' => "[gGğĞ]",
        'u' => "[uUüÜ]",
        'o' => "[oOöÖ]",
        'c' => "[cCçÇ]",
        _ => Regex.Escape(c.ToString())
    };
}
