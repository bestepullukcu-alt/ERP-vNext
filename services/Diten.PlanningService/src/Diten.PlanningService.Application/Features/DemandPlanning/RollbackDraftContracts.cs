using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

public enum RollbackDraftOutcome
{
    Created, Replayed, Conflict, InvalidSource, NotFound,
    PermissionDenied, AuthorityUnavailable, InvalidSnapshot
}

public sealed record RollbackDraftResult(RollbackDraftOutcome Outcome,
    DemandRevisionDraft? Draft = null);

// Internal producer boundary. A client cannot supply a source scope or origin proof.
public interface IRollbackDraftStore
{
    Task<RollbackDraftResult> CreateAsync(Guid tenantId, Guid legalEntityId,
        Guid sourceRevisionId, Guid actorId, string reason, string requestKey,
        int expectedSourceStateVersion, DateTimeOffset occurredAt,
        CancellationToken cancellationToken);
}
