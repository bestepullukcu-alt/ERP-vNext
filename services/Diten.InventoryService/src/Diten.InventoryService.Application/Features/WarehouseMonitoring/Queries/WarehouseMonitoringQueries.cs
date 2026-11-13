using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.WarehouseMonitoring.Queries;

/// <summary>WMS draft §9 <c>listWarehouseExceptions</c> — <c>kind?</c>, <c>warehouseId?</c>, <c>page</c>, <c>pageSize ≤ 200</c> (raw; the handler parses).</summary>
public sealed record GetWarehouseExceptionListQuery(string? Kind, string? WarehouseId, string? Page, string? PageSize) : IRequest<Response<WarehouseExceptionPageDto>>;

/// <summary>WMS draft §9 <c>listWarehouseActivity</c> — <c>from</c> / <c>to</c> mandatory (≤ 31 days), <c>warehouseId?</c>, <c>kind?</c>, paging.</summary>
public sealed record GetWarehouseActivityListQuery(string? From, string? To, string? WarehouseId, string? Kind, string? Page, string? PageSize)
    : IRequest<Response<WarehouseActivityPageDto>>;

/// <summary>WMS draft §9 <c>getWarehouseIntegrationStatus</c>.</summary>
public sealed record GetWarehouseIntegrationStatusQuery : IRequest<Response<WarehouseIntegrationStatusDto>>;
