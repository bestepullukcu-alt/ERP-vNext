using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU22 — the traceable link between a quality event and the FU aggregate that raised it
/// (GMG-QMS-SOP-0001).
///
/// WHY A LINK TABLE RATHER THAN A FOREIGN KEY ON THE SOURCE: the source aggregates (FU13 suspension cases, FU17
/// obsolete copy findings, FU20 temporary issues, FU21 corrections, FU14 impact assessments) already exist and are
/// covered by their own tests. Adding a quality-event id column to each of them would mean touching five features'
/// entities and update paths. This link table records the same relationship additively, from the FU22 side, and
/// leaves every existing string reference field exactly as it is.
///
/// It is also what makes the bridge IDEMPOTENT: re-running a detection that already raised an event finds the
/// existing link instead of creating a second event for the same finding. Links are closed, never deleted.
/// </summary>
public sealed class DocumentQualityEventSourceLink : TenantScopedEntity
{
    public required Guid QualityEventId { get; set; }

    public QualityEventSourceType SourceType { get; set; } = QualityEventSourceType.Manual;
    public required Guid SourceId { get; set; }

    public Guid? RegisterEntryId { get; set; }

    /// <summary>The event type this source raised — part of the idempotency key.</summary>
    public QualityEventType EventType { get; set; } = QualityEventType.Other;

    public QualityEventSourceLinkStatus LinkStatus { get; set; } = QualityEventSourceLinkStatus.Active;

    /// <summary>The pre-existing free-text reference carried by the source, preserved for traceability.</summary>
    public string? SourceReferenceSnapshot { get; set; }

    public string? Notes { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
