using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

// Internal persistence boundary. It does not expose a live API or grant a permission.
public interface IManualDraftStore
{
    Task<ManualDraftStoreResult> CreateAsync(
        DemandRevisionDraft draft, string requestKey, CancellationToken cancellationToken);

    Task<ManualDraftStoreResult> EditWeekAsync(
        Guid tenantId, Guid legalEntityId, Guid revisionId, Guid actorId,
        Guid skuId, string warehouseId, int weekNumber,
        DraftWeekValueKind kind, decimal? quantity, string reason,
        string requestKey, int expectedVersion, DateTimeOffset occurredAt,
        CancellationToken cancellationToken);

    Task<ManualDraftStoreResult> ExcludeSeriesAsync(
        Guid tenantId, Guid legalEntityId, Guid revisionId, Guid actorId,
        Guid skuId, string warehouseId, string reason, string requestKey,
        int expectedContentVersion, int expectedStateVersion,
        DateTimeOffset occurredAt, CancellationToken cancellationToken);

    Task<DemandRevisionDraft?> ReadAsync(
        Guid tenantId, Guid legalEntityId, Guid revisionId,
        CancellationToken cancellationToken);

    Task<ManualDraftStoreResult> TransitionReviewAsync(
        Guid tenantId, Guid legalEntityId, Guid revisionId, Guid actorId,
        DraftReviewAction action, string? reason, string requestKey,
        int expectedContentVersion, int expectedStateVersion,
        DateTimeOffset occurredAt, CancellationToken cancellationToken);
}

public enum ManualDraftStoreOutcome
{
    Created, Changed, Replayed, Conflict, Invalid, ScopeDenied, SeparationDenied
}

public sealed record ManualDraftStoreResult(
    ManualDraftStoreOutcome Outcome, DemandRevisionDraft? Draft = null);
