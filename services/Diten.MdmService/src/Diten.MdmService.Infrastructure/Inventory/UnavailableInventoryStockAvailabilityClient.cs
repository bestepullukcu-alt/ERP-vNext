using Diten.MdmService.Application.Contracts.Inventory;

namespace Diten.MdmService.Infrastructure.Inventory;

// MOD-0290-FU04 §8.3 / O-11: INVENTORY-BUNDLE v1 `/availability` is scoped to ONE legal entity; the tenant-wide answer
// MDM needs (every legal entity, every stock status, with MDM's service identity) has been asked of the inventory team
// and does not exist yet. Until it does, retirement is refused (503 ITEM_RETIRE_STOCK_CHECK_UNAVAILABLE) — the safe
// side. The HTTP client replaces this class when the endpoint and its contract arrive.
public sealed class UnavailableInventoryStockAvailabilityClient : IInventoryStockAvailabilityClient
{
    public const string TenantWideQueryMissing = "INVENTORY_TENANT_WIDE_QUERY_NOT_AVAILABLE";

    public Task<InventoryStockCheck> CheckNonZeroStockAsync(Guid skuId, string skuLevel,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(InventoryStockCheck.Unavailable(TenantWideQueryMissing));
}
