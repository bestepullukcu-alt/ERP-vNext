using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

public sealed record GetDemandRevisionStatusQuery(Guid TenantId, Guid LegalEntityId,
    Guid RevisionId, Guid ActorId) : IRequest<Response<RevisionStatusView>>;
