using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

public sealed record GetCurrentPublishedRevisionQuery(Guid TenantId,
    Guid LegalEntityId, string PlanningPeriodKey, Guid ActorId)
    : IRequest<Response<CurrentPublishedRevisionView>>;
