using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU22 — a quality event raised from a document-control failure (GMG-QMS-SOP-0001).
///
/// WHY THIS EXISTS: FU13/FU14/FU17/FU20/FU21 already carry <c>QualityEventReference</c>,
/// <c>DeviationReference</c> and <c>CorrectiveActionReference</c> — but only as free-text strings pointing at
/// records held somewhere else. Nothing could be queried, aggregated or closed out. This aggregate gives those
/// references a real, traceable home inside document control, without removing the string fields that still work.
///
/// SCOPE BOUNDARY: a BRIDGE, not a QMS. There is no CAPA workflow engine, no investigation module, no root-cause
/// methodology, no effectiveness scheduler, no e-signature, and no external QMS API call.
/// <see cref="ExternalQualitySystemReference"/> is the seam where a real QMS record id will live.
///
/// Closure is gated: an event that required a deviation cannot close while that deviation is open, and one that
/// required CAPA cannot close while its actions are outstanding. Nothing is ever hard-deleted.
/// </summary>
public sealed class DocumentQualityEvent : TenantScopedEntity
{
    public required string QualityEventNumber { get; set; }

    public required string EventTitle { get; set; }
    public required string EventDescription { get; set; }

    public QualityEventType EventType { get; set; } = QualityEventType.Other;
    public QualityEventSeverity EventSeverity { get; set; } = QualityEventSeverity.Minor;
    public QualityEventStatus EventStatus { get; set; } = QualityEventStatus.Draft;

    // ── Provenance: which FU aggregate raised this ───────────────────────────────────────────────────────
    public QualityEventSourceType SourceType { get; set; } = QualityEventSourceType.Manual;
    public Guid? SourceId { get; set; }

    public Guid? RegisterEntryId { get; set; }
    public Guid? ControlledDocumentId { get; set; }
    public Guid? TemplateVariantId { get; set; }
    public Guid? ExternalDocumentId { get; set; }

    // ── Detection ────────────────────────────────────────────────────────────────────────────────────────
    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? DetectedBy { get; set; }

    /// <summary>Mandatory for any non-manual source: a bridged event must say what evidence detected it.</summary>
    public string? DetectionEvidenceReference { get; set; }

    // ── Immediate containment (SOP: stop the bleeding before investigating) ──────────────────────────────
    public bool ImmediateContainmentRequired { get; set; }
    public string? ImmediateContainmentSummary { get; set; }

    // ── Follow-up requirements, set by the trigger mapper ────────────────────────────────────────────────
    public bool RequiresDeviation { get; set; }
    public bool RequiresCAPA { get; set; }
    public Guid? DeviationId { get; set; }
    public List<Guid> CAPAActionIds { get; set; } = [];

    /// <summary>
    /// Justification for NOT raising a deviation on a critical event. Recorded rather than forbidden, because a
    /// documented decision is auditable while a silent omission is not.
    /// </summary>
    public string? DeviationWaiverJustification { get; set; }
    public string? DeviationWaiverEvidenceReference { get; set; }

    /// <summary>EXTENSION POINT: the id of the corresponding record in a future external QMS. Never called by FU22.</summary>
    public string? ExternalQualitySystemReference { get; set; }

    public string? ClosureEvidenceReference { get; set; }
    public string? ClosureSummary { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public string? CancellationReason { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>An event is settled once closed or cancelled — used for bridge idempotency.</summary>
    public bool IsSettled() => EventStatus is QualityEventStatus.Closed or QualityEventStatus.Cancelled;
}
