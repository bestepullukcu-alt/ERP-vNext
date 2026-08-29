using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU12 — a formal periodic-review extension (GMG-QMS-SOP-0001 §9.15). An extension requires a documented risk
/// assessment, a MAXIMUM of 60 calendar days, and approval BEFORE the due date (GQD for a Critical document, QA
/// otherwise). ONE extension only — a second is never permitted, and an extension applied for after the due date is not
/// an extension (the review is simply overdue). Rejected/expired rows are retained as history; never hard-deleted.
/// </summary>
public sealed class DocumentPeriodicReviewExtension : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }
    public required Guid PeriodicReviewId { get; set; }

    /// <summary>Always 1 in practice — a second extension is not permitted (SOP §9.15).</summary>
    public int ExtensionNumber { get; set; } = 1;

    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? RequestedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public ReviewEscalationRole? ApproverRole { get; set; }

    public DateTimeOffset OriginalDueDate { get; set; }
    public DateTimeOffset ExtendedDueDate { get; set; }
    public int ExtensionDays { get; set; }

    /// <summary>Mandatory — a documented risk assessment of continued use of the current version.</summary>
    public required string RiskAssessmentReference { get; set; }
    public string? Justification { get; set; }

    /// <summary>Critical-document extensions are escalated to Management Review (SOP §9.15).</summary>
    public bool ManagementReviewEscalated { get; set; }

    public PeriodicReviewExtensionStatus Status { get; set; } = PeriodicReviewExtensionStatus.Requested;
    public string? RejectionReason { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
