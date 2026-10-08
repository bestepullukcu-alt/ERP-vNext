using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Persistence.Features.DemandPlanning;

// Content fields never change after insertion. State and StateVersion are lifecycle metadata.
public sealed class PublishedRevisionManifest : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid RevisionId { get; set; }
    public Guid PlanningCycleId { get; set; }
    public string PlanningPeriodKey { get; set; } = string.Empty;
    public DateOnly AsOfDate { get; set; }
    public string CalendarId { get; set; } = string.Empty;
    public string CalendarVersion { get; set; } = string.Empty;
    public string TimeZoneId { get; set; } = string.Empty;
    public DateOnly HorizonStart { get; set; }
    public DateOnly HorizonEnd { get; set; }
    public List<PlanningWeek> Weeks { get; set; } = [];
    public List<PublishedSeriesScope> SelectedSeries { get; set; } = [];
    public List<DraftSeriesExclusion> ExcludedSeries { get; set; } = [];
    public int ContentVersion { get; set; }
    public int StateVersion { get; set; }
    // Technical CAS fence for rollback creation; excluded from content checksum and business state.
    public long RollbackFenceVersion { get; set; }
    public DemandRevisionState State { get; set; } = DemandRevisionState.Published;
    public int ExpectedPartCount { get; set; }
    public int ExpectedRowCount { get; set; }
    public string ChecksumScheme { get; set; } = "MOD0188-CANONICAL-1/SHA-256";
    public string Checksum { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    // Original Draft preparation time, verified against the durable Draft on read.
    public DateTimeOffset PreparedAt { get; set; }
    public List<Guid> SignificantEditorIds { get; set; } = [];
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewReason { get; set; }
    public Guid PublishedBy { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
}

public sealed record PublishedSeriesScope(Guid SkuId, string WarehouseId, string BaseUomId);

public sealed record PublishedWeekRow(int Number, DateOnly WeekStart, DateOnly WeekEnd,
    DraftWeekValueKind ValueKind, decimal? Quantity, DraftWeekSource Source,
    string ManualReason, Guid ManualActorId, DateTimeOffset ManualAt);

public sealed class PublishedRevisionPart : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid RevisionId { get; set; }
    public Guid SkuId { get; set; }
    public string WarehouseId { get; set; } = string.Empty;
    public string BaseUomId { get; set; } = string.Empty;
    public List<PublishedWeekRow> Rows { get; set; } = [];
}

public sealed class PublishedBaselineSlot : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public string PlanningPeriodKey { get; set; } = string.Empty;
    public Guid RevisionId { get; set; }
    // An invalidated current revision keeps its historical reference but is not usable.
    public bool IsAvailable { get; set; } = true;
}

public sealed class ManualDraftPublicationAuditRecord : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public Guid RevisionId { get; set; }
    public string RequestKey { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public DemandRevisionState NewState { get; set; }
    public int ContentVersion { get; set; }
    public int StateVersionAfter { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? ReplacedByRevisionId { get; set; }
    public string? Reason { get; set; }
    public string? ImpactCode { get; set; }
    public string? EvidenceReference { get; set; }
    public string? BusinessImpact { get; set; }
}
