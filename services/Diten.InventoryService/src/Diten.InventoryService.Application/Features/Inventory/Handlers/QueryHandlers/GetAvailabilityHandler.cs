using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Queries;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Handlers.QueryHandlers;

/// <summary>
/// getAvailability — sums the caller's balance rows for the item / sku in scope.
/// <c>byStatus</c> carries OnHand per every StockStatus value (zeros included) so a consumer can tell "no stock in
/// QUARANTINE" from "status not reported". An item / sku with no ledger row in this Tenant + LE answers 404
/// <c>UNKNOWN_ITEM</c> (ASSUMPTION-S1-04): the read path makes no upstream call, and another tenant's item is
/// indistinguishable from a non-existent one.
/// </summary>
public sealed class GetAvailabilityHandler : IRequestHandler<GetAvailabilityQuery, Response<AvailabilityViewDto>>
{
    private readonly IInventoryLedgerRepository _ledger;
    private readonly ITenantContext _tenantContext;

    public GetAvailabilityHandler(IInventoryLedgerRepository ledger, ITenantContext tenantContext)
    {
        _ledger = ledger;
        _tenantContext = tenantContext;
    }

    public async Task<Response<AvailabilityViewDto>> Handle(GetAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var itemId = QueryParameterRules.Uuid(request.ItemId);
        var skuId = QueryParameterRules.Uuid(request.SkuId);
        var skuLevel = QueryParameterRules.Level(request.SkuLevel);
        var warehouseId = QueryParameterRules.Text(request.WarehouseId);
        var locationId = QueryParameterRules.Text(request.LocationId);

        var rows = await _ledger.ListBalancesAsync(
            new AvailabilityFilter(itemId, skuId, skuLevel, warehouseId, locationId), cancellationToken);
        if (rows.Count == 0)
        {
            return Response<AvailabilityViewDto>.Fail(InventoryErrorCodes.UnknownItem,
                "No stock ledger record exists for this item/sku in the caller's legal entity.", 404);
        }

        var byStatus = WireNames.AllStockStatuses.ToDictionary(
            WireNames.ToWire,
            status => InventoryQuantity.ToWire(rows.Where(r => r.StockStatus == status).Sum(r => r.OnHand)));

        var scope = new Dictionary<string, string>();
        if (warehouseId is not null) scope["warehouseId"] = warehouseId;
        if (locationId is not null) scope["locationId"] = locationId;

        var distinctItems = rows.Select(r => r.ItemId).Distinct().ToList();
        var distinctSkus = rows.Select(r => r.SkuId).Distinct().ToList();
        var distinctLevels = rows.Select(r => r.SkuLevel).Distinct().ToList();
        SkuLevel? reportedLevel = skuLevel ?? (distinctLevels.Count == 1 ? distinctLevels[0] : null);

        return Response<AvailabilityViewDto>.Success(new AvailabilityViewDto
        {
            ItemId = (itemId ?? (distinctItems.Count == 1 ? distinctItems[0] : null))?.ToString(),
            SkuId = (skuId ?? (distinctSkus.Count == 1 ? distinctSkus[0] : null))?.ToString(),
            SkuLevel = reportedLevel.HasValue ? WireNames.ToWire(reportedLevel.Value) : null,
            LegalEntityId = _tenantContext.LegalEntityId.ToString(),
            Scope = scope,
            OnHand = InventoryQuantity.ToWire(rows.Sum(r => r.OnHand)),
            Reserved = InventoryQuantity.ToWire(rows.Sum(r => r.Reserved)),
            Available = InventoryQuantity.ToWire(rows.Sum(r => r.Available)),
            ByStatus = byStatus,
            BaseUomId = rows[0].BaseUomId,
            AsOf = InventoryMapping.Utc(DateTimeOffset.UtcNow)
        });
    }
}
