using System.Text.RegularExpressions;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;

public static partial class ProductAbbreviationNormalizer
{
    /// <summary>
    /// Three ASCII letters, checked BEFORE upper-casing: case mapping turns non-ASCII letters into ASCII ones
    /// ("ı" → I, "ſ" → S), so a grammar applied afterwards would let "abı" through as ABI (Pack:280).
    /// Upper-casing is invariant so the result never depends on the server's culture (tr-TR "i" → "İ").
    /// </summary>
    public static bool TryNormalize(string? value, out string normalized)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (!AsciiLetters().IsMatch(trimmed))
        {
            normalized = string.Empty;
            return false;
        }

        normalized = trimmed.ToUpperInvariant();
        return true;
    }

    [GeneratedRegex("^[A-Za-z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex AsciiLetters();
}
