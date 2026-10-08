using Diten.PlanningService.Application.Features.DemandPlanning.ManualDrafts;

namespace Diten.PlanningService.Infrastructure.Features.DemandPlanning;

// No live Platform/MDM/LOCATION authority is configured for this development slice.
public sealed class UnconfiguredManualDraftAuthority : IManualDraftAuthority
{
    public Task<Guid?> ResolveSelectedAsync(Guid tenantId, Guid actorId,
        Guid selectedLegalEntityHint, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Manual Draft LegalEntity authority is not configured.");

    public Task<IReadOnlyList<VerifiedDraftSeriesReference>?> VerifyCreateSeriesAsync(
        Guid tenantId, Guid actorId, Guid legalEntityId,
        IReadOnlyList<DraftSeriesKey> requested, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Manual Draft reference authority is not configured.");

    public Task<bool?> CanAccessAsync(Guid tenantId, Guid actorId, Guid legalEntityId,
        IReadOnlyList<DraftSeriesKey> series, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Manual Draft scope authority is not configured.");
}
