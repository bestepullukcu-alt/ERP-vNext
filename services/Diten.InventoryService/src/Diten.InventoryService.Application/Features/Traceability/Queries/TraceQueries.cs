using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Queries;

/// <summary>getLot (v1).</summary>
public sealed record GetLotQuery(string LotId) : IRequest<Response<LotRefDto>>;

/// <summary>listLots (v1 filters + v1.1 filters / paging). Raw query values; the handler parses.</summary>
public sealed record ListLotsQuery(
    string? ItemId,
    string? Status,
    string? ExpiryBefore,
    string? Q,
    string? SkuId,
    string? SkuLevel,
    string? Warn,
    string? IncludeStock,
    string? Page,
    string? PageSize,
    string? Sort) : IRequest<Response<LotListDto>>;

/// <summary>getLotDetail (v1.1).</summary>
public sealed record GetLotDetailQuery(string LotId) : IRequest<Response<LotDetailDto>>;

/// <summary>getLotHistory (v1.1).</summary>
public sealed record GetLotHistoryQuery(string LotId, string? Page, string? PageSize) : IRequest<Response<LotHistoryPageDto>>;

/// <summary>getSerial (v1).</summary>
public sealed record GetSerialQuery(string SerialId) : IRequest<Response<SerialRefDto>>;

/// <summary>listSerials (v1.1).</summary>
public sealed record ListSerialsQuery(
    string? Q,
    string? SkuId,
    string? SkuLevel,
    string? LotId,
    string? Status,
    string? Page,
    string? PageSize) : IRequest<Response<SerialPageDto>>;

/// <summary>getGenealogy (v1 + v1.1 depth).</summary>
public sealed record GetGenealogyQuery(string LotId, string? Depth) : IRequest<Response<GenealogyDto>>;

/// <summary>validateTrace (v1) — the body is read by the API layer.</summary>
public sealed record ValidateTraceQuery(IReadOnlyList<string> LotIds, IReadOnlyList<string> SerialIds) : IRequest<Response<ValidateResponseDto>>;

/// <summary>checkGs1Code (v1.1, K-B) — read-only.</summary>
public sealed record CheckGs1CodeQuery(string? Code, string? SkuId, string? SkuLevel, string? LotId) : IRequest<Response<Gs1CheckResultDto>>;
