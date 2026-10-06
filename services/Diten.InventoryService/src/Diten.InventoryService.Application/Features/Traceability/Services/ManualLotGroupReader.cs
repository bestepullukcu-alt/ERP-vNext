using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Domain.Traceability.Repositories;

namespace Diten.InventoryService.Application.Features.Traceability.Services;

/// <summary>The migratable typed groups and what was left out (CT K4-4).</summary>
/// <param name="Groups">Groups of LOT-CONTROLLED items, in matcher order.</param>
/// <param name="ExcludedItemIds">Items whose typed texts stay as they are: not lot-controlled, or unknown to PRODUCT-MASTER.</param>
/// <param name="Products">PRODUCT-MASTER answer of each included item (tracking mode of a new lot).</param>
public sealed record ManualLotGroupSnapshot(
    IReadOnlyList<TypedLotGroupStock> Groups,
    IReadOnlySet<Guid> ExcludedItemIds,
    IReadOnlyDictionary<Guid, ProductReference> Products);

/// <summary>
/// WP-0174-P4 — the typed groups the migration works on (listManualLotGroups and the commit read the SAME set): every typed
/// group of the tenant whose item PRODUCT-MASTER calls lot-controlled. A typed text on an item that is not lot-controlled
/// is not a lot (pack §12 <c>LOT_NOT_ALLOWED</c>): it gets no group, no link, no re-key (CT K4-4). PRODUCT-MASTER
/// unreachable → <see cref="ProductMasterUnavailableException"/> (the contract's 503), nothing read further.
/// </summary>
public sealed class ManualLotGroupReader
{
    private readonly IManualLotMigrationRepository _store;
    private readonly IProductMasterClient _products;

    public ManualLotGroupReader(IManualLotMigrationRepository store, IProductMasterClient products)
    {
        _store = store;
        _products = products;
    }

    public async Task<ManualLotGroupSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var all = await _store.ListTypedGroupsAsync(cancellationToken);
        var products = new Dictionary<Guid, ProductReference>();
        var excluded = new HashSet<Guid>();
        foreach (var itemId in all.Select(g => g.Group.ItemId).Distinct())
        {
            var product = await _products.GetProductAsync(itemId, cancellationToken);
            if (product is { LotControlled: true })
            {
                products[itemId] = product;
            }
            else
            {
                excluded.Add(itemId);
            }
        }

        return new ManualLotGroupSnapshot(all.Where(g => products.ContainsKey(g.Group.ItemId)).ToList(), excluded, products);
    }
}
