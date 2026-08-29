using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU14 — one recorded monitoring check against an external source (GMG-QMS-SOP-0001 §10.2). This is the
/// EVIDENCE trail proving the source was checked at the required cadence: who checked, where they looked, what
/// version/effective date they observed, and whether a change was detected.
///
/// This is NOT the FU12 periodic review. FU12 reviews whether an INTERNAL controlled document is still current;
/// this records whether an EXTERNAL source has changed. The two must never be conflated — an overdue monitoring
/// check is a monitoring finding, not an internal document review overdue.
/// A check is an immutable append-only record: it is never hard-deleted.
/// </summary>
public sealed class ExternalDocumentMonitoringCheck : TenantScopedEntity
{
    public required Guid ExternalDocumentRegisterEntryId { get; set; }

    public DateTimeOffset CheckDate { get; set; } = DateTimeOffset.UtcNow;
    public string? CheckedBy { get; set; }
    public Guid? CheckedByUserId { get; set; }

    /// <summary>Where the check was performed (authority portal, subscription service, bulletin). Reference only.</summary>
    public required string MonitoringSource { get; set; }

    public string? SourceVersionObserved { get; set; }
    public DateTimeOffset? SourceEffectiveDateObserved { get; set; }

    public bool ChangeDetected { get; set; }

    /// <summary>Required whenever <see cref="ChangeDetected"/> is true.</summary>
    public string? ChangeSummary { get; set; }

    /// <summary>Mandatory evidence pointer for the check (SOP §10.2). A reference — never the document bytes.</summary>
    public required string EvidenceReference { get; set; }

    /// <summary>The next due date computed at the time of this check (carried onto the register entry).</summary>
    public DateTimeOffset? NextCheckDueDate { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
