using Diten.CrmService.Application.Features.Commercial.Pricing.Contracts;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.Evaluation;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 §4.1.2 / §4.6.1: the list in force for (legal entity, country) on a day — one list read
/// and one item read for the whole SKU set (§8). No list in force → <c>quantity</c> mode; a SKU that is not in the list (or
/// has no price in the tier) comes back <c>Found = false</c>.
/// </summary>
public sealed class PriceResolver : IPriceResolver
{
    private readonly IPriceListRepository _lists;
    private readonly IPriceListItemRepository _items;

    public PriceResolver(IPriceListRepository lists, IPriceListItemRepository items)
    {
        _lists = lists;
        _items = items;
    }

    public async Task<PriceResolution> ResolveAsync(Guid tenantId, Guid legalEntityId, string countryCode, DateOnly asOf, string tier,
        IReadOnlyCollection<Guid> skuIds, CancellationToken cancellationToken = default)
    {
        var (list, items) = await ReadAsync(tenantId, legalEntityId, countryCode, asOf, skuIds, cancellationToken);
        var effectiveTier = PriceTiers.All.Contains(tier, StringComparer.Ordinal) ? tier : PriceTiers.PharmacistPurchase;
        if (list is null)
        {
            return new PriceResolution(PricingModes.Quantity, null,
                skuIds.Distinct().Select(id => new ResolvedPriceItem(id, null, null, false)).ToList());
        }

        var bySku = items.GroupBy(i => i.SkuId).ToDictionary(g => g.Key, g => g.First());
        return new PriceResolution(PricingModes.Priced, Ref(list),
            skuIds.Distinct().Select(id => bySku.TryGetValue(id, out var item) && item.PriceOf(effectiveTier) is { } price
                ? new ResolvedPriceItem(id, price, item.VatRate, true)
                : new ResolvedPriceItem(id, null, null, false)).ToList());
    }

    /// <summary>The list in force and its items of these SKUs (the evaluator shares the read).</summary>
    internal async Task<(PriceList? List, IReadOnlyList<PriceListItem> Items)> ReadAsync(Guid tenantId, Guid legalEntityId,
        string countryCode, DateOnly asOf, IReadOnlyCollection<Guid> skuIds, CancellationToken cancellationToken)
    {
        var published = await _lists.ListPublishedAsync(tenantId, legalEntityId, (countryCode ?? string.Empty).Trim().ToUpperInvariant(),
            cancellationToken);
        var list = PricingCommon.InForceOn(published.Where(l => l.TenantId == tenantId), asOf);
        if (list is null)
        {
            return (null, Array.Empty<PriceListItem>());
        }

        var items = skuIds.Count == 0
            ? Array.Empty<PriceListItem>()
            : await _items.ListBySkusAsync(tenantId, list.Id, skuIds.Distinct().ToList(), cancellationToken);
        return (list, items.Where(i => i.TenantId == tenantId && !i.IsDeleted).ToList());
    }

    public static PriceListRef Ref(PriceList list) => new(list.Id, list.ListVersion, list.Name, list.Kind, list.Currency, list.ValidFrom);
}
