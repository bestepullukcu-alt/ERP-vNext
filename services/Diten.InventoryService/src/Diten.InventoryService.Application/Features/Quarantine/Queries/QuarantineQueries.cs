using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Quarantine.Queries;

/// <summary>§4.1 listHolds — raw query strings; the handler parses them (a malformed filter is a 400).</summary>
public sealed record ListHoldsQuery(
    string? Q, string? WarehouseId, string? StockStatus, string? Source, string? Status, string? ReviewState, string? LateOnly,
    string? LotId, string? Page, string? PageSize) : IRequest<Response<HoldListDto>>;

public sealed record GetHoldQuery(string HoldId) : IRequest<Response<HoldDetailDto>>;

public sealed record GetDecisionQuery(string DecisionId) : IRequest<Response<DecisionDto>>;

public sealed record ListBlockedQuery(
    string? Q, string? BlockReasonCode, string? PendingOnly, string? Origin, string? Status, string? WarehouseId, string? Page, string? PageSize,
    string Language) : IRequest<Response<BlockedListDto>>;

public sealed record GetBlockedQuery(string BlockedId, string Language) : IRequest<Response<BlockedStockDetailDto>>;

public sealed record GetSettingsQuery : IRequest<Response<SettingsDto>>;

public sealed record ListBlockReasonsQuery(string? ActiveOnly) : IRequest<Response<IReadOnlyList<BlockReasonDto>>>;

/// <summary>listRetest — the retest queue (KQ-H, Q4); raw query strings, parsed by the handler.</summary>
public sealed record ListRetestQuery(string? HorizonDays, string? State, string? WarehouseId) : IRequest<Response<IReadOnlyList<RetestItemDto>>>;

/// <summary>getLotQualityStatus — the quality picture of one lot in the caller's company (Q5, O-Q4-2).</summary>
public sealed record GetLotQualityStatusQuery(string LotId, string Language) : IRequest<Response<LotQualityStatusDto>>;
