namespace Diten.CrmService.Application.Features.Commercial.Pricing;

// WP-VW-W6 W6-BE-b — the doors MOD-0156 reads the outside through. Product identity is product-master v1.1 SKU (skuId +
// the level the server resolved; Retired never enters a list or a rule, K-O3); the legal entity is MDM's. Both are read
// through the Gateway with the caller's token, and both FAIL CLOSED: "could not ask" (null / Unavailable) is a 503 with
// nothing written, never "does not exist".

/// <summary>One SKU as product-master v1.1 answers it (<c>bulkReadSkus</c> / <c>searchStockableItems</c> / <c>getSku</c>).</summary>
public sealed record PricingSku(Guid SkuId, string SkuLevel, string Code, string Name, string LifecycleStatus, string? Gtin = null)
{
    public bool IsRetired => string.Equals(LifecycleStatus, "Retired", StringComparison.OrdinalIgnoreCase);
}

public interface IPricingSkuDirectory
{
    /// <summary>These SKUs (≤ 200 per MDM call; the client chunks). Absent ids are not in the answer. Null = MDM could not be
    /// asked.</summary>
    Task<IReadOnlyDictionary<Guid, PricingSku>?> ReadAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken cancellationToken);

    /// <summary>SKUs by their canonical code (LS- / FG- …), exact match, Retired included (so the import can mark them).
    /// Null = MDM could not be asked.</summary>
    Task<IReadOnlyDictionary<string, PricingSku>?> FindByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken);

    /// <summary>Finished-good SKUs by their sales-box GTIN. product-master v1.1 has no GTIN search (v1.2, BL-584 W-04):
    /// an implementation that cannot search answers an EMPTY map — the rows stay unmatched in the preview (§20.3 AÇIK-5).
    /// Null = MDM could not be asked.</summary>
    Task<IReadOnlyDictionary<string, PricingSku>?> FindByGtinsAsync(IReadOnlyCollection<string> gtins, CancellationToken cancellationToken);
}

public sealed record PricingLegalEntity(Guid Id, string Code, string Name, string CountryCode, string BaseCurrencyCode);

public enum PricingLookupOutcome
{
    Found,
    NotFound,
    Unavailable
}

public sealed record PricingLegalEntityLookup(PricingLookupOutcome Outcome, PricingLegalEntity? LegalEntity)
{
    public static readonly PricingLegalEntityLookup NotFound = new(PricingLookupOutcome.NotFound, null);
    public static readonly PricingLegalEntityLookup Unavailable = new(PricingLookupOutcome.Unavailable, null);
}

/// <summary>The tenant's MDM legal entity (country, base currency, name, short code) — or not found / could not ask.</summary>
public interface IPricingLegalEntityDirectory
{
    Task<PricingLegalEntityLookup> GetAsync(Guid legalEntityId, CancellationToken cancellationToken);
}

/// <summary>Which column of the file carries what (the user maps the header row, §4.2 ①). At least the GTIN or the SKU code,
/// at least one price tier, and the VAT column.</summary>
public sealed record PriceListFileColumns(
    string? Gtin, string? SkuCode, string? DepotPrice, string? PharmacistPurchasePrice, string? PublicPrice, string? VatRate);

/// <summary>One data row of the file, as text (the handler parses the numbers — one parser for the file and the manual form).</summary>
public sealed record ParsedPriceRow(int RowNo, string? Gtin, string? SkuCode, string? DepotPrice, string? PharmacistPurchasePrice,
    string? PublicPrice, string? VatRate);

public sealed record PriceListParseResult(bool Ok, string? Error, IReadOnlyList<ParsedPriceRow> Rows)
{
    public static PriceListParseResult Fail(string error) => new(false, error, Array.Empty<ParsedPriceRow>());
}

/// <summary>xlsx / csv → rows (≤ 10,000 data rows; the header row names the columns). The file is never stored.</summary>
public interface IPriceListFileParser
{
    PriceListParseResult Parse(string fileName, byte[] content, PriceListFileColumns columns);
}
