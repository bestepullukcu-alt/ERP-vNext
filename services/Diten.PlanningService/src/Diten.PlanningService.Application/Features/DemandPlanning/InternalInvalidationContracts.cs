namespace Diten.PlanningService.Application.Features.DemandPlanning;

// Forecast deviation is deliberately not a material-impact category.
public enum InvalidationImpactCode
{
    IdentityOrScope,
    CalendarTimeUnitOrQuantity,
    SourceMethodOrPolicy,
    ContentIntegrity,
    ForecastDeviation
}

// The adapter must independently verify permission, revision-bound scope and
// material downstream impact behind the evidence reference. Neither a caller
// flag nor reason text is proof. Production remains unconfigured and closed.
public interface IInternalInvalidationAuthority
{
    Task<InvalidationAuthorityEvidence> VerifyAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId,
        InvalidationImpactCode impactCode, string evidenceReference,
        CancellationToken cancellationToken);
}

public sealed record InvalidationAuthorityEvidence(bool SourceAvailable,
    bool HasInvalidatePermission, bool RevisionScopeVerified,
    bool MaterialImpactVerified, Guid TenantId, Guid LegalEntityId,
    Guid RevisionId, InvalidationImpactCode ImpactCode,
    string EvidenceReference,
    IReadOnlyList<(Guid SkuId, string WarehouseId)> AuthorizedSeries,
    string? VerifiedBusinessImpact = null);

public enum InvalidationOutcome
{
    Invalidated, Replayed, Conflict, InvalidRequest, NotFound,
    PermissionDenied, AuthorityUnavailable, MaterialImpactMissing,
    InvalidSnapshot
}

public sealed record InvalidationResult(InvalidationOutcome Outcome,
    Guid? RevisionId = null, int? StateVersion = null);

public interface IInternalRevisionInvalidator
{
    Task<InvalidationResult> InvalidateAsync(Guid tenantId, Guid legalEntityId,
        Guid revisionId, Guid actorId, InvalidationImpactCode impactCode,
        string reason, string evidenceReference, string requestKey,
        int expectedContentVersion, int expectedStateVersion,
        CancellationToken cancellationToken);
}

public interface IInternalInvalidatedHistoryAuthority
{
    Task<InvalidatedHistoryAuthorityEvidence> VerifyAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId,
        CancellationToken cancellationToken);
}

public sealed record InvalidatedHistoryAuthorityEvidence(bool SourceAvailable,
    bool HasAuditReadPermission, bool RevisionScopeVerified,
    Guid TenantId, Guid LegalEntityId, Guid RevisionId,
    IReadOnlyList<(Guid SkuId, string WarehouseId)> AuthorizedSeries);

public sealed record InternalInvalidatedHistory(Guid RevisionId, int StateVersion,
    string ChecksumScheme, string Checksum, string Warning,
    InternalSnapshotManifest Manifest, IReadOnlyList<InternalSnapshotRow> Rows,
    RevisionInvalidationView? Invalidation = null);

public sealed record InternalInvalidatedHistoryPage(string Warning,
    InternalSnapshotStatus Status, IReadOnlyList<InternalSnapshotRow> Rows,
    string? NextCursor);

public interface IInternalInvalidatedHistoryReader
{
    Task<SnapshotReadResult<InternalInvalidatedHistory>> ReadAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId,
        CancellationToken cancellationToken);

    Task<SnapshotReadResult<InternalInvalidatedHistoryPage>> ReadPageAsync(
        Guid tenantId, Guid legalEntityId, Guid revisionId, Guid actorId,
        int pageSize, string? cursor, CancellationToken cancellationToken);
}
