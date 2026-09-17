using Diten.ProcurementService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Grn.Queries;

/// <summary>GetGrnList (pack §3). Tenant+LE filtreli; opsiyonel poId + status + cursor.</summary>
public sealed record GetGrnListQuery(
    string? PoId = null,
    GrnStatus? Status = null,
    string? Cursor = null) : IRequest<Response<GrnListResultDto>>;
