using Diten.CrmService.Domain.Entities.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PriceListImports;

// WP-VW-W6 W6-BE-b — MOD-0156 §4.2 the import staging area on the wire.

/// <summary>The upload (multipart on the API): the file + the list header + the column mapping.</summary>
public sealed record PriceListImportUpload(
    string? Name, Guid? LegalEntityId, string? Currency, string? Kind, DateOnly? ValidFrom, DateOnly? ValidTo,
    string? FileName, byte[]? Content, PriceListFileColumns? Columns);

public sealed record PriceListImportDto(
    Guid Id, string Name, Guid LegalEntityId, string? LegalEntityName, string? LegalEntityCode, string CountryCode, string Currency,
    string Kind, DateOnly ValidFrom, DateOnly? ValidTo, string FileName, string FileSha256, string Status,
    int TotalRows, int Matched, int Unmatched, int Retired, int Duplicate, int InvalidPrice,
    Guid? ComparedPriceListId, int Added, int Removed, int Changed, int VatChanged, Guid? ConvertedPriceListId,
    string? CreatedBy, DateTimeOffset CreatedAt, int Version);

public sealed record PriceListImportRowDto(
    Guid Id, int RowNo, string? SourceCode, string? SourceGtin, Guid? SkuId, string? SkuLevel, string? SkuCode, string? SkuName,
    decimal? DepotPrice, decimal? PharmacistPurchasePrice, decimal? PublicPrice, decimal? VatRate, string RowStatus, string? DiffKind,
    decimal? PreviousDepotPrice, decimal? PreviousPharmacistPurchasePrice, decimal? PreviousPublicPrice, decimal? PreviousVatRate);

public sealed record PriceListImportPreviewDto(PriceListImportDto Import, IReadOnlyList<PriceListImportRowDto> Rows, long Total, int Page, int PageSize);

public static class PriceListImportMapping
{
    public static PriceListImportDto ToDto(PriceListImport i)
        => new(i.Id, i.Name, i.LegalEntityId, i.LegalEntityName, i.LegalEntityCode, i.CountryCode, i.Currency, i.Kind, i.ValidFrom, i.ValidTo,
            i.FileName, i.FileSha256, i.Status, i.TotalRows, i.Matched, i.Unmatched, i.Retired, i.Duplicate, i.InvalidPrice,
            i.ComparedPriceListId, i.Added, i.Removed, i.Changed, i.VatChanged, i.ConvertedPriceListId, i.CreatedBy, i.CreatedAt, i.Version);

    public static PriceListImportRowDto ToDto(PriceListImportRow r)
        => new(r.Id, r.RowNo, r.SourceCode, r.SourceGtin, r.SkuId, r.SkuLevel, r.SkuCode, r.SkuName, r.DepotPrice, r.PharmacistPurchasePrice,
            r.PublicPrice, r.VatRate, r.RowStatus, r.DiffKind, r.PreviousDepotPrice, r.PreviousPharmacistPurchasePrice, r.PreviousPublicPrice,
            r.PreviousVatRate);
}
