using Diten.ProcurementService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Requisition.Queries;

/// <summary>listRequisitions (contract GET /requisitions). Tenant+LE filtreli; opsiyonel status + cursor sayfalama.</summary>
public sealed record GetRequisitionListQuery(
    RequisitionStatus? Status = null,
    string? Cursor = null) : IRequest<Response<RequisitionListResultDto>>;
