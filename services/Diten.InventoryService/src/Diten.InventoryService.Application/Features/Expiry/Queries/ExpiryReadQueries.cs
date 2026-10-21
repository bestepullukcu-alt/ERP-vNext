using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Expiry.Queries;

// WP-0176-F4 — FEFO-BUNDLE 4.1–4.4 (getDashboard, listApproaching, listExpired, getFefoOrder). Raw query text in, validated by the handler
// (400 INVALID_REQUEST) — the F1 list pattern.

public sealed record GetExpiryDashboardQuery(string? WarehouseId, string? PeriodDays) : IRequest<Response<ExpiryDashboardDto>>;

public sealed record ListApproachingLotsQuery(
    string? WarehouseId,
    string? SkuId,
    string? Severity,
    string? HorizonDays,
    string? IncludeHeld,
    string? Q,
    string? Sort,
    string? Page,
    string? PageSize) : IRequest<Response<LotExpiryPageDto>>;

public sealed record ListExpiredLotsQuery(string? WarehouseId, string? SkuId, string? MinDaysSinceExpiry, string? Page, string? PageSize)
    : IRequest<Response<LotExpiryPageDto>>;

public sealed record GetFefoOrderQuery(string? WarehouseId, string? ItemId, string? SkuId, string? SkuLevel, string? Quantity, string? AsOf)
    : IRequest<Response<FefoOrderDto>>;
