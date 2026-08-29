using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU17 — an obsolete/uncontrolled copy finding (GMG-QMS-SOP-0001 §6.2 obsolete copy, §16 deviations). Use of
/// an obsolete or unapproved document is a QA quality event — this record captures the finding and REFERENCES a quality
/// event / deviation (no CAPA module here). Findings are idempotent per (register entry + finding type + copy); resolving
/// changes status only. Never hard-deleted.
/// </summary>
public sealed class DocumentObsoleteCopyFinding : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }
    public Guid? ControlledCopyId { get; set; }

    /// <summary>Deterministic dedupe key, e.g. <c>SuspendedDocumentInUse:{copyId}</c>.</summary>
    public required string FindingKey { get; set; }

    public ObsoleteCopyFindingType FindingType { get; set; }
    public ObsoleteCopyFindingSeverity Severity { get; set; } = ObsoleteCopyFindingSeverity.Warning;
    public ObsoleteCopyFindingStatus Status { get; set; } = ObsoleteCopyFindingStatus.Open;

    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? DetectedBy { get; set; }
    public string? LocationDescription { get; set; }
    public required string Description { get; set; }

    public string? QualityEventReference { get; set; }
    public string? DeviationReference { get; set; }
    public string? ResolutionEvidenceReference { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
