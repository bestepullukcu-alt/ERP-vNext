using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.PlannedVisit.Queries;

/// <summary>Loads one plan's detail, including its read-only provenance/snapshot panels. Cross-tenant → 404.</summary>
public sealed record GetPlannedVisitByIdQuery(Guid PlannedVisitId) : IRequest<Response<PlannedVisitDetailDto>>;
