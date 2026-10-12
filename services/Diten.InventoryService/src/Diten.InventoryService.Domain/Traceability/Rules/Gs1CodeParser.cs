using System.Globalization;
using System.Text.RegularExpressions;

namespace Diten.InventoryService.Domain.Traceability.Rules;

/// <summary>
/// Reads a scanned GS1 element string (K-B, pack proposal §4.6) in either form:
/// <list type="bullet">
/// <item>human-readable, bracketed: <c>(01)08699512340407(21)SN0001(17)280630(10)DKS-26P001</c>;</item>
/// <item>raw, as a scanner sends it: optional symbology identifier (<c>]d2</c>, <c>]C1</c>, <c>]Q3</c>), FNC1 as the GS
/// character (U+001D) after every variable-length field.</item>
/// </list>
/// (17) is <c>YYMMDD</c>; <c>DD = 00</c> means the last day of the month and the century follows the GS1 General
/// Specifications rule (a year more than 50 years ahead belongs to the previous century, more than 49 behind to the
/// next). The parser only reads; whether the values match the selected SKU / lot is the caller's check.
/// Unreadable = no AI found, a duplicate AI, a fixed-length field of the wrong length, or an AI it cannot size in raw form.
/// </summary>
public static partial class Gs1CodeParser
{
    private const char GroupSeparator = '\u001D';

    private static readonly IReadOnlyDictionary<string, int> FixedLength = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["00"] = 18, ["01"] = 14, ["02"] = 14, ["11"] = 6, ["12"] = 6, ["13"] = 6, ["15"] = 6, ["16"] = 6, ["17"] = 6, ["20"] = 2
    };

    private static readonly HashSet<string> VariableLength = new(StringComparer.Ordinal) { "10", "21", "22" };

    public static bool TryParse(string? raw, DateOnly today, out Gs1Code code)
    {
        code = new Gs1Code(null, null, null, null, null);
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var text = raw.Trim();
        var fields = text.Contains('(') ? ReadBracketed(text) : ReadRaw(text);
        if (fields is null || fields.Count == 0) return false;

        if (fields.TryGetValue("01", out var gtin) && !Digits().IsMatch(gtin)) return false;
        if (fields.TryGetValue("17", out var expiryRaw) && !Digits().IsMatch(expiryRaw)) return false;
        if (!fields.ContainsKey("01") && !fields.ContainsKey("21") && !fields.ContainsKey("10") && !fields.ContainsKey("17")) return false;

        code = new Gs1Code(
            fields.GetValueOrDefault("01"),
            fields.GetValueOrDefault("21"),
            expiryRaw,
            expiryRaw is null ? null : ExpiryDate(expiryRaw, today),
            fields.GetValueOrDefault("10"));
        return true;
    }

    /// <summary>A GTIN-8/12/13/14 written as 14 digits (left-padded with zeros) so two spellings compare equal.</summary>
    public static string? Gtin14(string? gtin)
    {
        var digits = gtin?.Trim();
        return digits is null || digits.Length is not (8 or 12 or 13 or 14) || !Digits().IsMatch(digits) ? null : digits.PadLeft(14, '0');
    }

    /// <summary>YYMMDD → date (GS1 century rule; DD 00 = last day of the month); null when it is not a real date.</summary>
    public static DateOnly? ExpiryDate(string yymmdd, DateOnly today)
    {
        if (yymmdd.Length != 6
            || !int.TryParse(yymmdd.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var yy)
            || !int.TryParse(yymmdd.AsSpan(2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || !int.TryParse(yymmdd.AsSpan(4, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var day)
            || month is < 1 or > 12)
        {
            return null;
        }

        var currentYy = today.Year % 100;
        var century = today.Year / 100;
        var difference = yy - currentYy;
        if (difference is >= 51 and <= 99) century -= 1;
        else if (difference is >= -99 and <= -50) century += 1;
        var year = century * 100 + yy;

        var lastDay = DateTime.DaysInMonth(year, month);
        if (day == 0) day = lastDay;
        return day > lastDay ? null : new DateOnly(year, month, day);
    }

    private static Dictionary<string, string>? ReadBracketed(string text)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var position = 0;
        foreach (Match match in Bracketed().Matches(text))
        {
            if (match.Index != position) return null; // text between fields: not a GS1 element string
            var ai = match.Groups["ai"].Value;
            var value = match.Groups["value"].Value.Trim();
            if (value.Length == 0 || !fields.TryAdd(ai, value)) return null;
            if (FixedLength.TryGetValue(ai, out var length) && value.Length != length) return null;
            position = match.Index + match.Length;
        }

        return position == text.Length ? fields : null;
    }

    private static Dictionary<string, string>? ReadRaw(string text)
    {
        foreach (var prefix in new[] { "]d2", "]C1", "]Q3", "]e0" })
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                text = text[prefix.Length..];
                break;
            }
        }

        text = text.TrimStart(GroupSeparator);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var i = 0;
        while (i < text.Length)
        {
            if (i + 2 > text.Length) return null;
            var ai = text.Substring(i, 2);
            i += 2;
            string value;
            if (FixedLength.TryGetValue(ai, out var length))
            {
                if (i + length > text.Length) return null;
                value = text.Substring(i, length);
                i += length;
                if (i < text.Length && text[i] == GroupSeparator) i++;
            }
            else if (VariableLength.Contains(ai))
            {
                var end = text.IndexOf(GroupSeparator, i);
                value = end < 0 ? text[i..] : text[i..end];
                i = end < 0 ? text.Length : end + 1;
                if (value.Length is 0 or > 20) return null;
            }
            else
            {
                return null;
            }

            if (!fields.TryAdd(ai, value)) return null;
        }

        return fields;
    }

    [GeneratedRegex(@"\((?<ai>\d{2,4})\)(?<value>[^()]*)", RegexOptions.CultureInvariant)]
    private static partial Regex Bracketed();

    [GeneratedRegex("^[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex Digits();
}
