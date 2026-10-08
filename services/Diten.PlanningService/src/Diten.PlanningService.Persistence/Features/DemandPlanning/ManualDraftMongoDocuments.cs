using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

// A revision is one business aggregate. Each series is a bounded physical part.
public sealed class ManualDraftManifest : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid PlanningCycleId { get; set; }
    public DemandRevisionState State { get; set; } = DemandRevisionState.Draft;
    public int StateVersion { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewReason { get; set; }
    public string CreateRequestKey { get; set; } = string.Empty;
    public string CreateFingerprint { get; set; } = string.Empty;
    public string CycleJson { get; set; } = string.Empty;
    public string CreationReason { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public int ExpectedPartCount { get; set; }
    // Null for pre-existing manual drafts. A rollback is a new revision, not a state rewind.
    public Guid? SourceRevisionId { get; set; }
    public DemandRevisionState? SourceState { get; set; }
    public int? SourceStateVersion { get; set; }
    public string? SourceChecksum { get; set; }
    public string? CopiedScopeJson { get; set; }
}

public sealed class ManualDraftSeriesPart : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid RevisionId { get; set; }
    public Guid SkuId { get; set; }
    public string WarehouseId { get; set; } = string.Empty;
    public string InitialSeriesJson { get; set; } = string.Empty;
    // Present only for rollback drafts; legacy InitialSeriesJson remains readable.
    public string? SourceSeriesJson { get; set; }
}

public sealed class ManualDraftAuditRecord : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid RevisionId { get; set; }
    public string RequestKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int VersionAfter { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ChangeJson { get; set; }
    public Guid? SourceRevisionId { get; set; }
    public DemandRevisionState? SourceState { get; set; }
    public int? SourceStateVersion { get; set; }
    public string? SourceChecksum { get; set; }
    public string? CopiedScopeJson { get; set; }
}

public sealed record RollbackWeekSnapshot(int Number, DateOnly WeekStart, DateOnly WeekEnd,
    DraftWeekValueKind ValueKind, decimal? Quantity, DraftWeekSource Source,
    string ManualReason, Guid ManualActorId, DateTimeOffset ManualAt);

public sealed record RollbackSeriesSnapshot(Guid SkuId, string WarehouseId,
    string BaseUomId, IReadOnlyList<RollbackWeekSnapshot> Weeks,
    DraftSeriesExclusion? SourceExclusion);

public sealed record RollbackScopeSnapshot(
    IReadOnlyList<PublishedSeriesScope> SelectedSeries,
    IReadOnlyList<DraftSeriesExclusion> ExcludedSeries);
