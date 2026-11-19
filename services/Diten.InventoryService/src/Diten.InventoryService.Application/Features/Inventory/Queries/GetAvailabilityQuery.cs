using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Queries;

/// <summary>
/// GET /api/inventory/availability (contract <c>getAvailability</c>). Raw query strings; scope comes from the JWT. INVENTORY 1.6.0
/// (MOD-0173-FU03): optional <c>lotId</c> (needs <c>warehouseId</c> and one named SKU).
/// </summary>
public sealed record GetAvailabilityQuery(
    string? ItemId,
    string? SkuId,
    string? SkuLevel,
    string? WarehouseId,
    string? LocationId,
    string? LotId = null) : IRequest<Response<AvailabilityViewDto>>;
