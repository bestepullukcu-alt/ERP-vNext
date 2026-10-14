using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Queries;

/// <summary>GET /api/inventory/balance (contract <c>getBalance</c>). Raw query strings; scope comes from the JWT.</summary>
public sealed record GetBalanceQuery(
    string? ItemId,
    string? SkuId,
    string? SkuLevel,
    string? WarehouseId,
    string? LocationId,
    string? LotId,
    string? StockStatus,
    string? Page,
    string? PageSize) : IRequest<Response<BalancePageDto>>;
