using Diten.ProcurementService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Queries;

/// <summary>listRfxEvents (contract GET /events). Tenant+LE filtreli; opsiyonel status filtresi + cursor sayfalama.</summary>
public sealed record GetRfxEventListQuery(
    RfxStatus? Status = null,
    string? Cursor = null) : IRequest<Response<RfxEventListResultDto>>;
