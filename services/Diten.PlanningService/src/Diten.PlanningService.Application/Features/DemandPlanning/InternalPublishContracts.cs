using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

// A trusted server adapter must independently resolve actor identity, permission and
// every SKU x Warehouse scope. Caller-provided flags are never authorization evidence.
public interface IInternalPublishAuthority
{
    Task<PublishAuthorityEvidence> VerifyAsync(Guid tenantId, Guid legalEntityId,
        Guid actorId, IReadOnlyList<(Guid SkuId, string WarehouseId)> selectedScope,
        CancellationToken cancellationToken);
}

public sealed record PublishAuthorityEvidence(bool SourceAvailable, bool HasPublishPermission,
    bool ScopeVerified, bool IsIntegrationActor);

public enum PublishOutcome
{
    Published, Replayed, Conflict, Invalid, ScopeDenied, PermissionDenied,
    SeparationDenied, AuthorityUnavailable, InvalidSnapshot
}

public sealed record PublishResult(PublishOutcome Outcome, Guid? RevisionId = null,
    string? Checksum = null, int? StateVersion = null);

public interface IInternalPublishedRevisionStore
{
    Task<PublishResult> PublishAsync(Guid tenantId, Guid legalEntityId,
        Guid revisionId, Guid actorId, string requestKey, int expectedContentVersion,
        int expectedStateVersion, DateTimeOffset occurredAt,
        CancellationToken cancellationToken);
}
