using Diten.CrmService.Application.Features.Approvals;
using Diten.CrmService.Domain.Entities.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists;

// WP-VW-W6 W6-BE-b — MOD-0156 price lists on the wire.

/// <summary>The header of a list (create / edit a draft). The country is never sent: it is the legal entity's (MDM).</summary>
public sealed record PriceListInput(
    string? Name, Guid? LegalEntityId, string? Currency, string? Kind, DateOnly? ValidFrom, DateOnly? ValidTo,
    IReadOnlyList<PriceListItemInput>? Items = null, IReadOnlyList<Guid>? RemoveItemIds = null, int? ExpectedVersion = null);

/// <summary>One item to add (no <see cref="ItemId"/>) or to change (with it) on a DRAFT list — EK-1 manual entry; the SAME
/// checks as an imported row (SKU exists and is not Retired, unique in the list, tiers ≥ 0 with ≤ 4 decimals, at least one
/// tier, VAT 0–100).</summary>
public sealed record PriceListItemInput(
    Guid? ItemId, Guid? SkuId, decimal? DepotPrice, decimal? PharmacistPurchasePrice, decimal? PublicPrice, decimal? VatRate);

public sealed record PriceListVersionInput(DateOnly? ValidFrom, DateOnly? ValidTo, string? Name = null);

public sealed record PriceListDecisionInput(string? Decision, string? Reason, int? ExpectedVersion = null);

public sealed record PriceListSubmitInput(int? ExpectedVersion = null);

public sealed record PriceListDto(
    Guid Id, string Name, Guid LegalEntityId, string? LegalEntityName, string? LegalEntityCode, string CountryCode, string Currency,
    string Kind, DateOnly ValidFrom, DateOnly? ValidTo, DateOnly? EffectiveValidTo, string Status, string? Phase, string Badge,
    int ListVersion, Guid FamilyId, Guid? PreviousPriceListId, Guid? SourceImportId, int ItemCount,
    string? CreatedBy, DateTimeOffset CreatedAt, string? UpdatedBy, DateTimeOffset? UpdatedAt, string? PublishedBy, DateTimeOffset? PublishedAt,
    int Version, IReadOnlyList<ApprovalRoundDto> Rounds, IReadOnlyList<ApprovalDecisionDto> Decisions);

public sealed record PriceListPageDto(IReadOnlyList<PriceListDto> Items, long Total, int Page, int PageSize);

/// <summary>The difference of an item against the list's previous version (<c>?diff=previous</c>).</summary>
public sealed record PriceListItemDiffDto(string Kind, decimal? PreviousDepotPrice, decimal? PreviousPharmacistPurchasePrice,
    decimal? PreviousPublicPrice, decimal? PreviousVatRate);

public sealed record PriceListItemDto(
    Guid Id, Guid SkuId, string? SkuLevel, string? SkuCode, string? SkuName, string? Gtin, decimal? DepotPrice,
    decimal? PharmacistPurchasePrice, decimal? PublicPrice, decimal VatRate, int Version, PriceListItemDiffDto? Diff);

public sealed record PriceListItemPageDto(IReadOnlyList<PriceListItemDto> Items, long Total, int Page, int PageSize,
    IReadOnlyList<PriceListItemDto>? Removed);

public sealed record PriceListExportDto(string FileName, string ContentType, byte[] Content);

public static class PriceListMapping
{
    /// <summary>The screen D badge (§4.1.1): draft · in_review · planned · in-force · past · discarded.</summary>
    public static string Badge(PriceList list, string? phase) => list.Status == PriceListStatuses.Published ? phase ?? PriceListPhases.InForce : list.Status;

    public static PriceListDto ToDto(PriceList list, IReadOnlyCollection<PriceList> sameKeyPublished, DateOnly today)
    {
        var phase = PricingCommon.PhaseOf(list, sameKeyPublished, today);
        return new PriceListDto(list.Id, list.Name, list.LegalEntityId, list.LegalEntityName, list.LegalEntityCode, list.CountryCode,
            list.Currency, list.Kind, list.ValidFrom, list.ValidTo,
            list.IsPublished() ? PricingCommon.EffectiveValidTo(list, sameKeyPublished) : list.ValidTo,
            list.Status, phase, Badge(list, phase), list.ListVersion, list.FamilyId, list.PreviousPriceListId, list.SourceImportId,
            list.ItemCount, list.CreatedBy, list.CreatedAt, list.UpdatedBy, list.UpdatedAt, list.PublishedBy, list.PublishedAt, list.Version,
            ApprovalRoundDto.From(list), ApprovalDecisionDto.From(list));
    }

    public static PriceListItemDto ToDto(PriceListItem item, PriceListItemDiffDto? diff = null)
        => new(item.Id, item.SkuId, item.SkuLevel, item.SkuCode, item.SkuName, item.Gtin, item.DepotPrice, item.PharmacistPurchasePrice,
            item.PublicPrice, item.VatRate, item.Version, diff);

    /// <summary>added / changed / unchanged against the previous version's item of the same SKU.</summary>
    public static PriceListItemDiffDto Diff(PriceListItem item, PriceListItem? previous)
        => previous is null
            ? new PriceListItemDiffDto(PriceListDiffKinds.Added, null, null, null, null)
            : new PriceListItemDiffDto(
                previous.DepotPrice == item.DepotPrice && previous.PharmacistPurchasePrice == item.PharmacistPurchasePrice
                && previous.PublicPrice == item.PublicPrice && previous.VatRate == item.VatRate
                    ? PriceListDiffKinds.Unchanged
                    : PriceListDiffKinds.Changed,
                previous.DepotPrice, previous.PharmacistPurchasePrice, previous.PublicPrice, previous.VatRate);
}
