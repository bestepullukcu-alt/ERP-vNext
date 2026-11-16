using System.Globalization;
using System.Text.RegularExpressions;

namespace Diten.Platform.Domain.Features.RecordCodes;

/// <summary>
/// MOD-0040-FU02 §8.4 (RCS-001 §5.7, §5.10) — <c>{PREFIX}-{TYPE}-{COUNTER}</c>: prefix 2–5 ASCII capitals, type 3 ASCII
/// capitals, counter at least six digits zero-padded and never cut (<c>999999</c> → <c>1000000</c>). No normalisation:
/// a value that does not match exactly is refused, never trimmed or upper-cased. <c>\z</c>, not <c>$</c>, so a trailing
/// newline is not a match.
/// </summary>
public static class RecordCodeFormat
{
    public const int MinimumCounterDigits = 6;

    private static readonly Regex PrefixPattern = new(@"^[A-Z]{2,5}\z", RegexOptions.CultureInvariant);
    private static readonly Regex RecordTypePattern = new(@"^[A-Z]{3}\z", RegexOptions.CultureInvariant);
    private static readonly Regex CodePattern = new(@"^[A-Z]{2,5}-[A-Z]{3}-[0-9]{6,}\z", RegexOptions.CultureInvariant);

    public static bool IsValidPrefix(string? value) => value is not null && PrefixPattern.IsMatch(value);

    public static bool IsValidRecordType(string? value) => value is not null && RecordTypePattern.IsMatch(value);

    public static bool IsValidCode(string? value) => value is not null && CodePattern.IsMatch(value);

    public static string Format(string prefix, string recordType, long sequence)
    {
        if (!IsValidPrefix(prefix))
        {
            throw new ArgumentException("A record-code prefix is 2–5 ASCII capitals.", nameof(prefix));
        }

        if (!IsValidRecordType(recordType))
        {
            throw new ArgumentException("A record type is 3 ASCII capitals.", nameof(recordType));
        }

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "A record-code counter starts at 1.");
        }

        return string.Concat(
            prefix,
            "-",
            recordType,
            "-",
            sequence.ToString("D" + MinimumCounterDigits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
    }
}
