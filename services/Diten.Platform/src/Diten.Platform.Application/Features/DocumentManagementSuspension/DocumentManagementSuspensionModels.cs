using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementSuspension;

// MOD-0029-FU13 — suspension / urgent withdrawal / retirement / temporary-instruction contracts, reason codes, options
// and wire mapping (GMG-QMS-SOP-0001 §12.1, §9.16, §6.1 class 7).

/// <summary>
/// MOD-0029-FU13 — RECOMMENDED Layer 1 RBAC keys. NOT seeded in this FU (no AuthService change): the controller reuses
/// the already-seeded controlled-documents create/view keys. FU06A hardening should seed these.
/// </summary>
public static class DocumentSuspensionPermissions
{
    public const string View = "platform.document-management.master-register.suspension.view";
    public const string Manage = "platform.document-management.master-register.suspension.manage";
    public const string Approve = "platform.document-management.master-register.suspension.approve";
    public const string RetirementApprove = "platform.document-management.master-register.retirement.approve";
}

public static class SuspensionReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string CaseNotFound = "CASE_NOT_FOUND";
    public const string NotEligible = "NOT_ELIGIBLE";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string EvidenceRequired = "EVIDENCE_REQUIRED";
    public const string CommunicationPlanRequired = "COMMUNICATION_PLAN_REQUIRED";
    public const string ApproverRoleInvalid = "APPROVER_ROLE_INVALID";
    public const string CaseNotApproved = "CASE_NOT_APPROVED";
    public const string DeviationRequired = "DEVIATION_REQUIRED";
    public const string TemporaryValidityExceeded = "TEMPORARY_VALIDITY_EXCEEDED";
    public const string NotTemporaryInstruction = "NOT_TEMPORARY_INSTRUCTION";
    public const string ExpiryActionRequired = "EXPIRY_ACTION_REQUIRED";
    public const string ReplacementRequired = "REPLACEMENT_REQUIRED";
    public const string LifecycleTransitionFailed = "LIFECYCLE_TRANSITION_FAILED";
    public const string PermissionDenied = "PERMISSION_DENIED";
}

/// <summary>
/// MOD-0029-FU13 — withdrawal policy. The 30-day ceiling is the SOP maximum for a temporary instruction (§6.1 class 7).
/// Section <c>DocumentManagement:Withdrawal</c>.
/// </summary>
public sealed class DocumentWithdrawalOptions
{
    public const string SectionName = "DocumentManagement:Withdrawal";

    public int TemporaryInstructionMaxValidityDays { get; set; } = 30;
    public int DueToExpireWarningDays { get; set; } = 5;
}

/// <summary>Only the GQD or an independent qualified QA delegate may approve a suspension/retirement (SOP §12.1, §5.1).</summary>
public static class SuspensionApprovers
{
    public static bool IsPermitted(ApprovalRequiredRole role) =>
        role is ApprovalRequiredRole.GQD or ApprovalRequiredRole.GQDDeputy or ApprovalRequiredRole.IndependentQASenior;
}

// ── inputs ───────────────────────────────────────────────────────────────────

public sealed record OpenSuspensionCaseInput(
    string TriggerType,
    string TriggerDescription,
    Guid? SourcePeriodicReviewEscalationId);

public sealed record EscalateSuspensionCaseInput(string? Comment);

public sealed record ApproveSuspensionInput(
    string Decision,
    string DecisionReason,
    string ApprovedByRole,
    string CommunicationPlanReference);

public sealed record RejectSuspensionInput(string Reason);

public sealed record ExecuteSuspensionInput(
    string SuspensionNoticeReference,
    string AccessRemovalEvidenceReference,
    string AffectedRecordsBatchesActivitiesReference);

public sealed record CloseSuspensionCaseInput(
    string? DeviationReference,
    string? CorrectiveActionReference,
    string? ReplacementPlanReference);

public sealed record RequestRetirementInput(
    string RetirementReason,
    string JustificationReference,
    string TransitionAssessmentReference,
    string? ReplacementDocumentUid,
    string? ReplacementDocumentCode);

public sealed record ApproveRetirementInput(string ApprovedByRole);

public sealed record RejectRetirementInput(string Reason);

public sealed record ExecuteRetirementInput(
    string CommunicationEvidenceReference,
    string ArchivalEvidenceReference);

public sealed record StartTemporaryInstructionInput(DateTimeOffset? ValidFrom, DateTimeOffset ValidUntil);

public sealed record CloseTemporaryInstructionInput(
    string ExpiryAction,
    string? ExpiryActionEvidenceReference,
    Guid? ReplacementRegisterEntryId);

// ── output models ────────────────────────────────────────────────────────────

public sealed record SuspensionCaseModel(
    Guid Id,
    Guid RegisterEntryId,
    int CaseNumber,
    string CaseStatus,
    string TriggerType,
    string TriggerDescription,
    DateTimeOffset ReportedAt,
    string? ReportedBy,
    DateTimeOffset? QaNotifiedAt,
    DateTimeOffset? EscalatedToGqdAt,
    DateTimeOffset? DocumentOwnerNotifiedAt,
    string? Decision,
    string? DecisionReason,
    string? ApprovedBy,
    string? ApprovedByRole,
    DateTimeOffset? ApprovedAt,
    string? CommunicationPlanReference,
    string? SuspensionNoticeReference,
    string? AccessRemovalEvidenceReference,
    string? AffectedRecordsBatchesActivitiesReference,
    string? DeviationReference,
    string? CorrectiveActionReference,
    string? ReplacementPlanReference,
    Guid? SourcePeriodicReviewEscalationId,
    DateTimeOffset? ExecutedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<string> Warnings);

public sealed record RetirementCaseModel(
    Guid Id,
    Guid RegisterEntryId,
    int CaseNumber,
    string CaseStatus,
    string RetirementReason,
    string JustificationReference,
    string TransitionAssessmentReference,
    string? CommunicationEvidenceReference,
    string? ArchivalEvidenceReference,
    string? ReplacementDocumentUid,
    string? ReplacementDocumentCode,
    string? ApprovedBy,
    string? ApprovedByRole,
    DateTimeOffset? ApprovedAt,
    string? RejectionReason,
    DateTimeOffset? ExecutedAt,
    IReadOnlyList<string> Warnings);

public sealed record TemporaryInstructionModel(
    Guid Id,
    Guid RegisterEntryId,
    string TemporaryInstructionStatus,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    int MaxValidityDays,
    int DaysUntilExpiry,
    string? ExpiryAction,
    string? ExpiryActionEvidenceReference,
    Guid? ReplacementRegisterEntryId,
    Guid? SuspensionCaseId,
    DateTimeOffset? CheckedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<string> Warnings);

public static class SuspensionWire
{
    public static SuspensionTriggerType? ParseTrigger(string? v) => Enum.TryParse<SuspensionTriggerType>(v, true, out var r) ? r : null;
    public static SuspensionDecision? ParseDecision(string? v) => Enum.TryParse<SuspensionDecision>(v, true, out var r) ? r : null;
    public static ApprovalRequiredRole? ParseRole(string? v) => Enum.TryParse<ApprovalRequiredRole>(v, true, out var r) ? r : null;
    public static TemporaryInstructionExpiryAction? ParseExpiryAction(string? v) => Enum.TryParse<TemporaryInstructionExpiryAction>(v, true, out var r) ? r : null;

    /// <summary>A quality/regulatory/data-integrity trigger requires a deviation or corrective action before closing (SOP §12.1).</summary>
    public static bool RequiresDeviation(SuspensionTriggerType trigger) =>
        trigger is SuspensionTriggerType.QualityRisk or SuspensionTriggerType.RegulatoryRisk or SuspensionTriggerType.DataIntegrityRisk;

    public static SuspensionCaseModel ToCase(DocumentSuspensionCase c, IReadOnlyList<string>? warnings = null) => new(
        c.Id, c.RegisterEntryId, c.CaseNumber, c.CaseStatus.ToString(), c.TriggerType.ToString(), c.TriggerDescription,
        c.ReportedAt, c.ReportedBy, c.QaNotifiedAt, c.EscalatedToGqdAt, c.DocumentOwnerNotifiedAt,
        c.Decision?.ToString(), c.DecisionReason, c.ApprovedBy, c.ApprovedByRole?.ToString(), c.ApprovedAt,
        c.CommunicationPlanReference, c.SuspensionNoticeReference, c.AccessRemovalEvidenceReference,
        c.AffectedRecordsBatchesActivitiesReference, c.DeviationReference, c.CorrectiveActionReference,
        c.ReplacementPlanReference, c.SourcePeriodicReviewEscalationId, c.ExecutedAt, c.ClosedAt, warnings ?? []);

    public static RetirementCaseModel ToRetirement(DocumentRetirementCase c, IReadOnlyList<string>? warnings = null) => new(
        c.Id, c.RegisterEntryId, c.CaseNumber, c.CaseStatus.ToString(), c.RetirementReason, c.JustificationReference,
        c.TransitionAssessmentReference, c.CommunicationEvidenceReference, c.ArchivalEvidenceReference,
        c.ReplacementDocumentUid, c.ReplacementDocumentCode, c.ApprovedBy, c.ApprovedByRole?.ToString(), c.ApprovedAt,
        c.RejectionReason, c.ExecutedAt, warnings ?? []);

    public static TemporaryInstructionModel ToTemporary(TemporaryInstructionControl t, DateTimeOffset now, IReadOnlyList<string>? warnings = null) => new(
        t.Id, t.RegisterEntryId, t.TemporaryInstructionStatus.ToString(), t.ValidFrom, t.ValidUntil, t.MaxValidityDays,
        (int)Math.Round((t.ValidUntil - now).TotalDays), t.ExpiryAction?.ToString(), t.ExpiryActionEvidenceReference,
        t.ReplacementRegisterEntryId, t.SuspensionCaseId, t.CheckedAt, t.ClosedAt, warnings ?? []);
}
