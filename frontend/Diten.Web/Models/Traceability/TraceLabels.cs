using Diten.Web.Models.Inventory;

namespace Diten.Web.Models.Traceability;

/// <summary>
/// The trace pages' door to FU01 <see cref="MasterLabelResolver"/>. That resolver asks the SKU master right after a
/// successful product answer and indexes the SKU list's first entry — with products but NO SKU it throws
/// (<c>ArgumentOutOfRangeException</c>, measured live in WP-0174-P3 E4: Lot Gezgini answered 500 as soon as a lot's
/// product resolved). The resolver is FU01's file and outside P3's paths, so the trace pages always hand it the rows'
/// SKUs, and when no row carries a usable SKU they do not ask for product names at all (raw ids, pack §8).
/// </summary>
public static class TraceLabels
{
    public static Task<MasterLabels> ResolveAsync(
        InventoryGatewayClient ledger,
        IEnumerable<string?> itemIds,
        IEnumerable<(string? SkuId, string? Level)> skus,
        IEnumerable<string?> locationIds,
        CancellationToken cancellationToken)
    {
        var usable = skus.Where(s => !string.IsNullOrWhiteSpace(s.SkuId) && !string.IsNullOrWhiteSpace(s.Level)).ToList();
        return MasterLabelResolver.ResolveAsync(ledger, usable.Count == 0 ? [] : itemIds, usable, locationIds, cancellationToken);
    }
}
