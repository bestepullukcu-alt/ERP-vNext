using Diten.PlanningService.Application.Features.DemandPlanning;

namespace Diten.PlanningService.Infrastructure.Features.DemandPlanning;

public sealed class UnconfiguredInvalidationAuthority : IInternalInvalidationAuthority
{
    public Task<InvalidationAuthorityEvidence> VerifyAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId,
        InvalidationImpactCode impactCode, string evidenceReference,
        CancellationToken cancellationToken) => Task.FromResult(
            new InvalidationAuthorityEvidence(false, false, false, false,
                Guid.Empty, Guid.Empty, Guid.Empty, impactCode, string.Empty, []));
}

public sealed class UnconfiguredInvalidatedHistoryAuthority : IInternalInvalidatedHistoryAuthority
{
    public Task<InvalidatedHistoryAuthorityEvidence> VerifyAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId,
        CancellationToken cancellationToken) => Task.FromResult(
            new InvalidatedHistoryAuthorityEvidence(false, false, false,
                Guid.Empty, Guid.Empty, Guid.Empty, []));
}
