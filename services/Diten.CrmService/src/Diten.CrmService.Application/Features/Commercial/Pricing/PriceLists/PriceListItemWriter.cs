using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists.Validators;
using Diten.CrmService.Domain.Entities.Commercial;
using Diten.CrmService.Domain.Repositories.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.PriceLists;

/// <summary>
/// WP-VW-W6 W6-BE-b — EK-1: the items of a DRAFT list, added / changed / removed by hand, through the SAME checks an
/// imported row passes (<see cref="PriceListItemValidator"/> + the SKU checks below). Every change goes to the change log.
/// </summary>
public sealed class PriceListItemWriter
{
    private readonly IPriceListItemRepository _items;
    private readonly IPricingSkuDirectory _skus;
    private readonly PricingChangeLogWriter _log;

    public PriceListItemWriter(IPriceListItemRepository items, IPricingSkuDirectory skus, PricingChangeLogWriter log)
    {
        _items = items;
        _skus = skus;
        _log = log;
    }

    /// <summary>Applies the item changes to a draft list; null = done, else the refusal (nothing of the batch written).</summary>
    public async Task<Response<PriceListDto>?> ApplyAsync(PriceList list, IReadOnlyList<PriceListItemInput>? upserts, IReadOnlyList<Guid>? removals,
        string? actor, CancellationToken ct)
    {
        upserts ??= Array.Empty<PriceListItemInput>();
        removals ??= Array.Empty<Guid>();
        if (upserts.Count == 0 && removals.Count == 0)
        {
            return null;
        }

        // 1) shape + prices — the shared item check (EK-1)
        foreach (var input in upserts)
        {
            if (input.ItemId is null && (input.SkuId is null || input.SkuId == Guid.Empty))
            {
                return PricingCommon.Fail<PriceListDto>(PricingErrors.PriceListItemInvalid, "A new item needs its SKU.", 400);
            }

            if (PriceListItemValidator.CheckPrices(input.DepotPrice, input.PharmacistPurchasePrice, input.PublicPrice, input.VatRate) is { } problem)
            {
                return PricingCommon.Fail<PriceListDto>(PricingErrors.PriceListItemInvalid, problem, 400);
            }
        }

        // 2) the existing items the batch touches, and the SKUs it adds
        var existing = (await _items.ListAllAsync(list.TenantId, list.Id, ct)).Where(i => !i.IsDeleted && i.TenantId == list.TenantId).ToList();
        var byId = existing.ToDictionary(i => i.Id);
        foreach (var id in removals.Concat(upserts.Where(u => u.ItemId is not null).Select(u => u.ItemId!.Value)))
        {
            if (!byId.ContainsKey(id))
            {
                return PricingCommon.NotFound<PriceListDto>();
            }
        }

        var newSkus = upserts.Where(u => u.ItemId is null).Select(u => u.SkuId!.Value).ToList();
        if (newSkus.Count != newSkus.Distinct().Count())
        {
            return PricingCommon.Fail<PriceListDto>(PricingErrors.PriceListItemDuplicate, "The same SKU appears twice in the request.", 409);
        }

        var removed = removals.ToHashSet();
        var remainingSkus = existing.Where(i => !removed.Contains(i.Id)).Select(i => i.SkuId).ToHashSet();
        if (newSkus.FirstOrDefault(remainingSkus.Contains) is { } duplicate && duplicate != Guid.Empty)
        {
            return PricingCommon.Fail<PriceListDto>(PricingErrors.PriceListItemDuplicate, $"The SKU {duplicate:D} is already in the list.", 409);
        }

        IReadOnlyDictionary<Guid, PricingSku> skus = new Dictionary<Guid, PricingSku>();
        if (newSkus.Count > 0)
        {
            var read = await _skus.ReadAsync(newSkus, ct);
            if (read is null)
            {
                return PricingCommon.MdmUnavailable<PriceListDto>();
            }

            skus = read;
            foreach (var skuId in newSkus)
            {
                if (!skus.TryGetValue(skuId, out var sku))
                {
                    return PricingCommon.Fail<PriceListDto>(PricingErrors.SkuInvalid, $"The SKU {skuId:D} does not exist.", 400);
                }

                if (sku.IsRetired)
                {
                    return PricingCommon.Fail<PriceListDto>(PricingErrors.SkuRetired, $"The SKU {sku.Code} is Retired and cannot be priced.", 400);
                }
            }
        }

        // 3) write — removals (soft), changes, additions; each with its change-log line
        var entries = new List<PricingChangeLog>();
        foreach (var id in removals)
        {
            var item = byId[id];
            item.IsDeleted = true;
            item.DeletedAt = DateTimeOffset.UtcNow;
            item.UpdatedBy = actor;
            if (!await _items.ReplaceAsync(item, item.Version, ct))
            {
                return PricingCommon.Stale<PriceListDto>();
            }

            entries.Add(_log.Entry(list.TenantId, PricingChangeEntityTypes.PriceListItem, item.Id, item.Version, list.Id,
                PricingChangeActions.ItemRemoved, actor, PricingChangeLogWriter.Diff(("sku", item.SkuCode, null))));
        }

        foreach (var input in upserts.Where(u => u.ItemId is not null))
        {
            var item = byId[input.ItemId!.Value];
            var changes = PricingChangeLogWriter.Diff(
                ("depotPrice", item.DepotPrice, input.DepotPrice), ("pharmacistPurchasePrice", item.PharmacistPurchasePrice, input.PharmacistPurchasePrice),
                ("publicPrice", item.PublicPrice, input.PublicPrice), ("vatRate", item.VatRate, input.VatRate));
            if (changes.Count == 0)
            {
                continue;
            }

            item.DepotPrice = input.DepotPrice;
            item.PharmacistPurchasePrice = input.PharmacistPurchasePrice;
            item.PublicPrice = input.PublicPrice;
            item.VatRate = input.VatRate!.Value;
            item.UpdatedBy = actor;
            item.UpdatedAt = DateTimeOffset.UtcNow;
            if (!await _items.ReplaceAsync(item, item.Version, ct))
            {
                return PricingCommon.Stale<PriceListDto>();
            }

            entries.Add(_log.Entry(list.TenantId, PricingChangeEntityTypes.PriceListItem, item.Id, item.Version, list.Id,
                PricingChangeActions.ItemChanged, actor, changes));
        }

        foreach (var input in upserts.Where(u => u.ItemId is null))
        {
            var sku = skus[input.SkuId!.Value];
            var item = new PriceListItem
            {
                TenantId = list.TenantId, PriceListId = list.Id, SkuId = sku.SkuId, SkuLevel = sku.SkuLevel, SkuCode = sku.Code, SkuName = sku.Name,
                Gtin = sku.Gtin, DepotPrice = input.DepotPrice, PharmacistPurchasePrice = input.PharmacistPurchasePrice, PublicPrice = input.PublicPrice,
                VatRate = input.VatRate!.Value, CreatedBy = actor
            };
            await _items.InsertAsync(item, ct);
            entries.Add(_log.Entry(list.TenantId, PricingChangeEntityTypes.PriceListItem, item.Id, item.Version, list.Id,
                PricingChangeActions.ItemAdded, actor, PricingChangeLogWriter.Created(("sku", sku.Code), ("depotPrice", item.DepotPrice),
                    ("pharmacistPurchasePrice", item.PharmacistPurchasePrice), ("publicPrice", item.PublicPrice), ("vatRate", item.VatRate))));
        }

        await _log.WriteManyAsync(entries, ct);
        return null;
    }
}
