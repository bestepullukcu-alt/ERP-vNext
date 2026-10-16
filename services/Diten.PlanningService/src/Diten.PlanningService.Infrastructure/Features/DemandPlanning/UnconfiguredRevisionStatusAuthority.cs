using Diten.PlanningService.Application.Features.DemandPlanning;

namespace Diten.PlanningService.Infrastructure.Features.DemandPlanning;

// No approved live actor-to-revision scope source exists yet.
public sealed class UnconfiguredRevisionStatusAuthority : IInternalRevisionStatusAuthority
{
    public Task<RevisionStatusAuthorityEvidence> VerifyAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId,
        CancellationToken cancellationToken) =>
        Task.FromResult(new RevisionStatusAuthorityEvidence(false, false, false,
            Guid.Empty, Guid.Empty, Guid.Empty, []));
}
