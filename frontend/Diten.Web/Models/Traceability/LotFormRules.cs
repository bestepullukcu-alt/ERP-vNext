using System.Globalization;
using System.Text.RegularExpressions;

namespace Diten.Web.Models.Traceability;

/// <summary>The Yeni Lot / Düzenle form as posted by lots/form.js (dates are yyyy-MM-dd, the SKU is <c>Level:skuId</c>).</summary>
public sealed record LotFormInput
{
    public string? IdempotencyKey { get; init; }
    public string? ItemId { get; init; }
    public string? Sku { get; init; }
    public string? LotNumber { get; init; }
    public string? ManufacturerLot { get; init; }
    public string? SupplierLot { get; init; }
    public string? ManufacturingDate { get; init; }
    public string? ExpiryDate { get; init; }
    public string? RetestDate { get; init; }
    public string? CountryOfOrigin { get; init; }
    public string? CoaReference { get; init; }
    public string? Reason { get; init; }
    public string? IfMatch { get; init; }
    /// <summary>Where the lot is created: <c>lot-form</c> (Yeni Lot page) or <c>ledger-shortcut</c> (Stock Ledger quick lot).</summary>
    public string? Origin { get; init; }

    /// <summary>
    /// WP-0176-F5 seam (CT decision A, KF-D): the prepared FEFO-BUNDLE date override of a lot whose expiry / retest differs from the
    /// suggestion — passed to TRACE 1.4.0 createLot (and a Draft updateLot, CT-F7) ONLY when sent; absent = the body is unchanged.
    /// </summary>
    public LotDateOverrideInput? DateOverride { get; init; }
}

/// <summary>TRACE 1.4.0 <c>dateOverride</c>: the override id (FEFO-BUNDLE createDateOverride) and, under ReasonAndSignature, the MOD-0022 signature id.</summary>
public sealed record LotDateOverrideInput
{
    public string? OverrideId { get; init; }

    public string? SignatureId { get; init; }
}

/// <summary>One allowed / disallowed target of the status dialog (pack §11.3 "Durum Değiştir", K-E).</summary>
public sealed record LotStatusOption(string Status, bool Allowed, string? ReasonKey, bool Danger);

/// <summary>
/// MOD-0174 P3 — the pack §12 field rules of the lot form, as the MVC checks them before anything reaches the Gateway
/// (the service repeats them and stays the authority). Field id → resx key of the sentence. K-C: a non-Draft lot is edited
/// only with a reason of ≥ 10 characters (CT-D8: Blocked and Expired too); lot number and SKU are never changed by an edit.
/// </summary>
public static partial class LotFormRules
{
    public const int MinReasonLength = 10;
    public const int MaxLotNumberLength = 40;
    public const int MaxAltLotLength = 40;
    public const int MaxCoaLength = 60;

    /// <summary>ISO 3166-1 alpha-2 codes the form offers (the UI shows the localized name, resx <c>Country.XX</c>).</summary>
    public static readonly IReadOnlyList<string> Countries =
        ["TR", "DE", "FR", "IT", "ES", "NL", "BE", "CH", "AT", "PL", "GB", "IE", "SE", "DK", "FI", "NO", "CZ", "HU", "RO", "GR",
         "PT", "US", "CA", "MX", "BR", "CN", "IN", "JP", "KR", "SG", "IL", "AE", "SA", "EG", "ZA", "RU", "UA", "AU"];

    public static string NormalizeLotNumber(string raw) => raw.Trim().ToUpperInvariant();

    public static bool IsLotNumber(string? normalized) => normalized is not null && LotNumberPattern().IsMatch(normalized);

    /// <summary>The field errors of a create (<paramref name="create"/>) or an edit of a lot in <paramref name="currentStatus"/>.</summary>
    public static IReadOnlyDictionary<string, string> Check(LotFormInput form, bool create, string? currentStatus, DateOnly today)
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        if (create)
        {
            if (!Guid.TryParse(form.ItemId, out _)) errors["itemId"] = "Validation.Required";
            if (!TryParseSku(form.Sku, out _, out _)) errors["sku"] = "Validation.Required";
            if (string.IsNullOrWhiteSpace(form.LotNumber)) errors["lotNumber"] = "Validation.Required";
            else if (!IsLotNumber(NormalizeLotNumber(form.LotNumber))) errors["lotNumber"] = "Validation.LotFormat";
        }

        if (form.ManufacturerLot is { } m && m.Trim().Length > MaxAltLotLength) errors["manufacturerLot"] = "Validation.Length";
        if (form.SupplierLot is { } s && s.Trim().Length > MaxAltLotLength) errors["supplierLot"] = "Validation.Length";
        if (form.CoaReference is { } c && c.Trim().Length > MaxCoaLength) errors["coaReference"] = "Validation.Length";
        if (!string.IsNullOrWhiteSpace(form.CountryOfOrigin) && !Countries.Contains(form.CountryOfOrigin.Trim().ToUpperInvariant(), StringComparer.Ordinal))
            errors["countryOfOrigin"] = "Validation.Required";

        var manufacturing = Date(form.ManufacturingDate, "manufacturingDate", errors);
        var expiry = Date(form.ExpiryDate, "expiryDate", errors);
        var retest = Date(form.RetestDate, "retestDate", errors);
        if (manufacturing is { } md && md > today) errors["manufacturingDate"] = "Validation.DatesOrder";
        if (manufacturing is { } m1 && expiry is { } e1 && e1 <= m1) errors["expiryDate"] = "Validation.DatesOrder";
        if (retest is { } r && ((manufacturing is { } m2 && r < m2) || (expiry is { } e2 && r > e2))) errors["retestDate"] = "Validation.DatesOrder";

        if (!create && !string.Equals(currentStatus, "Draft", StringComparison.Ordinal)
            && (form.Reason?.Trim().Length ?? 0) < MinReasonLength)
            errors["reason"] = "Validation.ReasonMin";
        return errors;
    }

    /// <summary>K-E lifecycle as the dialog shows it: Draft → Released, Released ⇄ Blocked; Expired is the system's.</summary>
    public static IReadOnlyList<LotStatusOption> StatusOptions(string? current) =>
        new[] { "Draft", "Released", "Blocked", "Expired" }
            .Where(s => !string.Equals(s, current, StringComparison.Ordinal))
            .Select(s =>
            {
                if (s == "Expired") return new LotStatusOption(s, false, "Status.Dialog.SystemOnly", false);
                var allowed = (current, s) is ("Draft", "Released") or ("Released", "Blocked") or ("Blocked", "Released");
                return new LotStatusOption(s, allowed, allowed ? null : "Status.Dialog.NotAllowed", s == "Blocked");
            })
            .ToList();

    /// <summary>A date field as the contract wants it (UTC midnight, ISO-8601) — or null when empty.</summary>
    public static string? Wire(string? day)
        => DateOnly.TryParseExact(day?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00:00Z"
            : null;

    /// <summary><c>Level:skuId</c> — the FU01 SKU picker value (Gsku · Lsku · FinishedGood · Item — TRACE 1.3.0, WP-0174-ITEM).</summary>
    public static bool TryParseSku(string? value, out string level, out string skuId)
    {
        level = skuId = string.Empty;
        var parts = (value ?? string.Empty).Split(':');
        if (parts.Length != 2 || parts[0] is not ("Gsku" or "Lsku" or "FinishedGood" or "Item") || !Guid.TryParse(parts[1], out var id)) return false;
        level = parts[0];
        skuId = id.ToString("D");
        return true;
    }

    private static DateOnly? Date(string? value, string field, Dictionary<string, string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) return day;
        errors[field] = "Validation.Required";
        return null;
    }

    [GeneratedRegex("^[A-Z0-9./-]{1,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex LotNumberPattern();
}
