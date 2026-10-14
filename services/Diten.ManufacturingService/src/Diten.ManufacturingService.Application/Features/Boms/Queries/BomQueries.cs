using Diten.Shared.Core;
using MediatR;

namespace Diten.ManufacturingService.Application.Features.Boms.Queries;

/// <summary>
/// Platform server-mode liste sorgusu (BL-440): <c>start</c>/<c>length</c>/<c>search</c>/<c>orderBy</c>/<c>orderDir</c>
/// + filtreler. Export aynı sorguyu <see cref="ExportRowCap"/> ile çalıştırır (BL-452): dilim yok, en çok cap + 1 satır.
/// </summary>
public sealed record GetBomListQuery(
    Guid? ItemId = null,
    string[]? Status = null,
    string? Search = null,
    string? OrderBy = null,
    string? OrderDir = null,
    int Start = 0,
    int Length = 25,
    int? ExportRowCap = null) : IRequest<Response<BomListResponse>>;

public sealed record GetBomVersionQuery(Guid BomVersionId) : IRequest<Response<BomView>>;

public sealed record GetCurrentBomQuery(Guid ItemId, DateOnly? AsOfDate) : IRequest<Response<BomView>>;

/// <summary>POST /api/bom/explode — HTTP POST olsa da kalıcı hiçbir şey yazmaz (sorgu).</summary>
public sealed record ExplodeBomQuery(ExplodeBomRequest Body) : IRequest<Response<ExplodeBomResponse>>;

public sealed record GetBomHistoryQuery(Guid BomVersionId) : IRequest<Response<BomHistoryResponse>>;
