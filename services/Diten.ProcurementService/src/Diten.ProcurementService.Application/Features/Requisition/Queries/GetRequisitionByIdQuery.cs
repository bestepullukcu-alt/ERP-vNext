using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Requisition.Queries;

/// <summary>getRequisition (contract GET /requisitions/{requisitionId}). Public RequisitionId ile; cross-tenant/LE → 404.</summary>
public sealed record GetRequisitionByIdQuery(string RequisitionId) : IRequest<Response<RequisitionDto>>;
