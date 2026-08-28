using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU22 — a corrective / preventive action arising from a document-control quality event or deviation
/// (GMG-QMS-SOP-0001).
///
/// A FOUNDATION STATE MACHINE, NOT A WORKFLOW ENGINE: the action moves Draft → Open → InProgress → Completed →
/// (EffectivenessPending → Effective|Ineffective) → Closed. There is no MOD-0023 workflow runtime behind it, no
/// scheduler driving the effectiveness due date, and no e-signature on completion — completion and effectiveness
/// are attested by an evidence REFERENCE recorded by a human.
///
/// THE KEY GUARANTEE: an action whose effectiveness check is required cannot be closed until the effectiveness
/// verdict is recorded, and an action found <see cref="CapaEffectivenessResult.Ineffective"/> can never be closed
/// as effective — closing it at all requires a documented exception, and the parent deviation is pushed back to
/// CAPARequired so the failure is not simply absorbed. Never hard-deleted.
/// </summary>
public sealed class DocumentCAPAAction : TenantScopedEntity
{
    public required string CAPANumber { get; set; }

    // At least one linkage is mandatory (enforced by the service): an orphan action has no context.
    public Guid? QualityEventId { get; set; }
    public Guid? DeviationId { get; set; }

    public CapaActionType ActionType { get; set; } = CapaActionType.CorrectiveAction;
    public required string ActionTitle { get; set; }
    public required string ActionDescription { get; set; }
    public CapaActionStatus ActionStatus { get; set; } = CapaActionStatus.Draft;

    public Guid? ActionOwnerUserId { get; set; }
    public string? ActionOwnerRole { get; set; }

    /// <summary>Mandatory for a corrective/preventive action; an undated commitment is not a commitment.</summary>
    public DateTimeOffset? DueDate { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public string? StartedBy { get; set; }

    /// <summary>Mandatory to complete. A reference — never the completion document bytes.</summary>
    public string? CompletionEvidenceReference { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }

    // ── Effectiveness check ──────────────────────────────────────────────────────────────────────────────
    public bool EffectivenessCheckRequired { get; set; }
    public DateTimeOffset? EffectivenessDueDate { get; set; }
    public string? EffectivenessEvidenceReference { get; set; }
    public CapaEffectivenessResult EffectivenessResult { get; set; } = CapaEffectivenessResult.NotRequired;
    public string? EffectivenessSummary { get; set; }
    public DateTimeOffset? EffectivenessRecordedAt { get; set; }
    public string? EffectivenessRecordedBy { get; set; }

    // ── What the action touches ──────────────────────────────────────────────────────────────────────────
    public List<Guid> RelatedRegisterEntryIds { get; set; } = [];
    public List<Guid> RelatedControlledDocumentIds { get; set; } = [];
    public List<Guid> RelatedExternalDocumentIds { get; set; } = [];

    // ── Closure ──────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>Documented basis for closing an ineffective or incomplete action.</summary>
    public string? ClosureExceptionJustification { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public string? CancellationReason { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// The action is DISCHARGED for the purposes of closing its parent deviation or quality event. An effective
    /// action counts here even before it is formally closed — the corrective work is demonstrably done.
    /// </summary>
    public bool IsSettled() =>
        ActionStatus is CapaActionStatus.Effective or CapaActionStatus.Closed or CapaActionStatus.Cancelled;

    /// <summary>
    /// The action can take no further transition. Deliberately NARROWER than <see cref="IsSettled"/>: an
    /// Effective action is settled but still closable, so closure/cancellation must test this, not IsSettled.
    /// </summary>
    public bool IsTerminal() =>
        ActionStatus is CapaActionStatus.Closed or CapaActionStatus.Cancelled;
}
