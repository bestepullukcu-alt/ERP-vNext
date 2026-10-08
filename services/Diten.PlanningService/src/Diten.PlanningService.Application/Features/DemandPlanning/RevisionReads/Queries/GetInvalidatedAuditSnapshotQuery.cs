using MediatR;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

public sealed record GetInvalidatedAuditSnapshotQuery(Guid TenantId, Guid LegalEntityId,
    Guid RevisionId, Guid ActorId) : IRequest<Response<InvalidatedAuditSnapshotView>>;
