using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementPeriodicReview;

// MOD-0029-FU12 — periodic review / extension / overdue contracts, reason codes, options and wire mapping
// (GMG-QMS-SOP-0001 §9.15, §15).

/// <summary>
/// MOD-0029-FU12 — RECOMMENDED Layer 1 RBAC keys. NOT seeded in this FU (no AuthService change): the controller reuses
/// the already-seeded controlled-documents create/view keys. FU06A hardening should seed these.
/// </summary>
public static class DocumentPeriodicReviewPermissions
{
    public const string View = "platform.document-management.master-register.periodic-review.view";
    public const string Manage = "platform.document-management.master-register.periodic-review.manage";
    public const string ApproveExtension = "platform.document-management.master-register.periodic-review.approve-extension";
    public const string EscalationView = "platform.document-management.master-register.periodic-review.escalation.view";
}

public static class PeriodicReviewReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string ReviewNotFound = "REVIEW_NOT_FOUND";
    public const string ExtensionNotFound = "EXTENSION_NOT_FOUND";
    public const string NotScheduledForReview = "NOT_SCHEDULED_FOR_REVIEW";
    public const string ScheduleIncomplete = "REVIEW_SCHEDULE_INCOMPLETE";
    public const string EvidenceRequired = "EVIDENCE_REQUIRED";
    public const string ImpactAssessmentRequired = "IMPACT_ASSESSMENT_REQUIRED";
    public const string RiskAssessmentRequired = "RISK_ASSESSMENT_REQUIRED";
    public const string ReviewAlreadyOverdue = "REVIEW_ALREADY_OVERDUE";
    public const string ExtensionAlreadyUsed = "EXTENSION_ALREADY_USED";
    public const string ExtensionTooLong = "EXTENSION_TOO_LONG";
    public const string GqdApprovalRequired = "GQD_APPROVAL_REQUIRED";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string PermissionDenied = "PERMISSION_DENIED";
}

/// <summary>
/// MOD-0029-FU12 — periodic review policy. Defaults follow SOP §9.15 (60-day initiation window, 60-day maximum single
/// extension). <see cref="AutoTransitionOnReviseDecision"/> stays FALSE: a review DECISION recommends a lifecycle
/// action; it never silently transitions the document (that stays with the FU08 engine).
/// Section <c>DocumentManagement:PeriodicReview</c>.
/// </summary>
public sealed class DocumentPeriodicReviewOptions
{
    public const string SectionName = "DocumentManagement:PeriodicReview";

    public int InitiationWindowDays { get; set; } = 60;
    public int MaxExtensionDays { get; set; } = 60;
    public bool AutoTransitionOnReviseDecision { get; set; }
}

// ── inputs ───────────────────────────────────────────────────────────────────

public sealed record CompletePeriodicReviewInput(
    string Decision,
    string ReviewEvidenceReference,
    string? ImpactAssessmentReference,
    string? Comment);

public sealed record RequestPeriodicReviewExtensionInput(
    int ExtensionDays,
    string RiskAssessmentReference,
    string? Justification);

public sealed record ApprovePeriodicReviewExtensionInput(
    string ApproverRole,
    bool ManagementReviewEscalated,
    string? Comment);

public sealed record RejectPeriodicReviewExtensionInput(string Reason);

// ── output models ────────────────────────────────────────────────────────────

public sealed record PeriodicReviewModel(
    Guid Id,
    Guid RegisterEntryId,
    int ReviewNumber,
    string ReviewStatus,
    DateTimeOffset ReviewDueDate,
    DateTimeOffset InitiationWindowStartDate,
    DateTimeOffset? InitiatedAt,
    string? InitiatedBy,
    DateTimeOffset? CompletedAt,
    string? CompletedBy,
    string? ReviewDecision,
    string? ReviewEvidenceReference,
    string? ImpactAssessmentReference,
    string? Comment);

public sealed record PeriodicReviewExtensionModel(
    Guid Id,
    Guid PeriodicReviewId,
    int ExtensionNumber,
    string Status,
    DateTimeOffset RequestedAt,
    string? RequestedBy,
    DateTimeOffset? ApprovedAt,
    string? ApprovedBy,
    string? ApproverRole,
    DateTimeOffset OriginalDueDate,
    DateTimeOffset ExtendedDueDate,
    int ExtensionDays,
    string RiskAssessmentReference,
    string? Justification,
    bool ManagementReviewEscalated,
    string? RejectionReason);

public sealed record PeriodicReviewEscalationModel(
    Guid Id,
    Guid RegisterEntryId,
    Guid PeriodicReviewId,
    string EscalationType,
    string Severity,
    string Status,
    string RequiredRole,
    string Description,
    DateTimeOffset? DueAt);

/// <summary>MOD-0029-FU12 — the computed review schedule/state for a register entry (read-only projection).</summary>
public sealed record PeriodicReviewScheduleModel(
    Guid RegisterEntryId,
    int? ReviewCycleMonths,
    DateTimeOffset? LastPeriodicReviewDate,
    DateTimeOffset? NextReviewDueDate,
    int? DaysUntilDue,
    DateTimeOffset? InitiationWindowStartDate,
    string ReviewStatus,
    bool IsDueSoon,
    bool IsOverdue,
    bool HasOpenExtension,
    bool ExtensionUsed,
    bool CanInitiate,
    bool CanRequestExtension,
    bool CanComplete,
    bool RequiresGqdEscalation,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> WarningReasons,
    PeriodicReviewModel? CurrentReview);

public static class PeriodicReviewWire
{
    public static PeriodicReviewDecision? ParseDecision(string? v) =>
        Enum.TryParse<PeriodicReviewDecision>(v, true, out var r) ? r : null;

    public static ReviewEscalationRole? ParseRole(string? v) =>
        Enum.TryParse<ReviewEscalationRole>(v, true, out var r) ? r : null;

    public static PeriodicReviewModel ToReview(DocumentPeriodicReview r) => new(
        r.Id, r.RegisterEntryId, r.ReviewNumber, r.ReviewStatus.ToString(), r.ReviewDueDate, r.InitiationWindowStartDate,
        r.InitiatedAt, r.InitiatedBy, r.CompletedAt, r.CompletedBy, r.ReviewDecision?.ToString(),
        r.ReviewEvidenceReference, r.ImpactAssessmentReference, r.Comment);

    public static PeriodicReviewExtensionModel ToExtension(DocumentPeriodicReviewExtension e) => new(
        e.Id, e.PeriodicReviewId, e.ExtensionNumber, e.Status.ToString(), e.RequestedAt, e.RequestedBy,
        e.ApprovedAt, e.ApprovedBy, e.ApproverRole?.ToString(), e.OriginalDueDate, e.ExtendedDueDate, e.ExtensionDays,
        e.RiskAssessmentReference, e.Justification, e.ManagementReviewEscalated, e.RejectionReason);

    public static PeriodicReviewEscalationModel ToEscalation(DocumentPeriodicReviewEscalation e) => new(
        e.Id, e.RegisterEntryId, e.PeriodicReviewId, e.EscalationType.ToString(), e.Severity.ToString(),
        e.Status.ToString(), e.RequiredRole.ToString(), e.Description, e.DueAt);
}
