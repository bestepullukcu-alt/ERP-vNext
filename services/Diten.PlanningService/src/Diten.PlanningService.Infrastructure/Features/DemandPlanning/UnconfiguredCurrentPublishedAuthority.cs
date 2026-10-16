using Diten.PlanningService.Application.Features.DemandPlanning;

namespace Diten.PlanningService.Infrastructure.Features.DemandPlanning;

// No approved live period/LegalEntity/series assignment source exists yet.
public sealed class UnconfiguredCurrentPublishedAuthority
    : IInternalCurrentPublishedAuthority
{
    public Task<CurrentPublishedAuthorityEvidence> VerifyAsync(Guid tenantId,
        Guid legalEntityId, string planningPeriodKey, Guid actorId,
        CancellationToken cancellationToken) =>
        Task.FromResult(new CurrentPublishedAuthorityEvidence(false, false, false,
            Guid.Empty, Guid.Empty, string.Empty, []));
}
