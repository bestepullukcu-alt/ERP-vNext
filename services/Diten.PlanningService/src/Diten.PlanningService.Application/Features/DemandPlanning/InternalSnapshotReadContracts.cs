using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

// The trusted server adapter must prove actor permission and revision-bound
// Tenant/LegalEntity/series scope without consulting the requested manifest or
// accepting a client-supplied eligibility flag. This is not public Demand v2.
public interface IInternalSnapshotReadAuthority
{
    Task<SnapshotReadAuthorityEvidence> VerifyAsync(Guid tenantId, Guid legalEntityId,
        Guid revisionId, Guid actorId,
        CancellationToken cancellationToken);
}

public sealed record SnapshotReadAuthorityEvidence(bool SourceAvailable,
    bool HasReadPermission, bool RevisionScopeVerified,
    Guid TenantId, Guid LegalEntityId, Guid RevisionId,
    IReadOnlyList<(Guid SkuId, string WarehouseId)> AuthorizedSeries);

public enum SnapshotReadOutcome
{
    Found, NotFound, PermissionDenied, AuthorityUnavailable,
    StateDenied, InvalidCursor, InvalidSnapshot, StoreUnavailable
}

public sealed record SnapshotReadResult<T>(SnapshotReadOutcome Outcome, T? Data = default);

public sealed record InternalSnapshotStatus(Guid TenantId, Guid LegalEntityId,
    Guid RevisionId, Guid PlanningCycleId, string PlanningPeriodKey,
    DemandRevisionState State, int StateVersion, int ContentVersion,
    string ChecksumScheme, string Checksum, int ExpectedPartCount, int ExpectedRowCount,
    DateOnly AsOfDate, string CalendarId, string CalendarVersion,
    string TimeZoneId, DateOnly HorizonStart, DateOnly HorizonEnd,
    IReadOnlyList<(Guid SkuId, string WarehouseId)> SelectedScope);

public sealed record InternalSnapshotManifest(InternalSnapshotStatus Status,
    DateOnly AsOfDate, string CalendarId, string CalendarVersion,
    string TimeZoneId, DateOnly HorizonStart, DateOnly HorizonEnd,
    IReadOnlyList<PlanningWeek> Weeks,
    IReadOnlyList<(Guid SkuId, string WarehouseId, string BaseUomId)> SelectedSeries,
    IReadOnlyList<DraftSeriesExclusion> ExcludedSeries,
    Guid CreatedBy, DateTimeOffset CreatedAt,
    IReadOnlyList<Guid> SignificantEditorIds,
    Guid? ReviewedBy, DateTimeOffset? ReviewedAt, string? ReviewReason,
    Guid PublishedBy, DateTimeOffset PublishedAt);

public sealed record InternalSnapshotRow(Guid SkuId, string WarehouseId,
    string BaseUomId, int WeekNumber, DateOnly WeekStart, DateOnly WeekEnd,
    DraftWeekValueKind ValueKind, decimal? Quantity, DraftWeekSource Source,
    string ManualReason = "");

public sealed record InternalSnapshotPage(InternalSnapshotStatus Status,
    IReadOnlyList<InternalSnapshotRow> Rows, string? NextCursor);

public interface IInternalPublishedSnapshotReader
{
    Task<SnapshotReadResult<InternalSnapshotStatus>> ReadStatusAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId,
        CancellationToken cancellationToken);

    Task<SnapshotReadResult<InternalSnapshotManifest>> ReadManifestAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId,
        CancellationToken cancellationToken);

    Task<SnapshotReadResult<InternalSnapshotPage>> ReadPageAsync(Guid tenantId,
        Guid legalEntityId, Guid revisionId, Guid actorId, int pageSize,
        string? cursor, CancellationToken cancellationToken);
}
