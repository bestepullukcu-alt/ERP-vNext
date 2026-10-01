using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Inventory.Queries;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Handlers.QueryHandlers;

/// <summary>
/// getAvailability — the caller's balance rows for the item / sku in scope, plus the SKU-level reservations (S2).
/// <list type="bullet">
/// <item><c>byStatus</c> carries OnHand per every StockStatus value (zeros included).</item>
/// <item><c>reserved</c> = Σ remaining quantity of ACTIVE, not-yet-due reservations of the matching SKUs;
/// <c>available</c> = Σ OnHand(AVAILABLE) − reserved (pack ASSUMPTION-0173-12). Reservations are not located, so with a
/// warehouse / location scope the subtraction is floored at 0 (ASSUMPTION-S2-05).</item>
/// <item>FU-S1-01 / ASSUMPTION-0173-18: with NO ledger row, PRODUCT-MASTER decides — known item/SKU → 200 with zeros;
/// unknown → 404 UNKNOWN_ITEM; unreachable → 503 PRODUCT_MASTER_UNAVAILABLE (fail-closed). With rows, no upstream call.</item>
/// </list>
/// </summary>
public sealed class GetAvailabilityHandler : IRequestHandler<GetAvailabilityQuery, Response<AvailabilityViewDto>>
{
    private readonly IInventoryLedgerRepository _ledger;
    private readonly IInventoryReservationRepository _reservations;
    private readonly IProductMasterClient _productMaster;
    private readonly ITenantContext _tenantContext;

    public GetAvailabilityHandler(
        IInventoryLedgerRepository ledger,
        IInventoryReservationRepository reservations,
        IProductMasterClient productMaster,
        ITenantContext tenantContext)
    {
        _ledger = ledger;
        _reservations = reservations;
        _productMaster = productMaster;
        _tenantContext = tenantContext;
    }

    public async Task<Response<AvailabilityViewDto>> Handle(GetAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var itemId = QueryParameterRules.Uuid(request.ItemId);
        var skuId = QueryParameterRules.Uuid(request.SkuId);
        var skuLevel = QueryParameterRules.Level(request.SkuLevel);
        var warehouseId = QueryParameterRules.Text(request.WarehouseId);
        var locationId = QueryParameterRules.Text(request.LocationId);

        var filter = new AvailabilityFilter(itemId, skuId, skuLevel, warehouseId, locationId);
        var rows = await _ledger.ListBalancesAsync(filter, cancellationToken);

        if (rows.Count == 0)
        {
            bool known;
            try
            {
                known = await IsKnownToProductMasterAsync(itemId, skuId, skuLevel, cancellationToken);
            }
            catch (ProductMasterUnavailableException)
            {
                return Response<AvailabilityViewDto>.Fail(InventoryErrorCodes.ProductMasterUnavailable,
                    "PRODUCT-MASTER could not be reached; availability of an item without stock cannot be confirmed.", 503);
            }

            if (!known)
            {
                return Response<AvailabilityViewDto>.Fail(InventoryErrorCodes.UnknownItem,
                    "The item/sku is not known to PRODUCT-MASTER.", 404);
            }
        }

        var reserved = await _reservations.SumActiveReservedAsync(
            filter with { WarehouseId = null, LocationId = null }, DateTimeOffset.UtcNow, cancellationToken);
        var availableOnHand = rows.Where(r => r.StockStatus == StockStatus.Available).Sum(r => r.OnHand);
        var scoped = warehouseId is not null || locationId is not null;
        var available = availableOnHand - reserved;
        if (scoped && available < 0m)
        {
            available = 0m;
        }

        var byStatus = WireNames.AllStockStatuses.ToDictionary(
            WireNames.ToWire,
            status => InventoryQuantity.ToWire(rows.Where(r => r.StockStatus == status).Sum(r => r.OnHand)));

        var scope = new Dictionary<string, string>();
        if (warehouseId is not null) scope["warehouseId"] = warehouseId;
        if (locationId is not null) scope["locationId"] = locationId;

        return Response<AvailabilityViewDto>.Success(new AvailabilityViewDto
        {
            ItemId = (itemId ?? Single(rows, r => r.ItemId))?.ToString(),
            SkuId = (skuId ?? Single(rows, r => r.SkuId))?.ToString(),
            SkuLevel = (skuLevel ?? Single(rows, r => r.SkuLevel)) is { } level ? WireNames.ToWire(level) : null,
            LegalEntityId = _tenantContext.LegalEntityId.ToString(),
            Scope = scope,
            OnHand = InventoryQuantity.ToWire(rows.Sum(r => r.OnHand)),
            Reserved = InventoryQuantity.ToWire(reserved),
            Available = InventoryQuantity.ToWire(available),
            ByStatus = byStatus,
            BaseUomId = rows.Count > 0 ? rows[0].BaseUomId : null,
            AsOf = InventoryMapping.Utc(DateTimeOffset.UtcNow)
        });
    }

    /// <summary>Every named reference (item and/or sku) must exist; a sku without a level matches any level.</summary>
    private async Task<bool> IsKnownToProductMasterAsync(Guid? itemId, Guid? skuId, SkuLevel? skuLevel, CancellationToken ct)
    {
        var refs = new List<ProductRefQuery>();
        if (itemId.HasValue) refs.Add(new ProductRefQuery(itemId, null, null));
        if (skuId.HasValue) refs.Add(new ProductRefQuery(null, skuId, skuLevel));

        var results = await _productMaster.ValidateRefsAsync(refs, ct);

        var itemKnown = !itemId.HasValue || results.Any(r => r.Exists && r.SkuId is null && Same(r.ItemId, itemId.Value));
        var skuKnown = !skuId.HasValue || results.Any(r => r.Exists && Same(r.SkuId, skuId.Value)
            && (!skuLevel.HasValue || string.Equals(r.SkuLevel, WireNames.ToWire(skuLevel.Value), StringComparison.Ordinal)));
        return itemKnown && skuKnown;
    }

    private static bool Same(string? wire, Guid expected) => Guid.TryParse(wire, out var parsed) && parsed == expected;

    private static T? Single<T>(IReadOnlyList<InventoryBalance> rows, Func<InventoryBalance, T> pick) where T : struct
    {
        var distinct = rows.Select(pick).Distinct().ToList();
        return distinct.Count == 1 ? distinct[0] : null;
    }
}
