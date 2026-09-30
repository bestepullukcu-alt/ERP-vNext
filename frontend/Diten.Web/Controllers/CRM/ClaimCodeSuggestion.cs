using System.Globalization;
using System.Text;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-CL-FE-4 — the claim code suggestion <c>CLM-{PRODUCT NAME}-{NN}</c>. The prefix is the product NAME (not the MDM
/// canonical code, which produced "CLM-GP000000000001-01" live): upper-cased, diacritics folded to ASCII (Ö → O,
/// ı → I), every run of characters that are not A–Z / 0–9 collapsed to a single "-". NN is the next number after the
/// highest existing code with the same prefix (two digits minimum). A suggestion only — the user may edit it, and CRM
/// still refuses a duplicate code.
/// </summary>
public static class ClaimCodeSuggestion
{
    public static string? Prefix(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            return null;
        }

        var folded = new StringBuilder();
        foreach (var ch in productName.Trim().Replace('ı', 'i').Replace('İ', 'I').Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                folded.Append(ch);
            }
        }

        var upper = folded.ToString().ToUpperInvariant();
        var key = new StringBuilder();
        foreach (var ch in upper)
        {
            if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                key.Append(ch);
            }
            else if (key.Length > 0 && key[^1] != '-')
            {
                key.Append('-');
            }
        }

        var body = key.ToString().Trim('-');
        return body.Length == 0 ? null : $"CLM-{body}-";
    }

    /// <summary>The suggested code, or null when the product name yields no usable prefix.</summary>
    public static string? Suggest(string? productName, IEnumerable<string> existingCodes)
    {
        var prefix = Prefix(productName);
        if (prefix is null)
        {
            return null;
        }

        var max = 0;
        foreach (var code in existingCodes)
        {
            var value = code?.Trim().ToUpperInvariant() ?? string.Empty;
            if (value.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(value[prefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                && n > max)
            {
                max = n;
            }
        }

        return prefix + (max + 1).ToString("00", CultureInfo.InvariantCulture);
    }
}
