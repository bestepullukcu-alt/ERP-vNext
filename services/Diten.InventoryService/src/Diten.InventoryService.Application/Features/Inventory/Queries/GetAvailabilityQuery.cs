using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Queries;

/// <summary>GET /api/inventory/availability (contract <c>getAvailability</c>). Raw query strings; scope comes from the JWT.</summary>
public sealed record GetAvailabilityQuery(
    string? ItemId,
    string? SkuId,
    string? SkuLevel,
    string? WarehouseId,
    string? LocationId) : IRequest<Response<AvailabilityViewDto>>;
