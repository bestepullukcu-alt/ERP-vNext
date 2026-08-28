using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU15 — a request to dispose of a regulated record once its retention period has elapsed
/// (GMG-QMS-SOP-0001 §22).
///
/// CRITICAL BOUNDARY: this aggregate NEVER causes a deletion. Its terminal state is
/// <see cref="DispositionRequestStatus.ExecutedAsNoDeleteMarker"/> — an evidence marker recording that disposition
/// was authorised and executed as a governance decision, while the subject record itself remains fully intact in
/// the database. Actual purge is deliberately out of scope for FU15 and is a separate, future task that would
/// consume these markers as its input.
///
/// Guards enforced by the service layer: an active legal hold blocks submit/approve/execute; a subject that is not
/// evaluated-eligible cannot be submitted; execution requires approval evidence.
/// </summary>
public sealed class DocumentDispositionRequest : TenantScopedEntity
{
    public required string RequestNumber { get; set; }

    public RetentionSubjectType SubjectType { get; set; } = RetentionSubjectType.Other;
    public required Guid SubjectId { get; set; }
    public Guid? RegisterEntryId { get; set; }
    public Guid? PolicyId { get; set; }

    public DispositionRequestStatus RequestStatus { get; set; } = DispositionRequestStatus.Draft;

    // ── Eligibility verdict captured at check time (never re-derived silently) ───────────────────────────
    public DateTimeOffset? EligibilityCheckedAt { get; set; }
    public DispositionEligibilityResult EligibilityResult { get; set; } = DispositionEligibilityResult.NotEligible;

    public string? RequestedBy { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    // ── Approval ─────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>Mandatory to approve/execute. A reference — never the approval document bytes.</summary>
    public string? ApprovalEvidenceReference { get; set; }
    public string? ApprovedBy { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    // ── Execution — marker only, no deletion ─────────────────────────────────────────────────────────────
    public string? ExecutionEvidenceReference { get; set; }
    public DateTimeOffset? ExecutedAt { get; set; }
    public string? ExecutedBy { get; set; }

    public string? Comment { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
