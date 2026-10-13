using System.Globalization;
using System.Text.RegularExpressions;

namespace Diten.Web.Models.Traceability;

/// <summary>One option of the Stock Ledger lot picker (pack §11.4): "lot no · SKT · status", disabled with its reason.</summary>
public sealed record LotPickerOption(
    string Value,
    string LotNumber,
    string? ExpiryDate,
    string Status,
    bool Disabled,
    string? DisabledReasonKey,
    bool RetestDue,
    string? RetestDate);

/// <summary>A scanned GS1 / ITS code, answered for the serial panel (K-B).</summary>
public sealed record Gs1Answer(
    bool Accepted,
    string? SerialNumber,
    bool GtinSkipped,
    string? WarningKey,
    IReadOnlyList<string> MismatchKeys);

/// <summary>
/// MOD-0174 P3 — the Stock Ledger lot field (pack §11.4, K-B, K-F, CT-D4) as pure functions, so every rule the form shows is
/// measured directly. The service stays the authority (pack §12 ledger rules, P2): a picker that let a Blocked lot through
/// would still be refused with <c>LOT_BLOCKED</c> — these rules keep the screen honest, they do not replace the server.
/// </summary>
public static partial class LotFieldRules
{
    public const int MaxRange = 5000;
    public const int MinSerialLength = 3;
    public const int MaxSerialLength = 24;

    public const string GtinNullReason = "PRODUCT_MASTER_GTIN_NULL";

    /// <summary>
    /// The lots of the selected SKU as picker options, sorted by SKT (none last), then lot number. Blocked and Expired lots
    /// — by status, or by an expiry date before <paramref name="today"/> — are shown but DISABLED with the reason
    /// (CT-D4: an inbound movement into them is refused; disposal of their stock goes through the source row).
    /// </summary>
    public static IReadOnlyList<LotPickerOption> PickerOptions(IEnumerable<LotRefDto> lots, DateOnly today)
        => lots
            .Where(l => !string.IsNullOrWhiteSpace(l.LotId) && !string.IsNullOrWhiteSpace(l.LotNumber))
            .Select(l =>
            {
                var expiry = Day(l.ExpiryDate);
                var status = l.Status ?? string.Empty;
                string? reason = status switch
                {
                    "Blocked" => "Lot.BlockedDisabled",
                    "Expired" => "Lot.ExpiredDisabled",
                    _ => expiry is { } e && e < today ? "Lot.ExpiredDisabled" : null
                };
                var retest = Day(l.RetestDate);
                return new LotPickerOption(l.LotId!, l.LotNumber!, l.ExpiryDate, status, reason is not null, reason,
                    l.RetestDue == true || (retest is { } r && r < today), l.RetestDate);
            })
            .OrderBy(o => Day(o.ExpiryDate) ?? DateOnly.MaxValue)
            .ThenBy(o => o.LotNumber, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// K-B: a GS1 check answer for the serial panel. The code is added only when the service accepted it; every field that
    /// did not match is named. When PRODUCT-MASTER has no GTIN for the SKU (MOD-0290 D-1) the GTIN check is skipped — and
    /// the warning is ALWAYS visible; the lot and the SKT are checked regardless.
    /// </summary>
    public static Gs1Answer Gs1(Gs1CheckResultDto result)
    {
        var mismatches = (result.Checks ?? [])
            .Where(c => c.Result is "Mismatch" or "Missing")
            .Select(c => c.Field switch
            {
                "Gtin" => "Serial.Mismatch.Gtin",
                "Lot" => "Serial.Mismatch.Lot",
                "Expiry" => "Serial.Mismatch.Expiry",
                _ => "Serial.Unreadable"
            })
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var gtinSkipped = string.Equals(result.GtinSkippedReason, GtinNullReason, StringComparison.Ordinal)
                          || (result.Checks ?? []).Any(c => c.Field == "Gtin" && c.Result == "Skipped");
        var accepted = result.Accepted == true && mismatches.Count == 0 && !string.IsNullOrWhiteSpace(result.SerialNumber);
        return new Gs1Answer(accepted, accepted ? NormalizeSerial(result.SerialNumber!) : null, gtinSkipped,
            gtinSkipped ? "Serial.GtinSkippedWarning" : null, mismatches);
    }

    public static string NormalizeSerial(string raw) => raw.Trim().ToUpperInvariant();

    /// <summary>Serial shape of the approved mockup (pack §12 UI rules): 3–24 of A–Z 0–9 -.</summary>
    public static bool IsSerial(string? normalized) => normalized is not null && SerialPattern().IsMatch(normalized);

    /// <summary>
    /// K-B range: <paramref name="start"/> … <paramref name="end"/> share their prefix and the digit count of the trailing
    /// number, end ≥ start, at most <see cref="MaxRange"/> serials, each a valid serial. Error = the resx key of the reason.
    /// </summary>
    public static (IReadOnlyList<string> Serials, string? ErrorKey) ExpandRange(string? start, string? end)
    {
        var a = NormalizeSerial(start ?? string.Empty);
        var b = NormalizeSerial(end ?? string.Empty);
        if (!IsSerial(a) || !IsSerial(b)) return ([], "Serial.BadFormat");

        var ma = TrailingNumber().Match(a);
        var mb = TrailingNumber().Match(b);
        if (!ma.Success || !mb.Success) return ([], "Serial.RangePrefix");
        var prefixA = a[..ma.Index];
        var prefixB = b[..mb.Index];
        if (!string.Equals(prefixA, prefixB, StringComparison.Ordinal) || ma.Value.Length != mb.Value.Length) return ([], "Serial.RangePrefix");

        if (!long.TryParse(ma.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var from)
            || !long.TryParse(mb.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var to))
            return ([], "Serial.RangePrefix");
        if (to < from) return ([], "Serial.RangeOrder");
        if (to - from + 1 > MaxRange) return ([], "Serial.RangeMax");

        var width = ma.Value.Length;
        var serials = new List<string>((int)(to - from + 1));
        for (var n = from; n <= to; n++)
            serials.Add(prefixA + n.ToString(CultureInfo.InvariantCulture).PadLeft(width, '0'));
        return (serials, null);
    }

    private static DateOnly? Day(string? wire)
        => DateTimeOffset.TryParse(wire, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var value)
            ? DateOnly.FromDateTime(value.UtcDateTime)
            : null;

    [GeneratedRegex("^[A-Z0-9-]{3,24}$", RegexOptions.CultureInvariant)]
    private static partial Regex SerialPattern();

    [GeneratedRegex("[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingNumber();
}
