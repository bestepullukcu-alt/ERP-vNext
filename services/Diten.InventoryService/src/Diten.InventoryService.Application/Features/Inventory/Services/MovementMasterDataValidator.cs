using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Domain.Rules;
using Microsoft.Extensions.Options;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>
/// Pack §12 rules that need the frozen upstream masters (PRODUCT-MASTER, LOCATION), in this order:
/// item / sku existence (<c>UNKNOWN_ITEM</c>) → D-SKU-LINK (<c>SKU_ITEM_MISMATCH</c>) → base-UoM conversion
/// (<c>UOM_CONVERSION_MISSING</c>) → lot (<c>LOT_REQUIRED</c>) → serial count (<c>SERIAL_COUNT_MISMATCH</c>) →
/// each named location exists, is Active and stockable, belongs to the caller's legal entity and sits under the
/// named warehouse (<c>UNKNOWN_LOCATION</c>).
/// Upstream failures are not caught here: they surface as the *UnavailableException of the client and the handler
/// turns them into 503 before anything is written (fail-closed, pack §8).
/// </summary>
public sealed class MovementMasterDataValidator
{
    private readonly IProductMasterClient _productMaster;
    private readonly ILocationMasterClient _locationMaster;
    private readonly ITenantContext _tenantContext;
    private readonly InventoryPolicyOptions _policy;

    public MovementMasterDataValidator(
        IProductMasterClient productMaster,
        ILocationMasterClient locationMaster,
        ITenantContext tenantContext,
        IOptions<InventoryPolicyOptions> policy)
    {
        _productMaster = productMaster;
        _locationMaster = locationMaster;
        _tenantContext = tenantContext;
        _policy = policy.Value;
    }

    public async Task<MasterDataCheck> CheckAsync(ParsedMovement movement, CancellationToken ct)
    {
        var direction = movement.Direction
            ?? throw new InvalidOperationException("A REVERSAL is not validated against the masters; it copies the original.");

        // ── PRODUCT-MASTER ────────────────────────────────────────────────────────────────────────────────
        var validation = await _productMaster.ValidateRefsAsync(
        [
            new ProductRefQuery(movement.ItemId, null, null),
            new ProductRefQuery(null, movement.SkuId, movement.SkuLevel)
        ], ct);

        var itemKnown = validation.Any(r => r.Exists && SameId(r.ItemId, movement.ItemId) && r.SkuId is null);
        var skuKnown = validation.Any(r => r.Exists && SameId(r.SkuId, movement.SkuId)
                                           && string.Equals(r.SkuLevel, WireNames.ToWire(movement.SkuLevel), StringComparison.Ordinal));
        if (!itemKnown || !skuKnown)
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.UnknownItem,
                !itemKnown ? "itemId is not known to PRODUCT-MASTER." : "skuId/skuLevel is not known to PRODUCT-MASTER.");
        }

        var product = await _productMaster.GetProductAsync(movement.ItemId, ct);
        if (product is null)
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.UnknownItem, "itemId is not known to PRODUCT-MASTER.");
        }

        var sku = await _productMaster.GetSkuAsync(movement.SkuId, movement.SkuLevel, ct);
        if (sku is null)
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.UnknownItem, "skuId/skuLevel is not known to PRODUCT-MASTER.");
        }

        if (!SameId(sku.ItemId, movement.ItemId))
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.SkuItemMismatch,
                "The SKU does not belong to itemId (D-SKU-LINK).",
                new Dictionary<string, object?> { ["skuItemId"] = sku.ItemId, ["itemId"] = movement.ItemId.ToString() });
        }

        var uom = await _productMaster.GetSkuUomAsync(movement.SkuId, ct);
        if (uom is null || string.IsNullOrWhiteSpace(uom.BaseUomId)
            || !UomConverter.TryConvertToBase(movement.Quantity, movement.UomId, uom.BaseUomId, uom.Conversions,
                movement.TransactionDate, out var baseQuantity))
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.UomConversionMissing,
                $"No conversion from '{movement.UomId}' to the SKU base UoM.",
                new Dictionary<string, object?> { ["uomId"] = movement.UomId, ["baseUomId"] = uom?.BaseUomId });
        }

        if (product.LotControlled && movement.LotId is null)
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.LotRequired, "lotId is required: the item is lot-controlled.");
        }

        if (product.SerialControlled)
        {
            var distinct = movement.SerialIds?.Distinct(StringComparer.Ordinal).Count() ?? 0;
            if (baseQuantity != decimal.Truncate(baseQuantity) || distinct != baseQuantity)
            {
                return MasterDataCheck.Refused(InventoryErrorCodes.SerialCountMismatch,
                    "The item is serial-controlled: the number of distinct serialIds must equal the base quantity.",
                    new Dictionary<string, object?>
                    {
                        ["serialCount"] = distinct.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["baseQuantity"] = InventoryQuantity.ToWire(baseQuantity)
                    });
            }
        }

        // ── LOCATION ──────────────────────────────────────────────────────────────────────────────────────
        var sides = new List<(string WarehouseId, string LocationId)>();
        if (direction.FromLocationId is not null) sides.Add((direction.FromWarehouseId!, direction.FromLocationId));
        if (direction.ToLocationId is not null) sides.Add((direction.ToWarehouseId!, direction.ToLocationId));

        var ids = sides.Select(s => s.LocationId).Distinct(StringComparer.Ordinal).ToArray();
        var existence = await _locationMaster.ValidateLocationsAsync(ids, ct);

        foreach (var (warehouseId, locationId) in sides)
        {
            var refusal = await CheckLocationAsync(existence, warehouseId, locationId, ct);
            if (refusal is not null)
            {
                return refusal;
            }
        }

        return MasterDataCheck.Valid(baseQuantity, uom.BaseUomId);
    }

    private async Task<MasterDataCheck?> CheckLocationAsync(
        IReadOnlyList<LocationValidation> existence, string warehouseId, string locationId, CancellationToken ct)
    {
        var details = new Dictionary<string, object?> { ["locationId"] = locationId, ["warehouseId"] = warehouseId };

        var exists = existence.FirstOrDefault(r => string.Equals(r.LocationId, locationId, StringComparison.Ordinal));
        if (exists is null || !exists.Exists || exists.IsStockable == false)
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.UnknownLocation, "Location does not exist or is not stockable.", details);
        }

        var location = await _locationMaster.GetLocationAsync(locationId, ct);
        if (location is null
            || !string.Equals(location.Status, "Active", StringComparison.Ordinal)
            || !location.IsStockable)
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.UnknownLocation, "Location is not an Active stockable location.", details);
        }

        if (_policy.EnforceLocationLegalEntityMatch && !SameId(location.LegalEntityId, _tenantContext.LegalEntityId))
        {
            // A location of another legal entity is, for this caller, unknown (ASSUMPTION-0173-07).
            return MasterDataCheck.Refused(InventoryErrorCodes.UnknownLocation, "Location does not belong to the caller's legal entity.", details);
        }

        var path = await _locationMaster.GetLocationPathAsync(locationId, ct);
        if (path is null || !path.ContainsWarehouse(warehouseId))
        {
            return MasterDataCheck.Refused(InventoryErrorCodes.UnknownLocation, "Location is not under the named warehouse.", details);
        }

        return null;
    }

    private static bool SameId(string? wire, Guid expected)
        => Guid.TryParse(wire, out var parsed) && parsed == expected;
}
