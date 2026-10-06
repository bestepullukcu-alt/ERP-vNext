using Diten.Shared.Core;
using MediatR;

namespace Diten.ManufacturingService.Application.Features.Boms.Queries;

public sealed record GetBomListQuery(Guid? ItemId, string? Status, int Page, int PageSize) : IRequest<Response<BomListResponse>>;

public sealed record GetBomVersionQuery(Guid BomVersionId) : IRequest<Response<BomView>>;

public sealed record GetCurrentBomQuery(Guid ItemId, DateOnly? AsOfDate) : IRequest<Response<BomView>>;

/// <summary>POST /api/bom/explode — HTTP POST olsa da kalıcı hiçbir şey yazmaz (sorgu).</summary>
public sealed record ExplodeBomQuery(ExplodeBomRequest Body) : IRequest<Response<ExplodeBomResponse>>;

public sealed record GetBomHistoryQuery(Guid BomVersionId) : IRequest<Response<BomHistoryResponse>>;
