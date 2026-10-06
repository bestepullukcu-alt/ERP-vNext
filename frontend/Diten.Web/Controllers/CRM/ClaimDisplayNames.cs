using System.Globalization;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-CL-FE-2 — display names of countries and languages for the claim screens, from ICU (never a compiled list).
/// <para><c>RegionInfo.DisplayName</c> is NOT localized in .NET (it returns the region's native name whatever the UI
/// culture), so the country name in the UI culture is read from the region part of a culture display name:
/// <c>new CultureInfo("en-TR").DisplayName</c> is "İngilizce (Türkiye)" under tr, "anglais (Turquie)" under fr. The
/// native country name uses the country's first content language the same way ("o‘zbek (Oʻzbekiston)"). A code ICU
/// does not know stays the code (the region part then equals the code) — never a guess.</para>
/// </summary>
internal static class ClaimDisplayNames
{
    /// <summary>The country name in the current UI culture; null when ICU does not know the code.</summary>
    public static string? CountryName(string code) => RegionPart(SafeCulture("en-" + code)?.DisplayName, code);

    /// <summary>The country name in its own language (first content language), else ICU's region native name.</summary>
    public static string? CountryNativeName(string code, string? firstLanguage)
    {
        if (!string.IsNullOrWhiteSpace(firstLanguage)
            && RegionPart(SafeCulture($"{firstLanguage}-{code}")?.NativeName, code) is { } native)
        {
            return native;
        }

        try
        {
            var region = new RegionInfo(code);
            return string.Equals(region.NativeName, code, StringComparison.OrdinalIgnoreCase) ? null : region.NativeName;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The language name in the current UI culture (first letter upper-cased); null when unknown.</summary>
    public static string? LanguageName(string code)
    {
        var culture = SafeCulture(code);
        return culture is null ? null : Capitalize(Known(culture.DisplayName, code), CultureInfo.CurrentUICulture);
    }

    /// <summary>The language name in itself ("русский" → "Русский", "o‘zbek" → "O‘zbek"); null when unknown.</summary>
    public static string? LanguageNativeName(string code)
    {
        var culture = SafeCulture(code);
        return culture is null ? null : Capitalize(Known(culture.NativeName, code), culture);
    }

    private static CultureInfo? SafeCulture(string name)
    {
        try
        {
            return new CultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    /// <summary>ICU echoes an unknown code back ("xx", "English (QQ)"): that is "unknown", not a name.</summary>
    private static string? Known(string? name, string code) =>
        string.IsNullOrWhiteSpace(name) || string.Equals(name.Trim(), code.Trim(), StringComparison.OrdinalIgnoreCase)
            ? null
            : name.Trim();

    /// <summary>The last parenthesized part of "Language (Region)" — ASCII or full-width brackets (zh).</summary>
    private static string? RegionPart(string? displayName, string code)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var close = Math.Max(displayName.LastIndexOf(')'), displayName.LastIndexOf('）'));
        var open = Math.Max(displayName.LastIndexOf('(', Math.Max(close, 0)), displayName.LastIndexOf('（', Math.Max(close, 0)));
        if (close <= 0 || open < 0 || open >= close)
        {
            return null;
        }

        return Known(displayName.Substring(open + 1, close - open - 1), code);
    }

    private static string? Capitalize(string? value, CultureInfo culture) =>
        string.IsNullOrEmpty(value) ? value : culture.TextInfo.ToUpper(value[0]) + value[1..];
}
