using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Rules;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Services;

/// <summary>
/// The checks a task write runs before it writes (pack §12; WMS draft §3.6, §4): the target bin, the storage-condition fit (§8.12), the
/// recall rule (§8.5), the source bucket's status (§8.4) and the serial bucket (§8.16). Each answers null (passes) or the refusal. They read
/// in-process (LOCATION, roles, PRODUCT-MASTER, RECALL, INVENTORY balances) and never write; an unreadable master is 503, nothing written.
/// </summary>
public sealed class PutawayChecks
{
    private readonly ILocationRepository _locations;
    private readonly LocationRoleReads _roles;
    private readonly IProductMasterClient _products;
    private readonly IWarehouseStockReader _stock;
    private readonly IRecallLotStatusReader _recall;
    private readonly IWarehouseSerialReader _serials;
    private readonly IWarehouseAssigneeDirectory _assignees;

    public PutawayChecks(ILocationRepository locations, LocationRoleReads roles, IProductMasterClient products, IWarehouseStockReader stock,
        IRecallLotStatusReader recall, IWarehouseSerialReader serials, IWarehouseAssigneeDirectory assignees)
    {
        _assignees = assignees;
        _locations = locations;
        _roles = roles;
        _products = products;
        _stock = stock;
        _recall = recall;
        _serials = serials;
    }

    private static Dictionary<string, object?> D(params (string Key, object? Value)[] values)
        => values.Where(v => v.Value is not null).ToDictionary(v => v.Key, v => v.Value);

    /// <summary>
    /// §12 target rules: a LOCATION node of the company (422 UNKNOWN_LOCATION), Active, a stockable Bin or stockable StorageLocation, under the
    /// task's warehouse, not the source, and holding no Receiving / Staging role, own or inherited (422 WMS_TARGET_NOT_ALLOWED + details.rule).
    /// </summary>
    public async Task<Response<T>?> TargetAsync<T>(Guid warehouseId, Guid fromLocationId, Guid toLocationId, CancellationToken ct)
    {
        var view = await _roles.ViewAsync(toLocationId, ct);
        if (view is null)
        {
            return PutawayTaskSupport.Fail<T>(InventoryErrorCodes.UnknownLocation, "The target location does not exist in the selected company.", 422,
                D(("locationId", toLocationId.ToString("D"))));
        }

        string? rule = null;
        var node = view.Location;
        if (node.Status != LocationStatus.Active) rule = "Inactive";
        else if (!node.IsStockable || node.Type is not (LocationType.Bin or LocationType.StorageLocation)) rule = "NotStockable";
        else if (!node.AncestorIds.Contains(warehouseId)) rule = "OtherWarehouse";
        else if (toLocationId == fromLocationId) rule = "SameAsSource";
        else if (view.EffectiveRoles.Contains(WarehouseRole.Receiving) || view.EffectiveRoles.Contains(WarehouseRole.Staging)) rule = "RoleLocation";

        return rule is null
            ? null
            : PutawayTaskSupport.Fail<T>(WarehouseErrorCodes.TargetNotAllowed, $"The target location is not allowed ({rule}).", 422,
                D(("rule", rule), ("locationId", toLocationId.ToString("D"))));
    }

    /// <summary>
    /// §8.12 fit: the product's conditions (PRODUCT-MASTER v1.1 <c>getSku.storageConditions</c>; absent = UNKNOWN → 422
    /// WMS_STORAGE_CONDITIONS_UNKNOWN) must all be in the target's effective accepted set (nearest tagged node; unlabelled = AMBIENT only).
    /// </summary>
    public async Task<Response<T>?> StorageFitAsync<T>(Guid skuId, SkuLevel level, Guid toLocationId, CancellationToken ct)
    {
        SkuReference? sku;
        try
        {
            sku = await _products.GetSkuAsync(skuId, level, ct);
        }
        catch (ProductMasterUnavailableException)
        {
            return PutawayTaskSupport.Fail<T>(InventoryErrorCodes.ProductMasterUnavailable, "PRODUCT-MASTER could not be reached; nothing was written.", 503);
        }

        var view = await _roles.ViewAsync(toLocationId, ct);
        var effective = view?.EffectiveConditions;
        var fit = LocationRoleRules.Fit(sku?.StorageConditions, effective);
        return fit switch
        {
            StorageConditionFit.Fits => null,
            StorageConditionFit.Unknown => PutawayTaskSupport.Fail<T>(WarehouseErrorCodes.StorageConditionsUnknown,
                "PRODUCT-MASTER does not state the product's storage conditions; complete the product card before putaway.", 422,
                D(("skuId", skuId.ToString("D")))),
            _ => PutawayTaskSupport.Fail<T>(WarehouseErrorCodes.StorageConditionMismatch,
                "The target location does not accept the product's storage conditions.", 422,
                D(("productConditions", sku!.StorageConditions!.ToArray()), ("locationConditions", (effective?.Codes ?? []).ToArray()),
                    ("inheritedFromLocationId", effective is { Inherited: true } e ? e.SourceLocationId.ToString("D") : null)))
        };
    }

    /// <summary>
    /// §8.5: a lot under recall is never put away (LOCATION_TRANSFER is exempt from the ledger's recall block) → 422 LOT_UNDER_RECALL in the
    /// INVENTORY shape (<c>details.movementType = LOCATION_TRANSFER</c>); the read fails → 503, nothing written.
    /// </summary>
    public async Task<Response<T>?> RecallAsync<T>(string? lotId, string? lotNumber, CancellationToken ct)
    {
        if (lotId is null) return null;
        IReadOnlySet<string> recalled;
        try
        {
            recalled = await _recall.UnderRecallAsync([lotId], ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PutawayTaskSupport.Fail<T>(InventoryErrorCodes.InventoryUnavailable, "The recall status of the lot could not be read; nothing was written.", 503);
        }

        return recalled.Contains(lotId)
            ? PutawayTaskSupport.Fail<T>(InventoryErrorCodes.LotUnderRecall, "The lot is under recall; it is not put away.", 422,
                D(("lotId", lotId), ("lotNumber", lotNumber), ("movementType", "LOCATION_TRANSFER")))
            : null;
    }

    /// <summary>The stock rows of the task's source location for its SKU and lot (every status).</summary>
    public async Task<IReadOnlyList<WarehouseStockRow>> BucketRowsAsync(Guid warehouseId, Guid locationId, Guid skuId, SkuLevel level, string? lotId, CancellationToken ct)
        => (await _stock.RowsAtAsync([locationId], ct))
            .Where(r => r.WarehouseId == warehouseId && r.SkuId == skuId && r.SkuLevel == level && string.Equals(r.LotId, lotId, StringComparison.Ordinal))
            .ToList();

    /// <summary>
    /// §8.4 at confirmation: the source bucket must still hold AVAILABLE stock; if it has none but the same SKU / lot sits there in another
    /// status, the stock was taken into quality control → 422 WMS_STOCK_NOT_PUTAWAY_ELIGIBLE (details.stockStatus). Quantity is the ledger's.
    /// </summary>
    public async Task<Response<T>?> StillAvailableAsync<T>(WarehouseTask task, CancellationToken ct)
    {
        var rows = await BucketRowsAsync(task.WarehouseId, task.FromLocationId, task.SkuId, task.SkuLevel, task.LotId, ct);
        if (rows.Any(r => r.StockStatus == StockStatus.Available)) return null;
        var held = rows.FirstOrDefault(r => r.StockStatus != StockStatus.Available);
        return held is null
            ? null
            : PutawayTaskSupport.Fail<T>(WarehouseErrorCodes.StockNotPutawayEligible, "The stock of the task is held for a quality decision.", 422,
                D(("stockStatus", WireNames.ToWire(held.StockStatus))));
    }

    /// <summary>
    /// §8.16 for a serial SKU: no non-AVAILABLE stock of the same lot + SKU at the location (422 WMS_SERIAL_BUCKET_MIXED_STATUS) and the
    /// available serials of the bucket are exactly <paramref name="expected"/> (422 WMS_SERIAL_BUCKET_CHANGED, details expected / found).
    /// </summary>
    public async Task<(Response<T>? Refusal, IReadOnlyList<string> Serials)> SerialsAsync<T>(WarehouseTask task, decimal expected, CancellationToken ct)
    {
        try
        {
            var rows = await BucketRowsAsync(task.WarehouseId, task.FromLocationId, task.SkuId, task.SkuLevel, task.LotId, ct);
            if (rows.Any(r => r.StockStatus != StockStatus.Available)) return (MixedStatus<T>(), []);
            var serials = await _serials.AvailableSerialsAsync(new WarehouseSerialBucket(task.ItemId, task.SkuId, task.SkuLevel, task.LotId, task.WarehouseId,
                task.FromLocationId), ct);
            return serials.Count == expected
                ? (null, serials)
                : (PutawayTaskSupport.Fail<T>(WarehouseErrorCodes.SerialBucketChanged, "The serials of the source bucket changed since the task was opened.", 422,
                    D(("expected", PutawayTaskSupport.Q(expected)), ("found", PutawayTaskSupport.Q(serials.Count)))), []);
        }
        catch (WarehouseInventoryUnavailableException)
        {
            return (PutawayTaskSupport.Fail<T>(InventoryErrorCodes.InventoryUnavailable, "The stock of the bucket could not be read; nothing was written.", 503), []);
        }
    }

    /// <summary>
    /// Q-13 A: an assignee must be an active user of the tenant — AUTH <c>lookup-validation</c> with the caller's token (422
    /// <c>WMS_ASSIGNEE_INVALID</c>; AUTH unreachable or refusing the caller → 503, nothing written). AUTH does not check the company.
    /// </summary>
    public async Task<Response<T>?> AssigneeAsync<T>(Guid? userId, CancellationToken ct)
    {
        if (userId is not { } id) return null;
        return await _assignees.CheckAsync(id, ct) switch
        {
            AssigneeCheck.Valid => null,
            AssigneeCheck.Invalid => PutawayTaskSupport.Fail<T>(WarehouseErrorCodes.AssigneeInvalid, "The assignee is not an active user of this tenant.", 422,
                D(("assigneeUserId", id.ToString("D")))),
            _ => PutawayTaskSupport.Fail<T>(WarehouseErrorCodes.AssigneeDirectoryUnavailable, "The assignee could not be checked with AUTH; nothing was written.", 503)
        };
    }

    public static Response<T> MixedStatus<T>()
        => PutawayTaskSupport.Fail<T>(WarehouseErrorCodes.SerialBucketMixedStatus,
            "The same lot of this SKU at this location also has stock waiting for a quality decision; complete that decision first.", 422);

    /// <summary>
    /// The PRODUCT-MASTER stock behaviour with the ledger's own rule (§4.1 <c>SerialControlled</c>, OD-10): Lsku / FinishedGood / Item read the
    /// SKU flag, Gsku the product's — Gsku opens only with an explicit product <c>serialControlled: false</c>.
    /// </summary>
    public async Task<(Response<T>? Refusal, bool SerialControlled)> SerialFlagAsync<T>(Guid itemId, Guid skuId, SkuLevel level, CancellationToken ct)
    {
        try
        {
            var sku = await _products.GetSkuAsync(skuId, level, ct);
            if (sku is null)
            {
                return (PutawayTaskSupport.Fail<T>(InventoryErrorCodes.ProductMasterDataIncomplete, "PRODUCT-MASTER does not know this SKU at this level.", 422), false);
            }

            var productSerial = level == SkuLevel.Gsku ? (await _products.GetProductAsync(itemId, ct))?.SerialControlled : null;
            var behaviour = ProductMasterStockRules.Behaviour(level, sku.LotControlled, sku.SerialControlled, productSerial);
            return behaviour.Refusal switch
            {
                null => (null, behaviour.SerialControlled),
                StockBehaviourRefusal.LevelNotStockable => (PutawayTaskSupport.Fail<T>(InventoryErrorCodes.SkuLevelNotStockable,
                    "A GSKU-level task needs a product whose serial flag is explicitly false; serial-tracked stock is held at Lsku / FinishedGood.", 422,
                    D(("skuLevel", WireNames.ToWire(level)), ("productSerialControlled", productSerial))), false),
                _ => (PutawayTaskSupport.Fail<T>(InventoryErrorCodes.ProductMasterDataIncomplete,
                    "PRODUCT-MASTER does not state the lot / serial flag this SKU level needs.", 422), false)
            };
        }
        catch (ProductMasterUnavailableException)
        {
            return (PutawayTaskSupport.Fail<T>(InventoryErrorCodes.ProductMasterUnavailable, "PRODUCT-MASTER could not be reached; nothing was written.", 503), false);
        }
    }
}
