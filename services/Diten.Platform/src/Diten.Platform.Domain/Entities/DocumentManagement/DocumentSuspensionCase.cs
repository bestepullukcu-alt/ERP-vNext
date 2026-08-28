using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU13 — an urgent-withdrawal / suspension case for a Document Master Register entry (GMG-QMS-SOP-0001
/// §12.1). Records the full chain: any user stops use and notifies QA → QA escalates to the GQD and the Document Owner
/// the same working day → the GQD (or an independent QA delegate) approves the suspension, temporary instruction and
/// communication plan within 1 working day → QA/Local QA removes access, issues the notice and identifies affected
/// records/batches/activities → a deviation and replacement/corrective action are opened within 5 working days.
///
/// This is a GOVERNANCE + EVIDENCE record, not a CAPA module and not a workflow engine: the deviation / corrective
/// action are captured as REFERENCES (extension points for a future quality-event/CAPA module). Executing the case
/// delegates the lifecycle change to the FU08 engine. Never hard-deleted.
/// </summary>
public sealed class DocumentSuspensionCase : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }

    /// <summary>1-based case number for this entry.</summary>
    public int CaseNumber { get; set; }

    public SuspensionCaseStatus CaseStatus { get; set; } = SuspensionCaseStatus.Opened;

    public SuspensionTriggerType TriggerType { get; set; }
    public required string TriggerDescription { get; set; }

    public DateTimeOffset ReportedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ReportedBy { get; set; }

    public DateTimeOffset? QaNotifiedAt { get; set; }
    public DateTimeOffset? EscalatedToGqdAt { get; set; }
    public DateTimeOffset? DocumentOwnerNotifiedAt { get; set; }

    public SuspensionDecision? Decision { get; set; }
    public string? DecisionReason { get; set; }
    public string? ApprovedBy { get; set; }
    public ApprovalRequiredRole? ApprovedByRole { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    // Execution evidence (SOP §12.1).
    public string? CommunicationPlanReference { get; set; }
    public string? SuspensionNoticeReference { get; set; }
    public string? AccessRemovalEvidenceReference { get; set; }
    public string? AffectedRecordsBatchesActivitiesReference { get; set; }
    public DateTimeOffset? ExecutedAt { get; set; }
    public string? ExecutedBy { get; set; }

    // Quality-event extension points (a full CAPA / quality-event module is out of scope).
    public string? DeviationReference { get; set; }
    public string? CorrectiveActionReference { get; set; }
    public string? ReplacementPlanReference { get; set; }

    /// <summary>Links the case back to the FU12 escalation (overdue / extension-expired / GQD determination) that triggered it.</summary>
    public Guid? SourcePeriodicReviewEscalationId { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
