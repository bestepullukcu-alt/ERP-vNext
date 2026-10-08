using Diten.PlanningService.Application.Features.DemandPlanning;

namespace Diten.PlanningService.Infrastructure.Features.DemandPlanning;

// No live actor-to-scope source is approved yet. Production reads stay closed.
public sealed class UnconfiguredSnapshotReadAuthority : IInternalSnapshotReadAuthority
{
    public Task<SnapshotReadAuthorityEvidence> VerifyAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId,
        CancellationToken cancellationToken) =>
        Task.FromResult(new SnapshotReadAuthorityEvidence(false, false, false,
            Guid.Empty, Guid.Empty, Guid.Empty, []));
}
