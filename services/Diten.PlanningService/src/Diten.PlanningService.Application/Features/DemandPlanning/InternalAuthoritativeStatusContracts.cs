using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

// Status is not a snapshot or permission to read rows. Production authority
// must resolve the actor's consume permission and revision-bound scope without
// consulting the requested manifest or trusting caller-supplied scope flags.
public interface IInternalRevisionStatusAuthority
{
    Task<RevisionStatusAuthorityEvidence> VerifyAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId,
        CancellationToken cancellationToken);
}

public sealed record RevisionStatusAuthorityEvidence(bool SourceAvailable,
    bool HasConsumePermission, bool RevisionScopeVerified,
    Guid TenantId, Guid LegalEntityId, Guid RevisionId,
    IReadOnlyList<(Guid SkuId, string WarehouseId)> AuthorizedSeries);

public enum AuthoritativeStatusOutcome
{
    Found, NotFound, PermissionDenied, AuthorityUnavailable,
    Inconsistent, StoreUnavailable
}

public sealed record AuthoritativeRevisionStatus(Guid TenantId,
    Guid LegalEntityId, Guid RevisionId, Guid PlanningCycleId,
    string PlanningPeriodKey, DemandRevisionState State,
    int StateVersion, int ContentVersion,
    IReadOnlyList<(Guid SkuId, string WarehouseId)> SelectedScope,
    int ExcludedSeriesCount,
    Guid PreparedBy, DateTimeOffset PreparedAt,
    IReadOnlyList<Guid> SignificantEditorIds,
    Guid? ReviewedBy, DateTimeOffset? ReviewedAt,
    Guid PublishedBy, DateTimeOffset PublishedAt,
    string? InvalidationReason, string? InvalidationImpactCode,
    string? InvalidationEvidenceReference, Guid? InvalidatedBy,
    DateTimeOffset? InvalidatedAt);

public sealed record AuthoritativeStatusResult(AuthoritativeStatusOutcome Outcome,
    AuthoritativeRevisionStatus? Status = null);

public interface IInternalAuthoritativeRevisionStatusReader
{
    Task<AuthoritativeStatusResult> ReadAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId,
        CancellationToken cancellationToken);
}
