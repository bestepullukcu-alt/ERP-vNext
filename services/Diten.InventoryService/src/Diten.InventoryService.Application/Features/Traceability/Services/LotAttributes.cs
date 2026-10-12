using Diten.InventoryService.Domain.Traceability.Rules;

namespace Diten.InventoryService.Application.Features.Traceability.Services;

/// <summary>
/// The optional lot attributes of a create / an edit (pack §12 rows 3–9), read from raw request strings: trimmed, an
/// empty string is "no value", lengths per the pack, country an ISO 3166-1 alpha-2 code (upper-cased). <see cref="Error"/>
/// names the first refused member.
/// </summary>
public sealed record LotAttributes(string? ManufacturerLot, string? SupplierLot, string? Country, string? Coa, string? Error)
{
    public static LotAttributes Read(string? manufacturerLot, string? supplierLot, string? country, string? coa)
    {
        var mfr = TraceWriteSupport.Trimmed(manufacturerLot);
        var sup = TraceWriteSupport.Trimmed(supplierLot);
        var cty = TraceWriteSupport.Trimmed(country)?.ToUpperInvariant();
        var coaRef = TraceWriteSupport.Trimmed(coa);

        var error = AttributeError("manufacturerLot", mfr, TraceContract.MaxAttributeLength)
                    ?? AttributeError("supplierLot", sup, TraceContract.MaxAttributeLength)
                    ?? AttributeError("coaReference", coaRef, TraceContract.MaxCoaLength)
                    ?? (cty is not null && !TraceWriteSupport.IsCountryCode(cty) ? "countryOfOrigin must be an ISO 3166-1 alpha-2 code (e.g. TR)." : null);
        return new LotAttributes(mfr, sup, cty, coaRef, error);
    }

    public static string? AttributeError(string name, string? value, int max)
        => value is not null && value.Length > max ? $"{name} must be at most {max} characters." : null;

    /// <summary>Parses the three dates (absent / null → none) and applies the date-order rules (pack §12 rows 5–7).</summary>
    public static LotDates ReadDates(string? manufacturing, string? expiry, string? retest, DateOnly today)
    {
        if (!TryDate("manufacturingDate", manufacturing, out var m, out var error)
            || !TryDate("expiryDate", expiry, out var e, out error)
            || !TryDate("retestDate", retest, out var r, out error))
        {
            return new LotDates(null, null, null, error);
        }

        var ruleError = LotDateRules.Validate(m, e, r, today);
        return new LotDates(
            m is { } md ? TraceMapping.Midnight(md) : null,
            e is { } ed ? TraceMapping.Midnight(ed) : null,
            r is { } rd ? TraceMapping.Midnight(rd) : null,
            ruleError);
    }

    public static bool TryDate(string name, string? raw, out DateOnly? day, out string? error)
    {
        day = null;
        error = null;
        if (raw is null) return true;
        if (TraceWriteSupport.TryParseDate(raw.Trim(), out var parsed))
        {
            day = parsed;
            return true;
        }

        error = $"{name} must be a date: yyyy-MM-dd or yyyy-MM-ddT00:00:00Z.";
        return false;
    }
}

/// <summary>Parsed lot dates (UTC midnights) or the first error.</summary>
public sealed record LotDates(DateTimeOffset? Manufacturing, DateTimeOffset? Expiry, DateTimeOffset? Retest, string? Error);
