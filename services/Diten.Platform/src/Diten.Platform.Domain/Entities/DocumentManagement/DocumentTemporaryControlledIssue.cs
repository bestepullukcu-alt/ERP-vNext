using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU20 — a controlled document copy issued OUTSIDE the normal environment while the repository / DMS is
/// down (GMG-QMS-SOP-0001 §11.3).
///
/// THE CENTRAL SOP RISK THIS MANAGES: a temporary controlled issue must never silently become an uncontrolled
/// copy. That is prevented by three things held here — the outside-normal-environment approval (with its mechanism
/// and evidence), the recorded list of copies actually issued (linked to FU17
/// <see cref="DocumentControlledCopy"/> rows of type TemporaryControlledIssue), and a hard 3-working-day
/// reconciliation deadline after which the issue becomes Overdue and demands a deviation reference.
///
/// NOT FU13: <see cref="TemporaryInstructionControl"/> governs the 30-day validity of a temporary INSTRUCTION
/// DOCUMENT. This governs a temporary ISSUE of an already-effective controlled document during an outage.
///
/// BOUNDARIES: no e-signature is implemented or validated; <see cref="ApprovalMechanism"/> records what the
/// approver states was used. Deviation and corrective action are REFERENCES — FU20 implements no CAPA module.
/// Nothing here is ever hard-deleted.
/// </summary>
public sealed class DocumentTemporaryControlledIssue : TenantScopedEntity
{
    public required Guid DowntimeEventId { get; set; }
    public required Guid RegisterEntryId { get; set; }
    public Guid? ControlledDocumentId { get; set; }
    public Guid? ControlledDocumentVersionId { get; set; }

    public required string IssueNumber { get; set; }
    public TemporaryIssueStatus IssueStatus { get; set; } = TemporaryIssueStatus.Requested;
    public string? IssueReason { get; set; }

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? RequestedBy { get; set; }

    // ── Outside-normal-environment approval (SOP §11.3 — required BEFORE issue) ──────────────────────────
    public string? ApprovedBy { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByRole { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public OutsideNormalEnvironmentApprovalMechanism? ApprovalMechanism { get; set; }

    /// <summary>Mandatory to approve. A reference — never the signed document bytes.</summary>
    public string? ApprovalEvidenceReference { get; set; }

    // ── Issue ────────────────────────────────────────────────────────────────────────────────────────────
    public DateTimeOffset? IssuedAt { get; set; }
    public string? IssuedBy { get; set; }
    public int IssuedCopyCount { get; set; }
    public string? TemporaryLocationDescription { get; set; }

    public List<Guid> RecipientUserIds { get; set; } = [];
    public string? RecipientRole { get; set; }
    public string? RecipientDepartment { get; set; }

    /// <summary>FU17 controlled copy rows created for this issue (CopyType = TemporaryControlledIssue).</summary>
    public List<Guid> RelatedControlledCopyIds { get; set; } = [];

    // ── Reconciliation (SOP §11.3 — 3 working days) ──────────────────────────────────────────────────────
    public DateTimeOffset? ReconciliationDueDate { get; set; }
    public DateTimeOffset? ReconciledAt { get; set; }
    public string? ReconciledBy { get; set; }
    public string? ReconciliationEvidenceReference { get; set; }

    public string? MissingReconciliationReason { get; set; }

    /// <summary>Required to reconcile late — a missed deadline is a deviation. A reference only (no CAPA module).</summary>
    public string? DeviationReference { get; set; }
    public string? CorrectiveActionReference { get; set; }

    public string? CancellationReason { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>An issue is settled once reconciled or cancelled — the precondition for closing the downtime event.</summary>
    public bool IsSettled() => IssueStatus is TemporaryIssueStatus.Reconciled or TemporaryIssueStatus.Cancelled;
}
