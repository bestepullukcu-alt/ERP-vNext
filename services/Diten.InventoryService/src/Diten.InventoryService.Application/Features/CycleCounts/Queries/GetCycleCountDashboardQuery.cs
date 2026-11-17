using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Queries;

/// <summary><c>getCycleCountDashboard</c> (pack §8.13, AC-40) — the KPI cards, counted at read time; <c>warehouseId</c> optional (raw text, 400 when bad).</summary>
public sealed record GetCycleCountDashboardQuery(string? WarehouseId) : IRequest<Response<CycleCountDashboardDto>>;
