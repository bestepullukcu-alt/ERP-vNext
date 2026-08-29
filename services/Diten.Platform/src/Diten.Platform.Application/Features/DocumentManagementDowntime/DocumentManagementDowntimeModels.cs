using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementDowntime;

// MOD-0029-FU20 — repository downtime / temporary controlled issue contracts, reason codes and wire mapping
// (GMG-QMS-SOP-0001 §11.3).

/// <summary>
/// MOD-0029-FU20 — RECOMMENDED Layer 1 RBAC keys. NOT seeded in this FU (no AuthService change): the controller
/// reuses the already-seeded controlled-documents view/create keys. A later hardening FU should seed these —
/// approving an outside-normal-environment issue is a materially different authority from creating a document.
/// </summary>
public static class DowntimePermissions
{
    public const string View = "platform.document-management.downtime.view";
    public const string Manage = "platform.document-management.downtime.manage";
    public const string TemporaryIssue = "platform.document-management.downtime.temporary-issue";
    public const string Reconcile = "platform.document-management.downtime.reconcile";
}

public static class DowntimeReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string DowntimeNotFound = "DOWNTIME_EVENT_NOT_FOUND";
    public const string IssueNotFound = "TEMPORARY_ISSUE_NOT_FOUND";
    public const string RegisterEntryNotFound = "REGISTER_ENTRY_NOT_FOUND";
    public const string DetectionEvidenceRequired = "DETECTION_EVIDENCE_REQUIRED";
    public const string RestoreEvidenceRequired = "RESTORE_EVIDENCE_REQUIRED";
    public const string StartedAtInFuture = "DOWNTIME_STARTED_AT_IN_FUTURE";
    public const string DowntimeNotAcceptingIssues = "DOWNTIME_NOT_ACCEPTING_ISSUES";
    public const string DowntimeAlreadyRestored = "DOWNTIME_ALREADY_RESTORED";
    public const string DowntimeAlreadyClosed = "DOWNTIME_ALREADY_CLOSED";
    public const string BcpAssessmentRequired = "BCP_ASSESSMENT_REFERENCE_REQUIRED";
    public const string UnsettledIssuesBlockClose = "UNSETTLED_TEMPORARY_ISSUES_BLOCK_CLOSE";

    public const string DocumentNotOperational = "DOCUMENT_NOT_OPERATIONALLY_EFFECTIVE";
    public const string UnapprovedRepositoryBlocked = "UNAPPROVED_REPOSITORY_CANNOT_ISSUE_CONTROLLED_COPY";
    public const string ApprovalMechanismRequired = "APPROVAL_MECHANISM_REQUIRED";
    public const string ApprovalEvidenceRequired = "APPROVAL_EVIDENCE_REQUIRED";
    public const string ApproverRoleInvalid = "APPROVER_ROLE_INVALID";
    public const string IssueNotApproved = "TEMPORARY_ISSUE_NOT_APPROVED";
    public const string CopyCountInvalid = "ISSUED_COPY_COUNT_MUST_BE_POSITIVE";
    public const string TemporaryLocationRequired = "TEMPORARY_LOCATION_REQUIRED";
    public const string ReconciliationEvidenceRequired = "RECONCILIATION_EVIDENCE_REQUIRED";
    public const string DeviationReferenceRequired = "LATE_RECONCILIATION_DEVIATION_REFERENCE_REQUIRED";
    public const string IssueInvalidState = "TEMPORARY_ISSUE_INVALID_STATE";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string PermissionDenied = "PERMISSION_DENIED";
}

/// <summary>
/// SOP §11.3 — who may approve a controlled issue made outside the normal environment. Deliberately narrow.
/// </summary>
public static class TemporaryIssueApprovers
{
    public static bool IsPermitted(ApprovalRequiredRole role) =>
        role is ApprovalRequiredRole.GQD or ApprovalRequiredRole.GQDDeputy
            or ApprovalRequiredRole.QADocumentation or ApprovalRequiredRole.ITCSVOwner
            or ApprovalRequiredRole.LocalQA;
}

// ── inputs ───────────────────────────────────────────────────────────────────

public sealed record OpenDowntimeEventInput(
    string DetectionEvidenceReference,
    string? DowntimeType,
    Guid? RepositoryAssessmentId,
    string? RepositoryName,
    DateTimeOffset? StartedAt,
    Guid? DetectedByUserId,
    string? ImpactSummary);

public sealed record MarkRepositoryRestoredInput(
    string RestoreEvidenceReference,
    DateTimeOffset? RestoredAt);

public sealed record CloseDowntimeEventInput(
    string? BcpAssessmentReference,
    string? ClosureNote);

public sealed record RequestTemporaryIssueInput(
    Guid RegisterEntryId,
    Guid? ControlledDocumentId,
    Guid? ControlledDocumentVersionId,
    string? IssueReason,
    string? RecipientRole,
    string? RecipientDepartment,
    IReadOnlyList<Guid>? RecipientUserIds);

public sealed record ApproveTemporaryIssueInput(
    string ApprovedByRole,
    string ApprovalMechanism,
    string ApprovalEvidenceReference,
    Guid? ApprovedByUserId);

public sealed record IssueTemporaryControlledCopyInput(
    int IssuedCopyCount,
    string TemporaryLocationDescription,
    string? LocationType);

public sealed record ReconcileTemporaryIssueInput(
    string ReconciliationEvidenceReference,
    string? DeviationReference,
    string? CorrectiveActionReference,
    string? MissingReconciliationReason,
    bool WithdrawCopiesInsteadOfReconcile = false);

public sealed record CancelTemporaryIssueInput(string Reason);

// ── output models ────────────────────────────────────────────────────────────

public sealed record DowntimeEventModel(
    Guid Id,
    string DowntimeNumber,
    Guid? RepositoryAssessmentId,
    string? RepositoryName,
    string DowntimeStatus,
    string DowntimeType,
    DateTimeOffset StartedAt,
    string? StartedBy,
    Guid? DetectedByUserId,
    string DetectionEvidenceReference,
    string? ImpactSummary,
    DateTimeOffset? RestoredAt,
    string? RestoredBy,
    string? RestoreEvidenceReference,
    int? DurationWorkingDays,
    bool RequiresGqdItCsvEscalation,
    DateTimeOffset? EscalatedAt,
    string? EscalationEvidenceReference,
    string? BcpAssessmentReference,
    DateTimeOffset? ClosedAt,
    string? ClosedBy,
    string? ClosureNote,
    string RepositoryBoundaryStatement,
    string BoundaryStatement);

public sealed record TemporaryControlledIssueModel(
    Guid Id,
    Guid DowntimeEventId,
    Guid RegisterEntryId,
    Guid? ControlledDocumentId,
    Guid? ControlledDocumentVersionId,
    string IssueNumber,
    string IssueStatus,
    string? IssueReason,
    DateTimeOffset RequestedAt,
    string? RequestedBy,
    string? ApprovedBy,
    string? ApprovedByRole,
    DateTimeOffset? ApprovedAt,
    string? ApprovalMechanism,
    string? ApprovalEvidenceReference,
    DateTimeOffset? IssuedAt,
    string? IssuedBy,
    int IssuedCopyCount,
    string? TemporaryLocationDescription,
    IReadOnlyList<Guid> RecipientUserIds,
    string? RecipientRole,
    string? RecipientDepartment,
    IReadOnlyList<Guid> RelatedControlledCopyIds,
    DateTimeOffset? ReconciliationDueDate,
    DateTimeOffset? ReconciledAt,
    string? ReconciledBy,
    string? ReconciliationEvidenceReference,
    string? MissingReconciliationReason,
    string? DeviationReference,
    string? CorrectiveActionReference,
    bool IsOverdue,
    string BoundaryStatement);

public sealed record DowntimeEscalationModel(
    Guid Id,
    Guid DowntimeEventId,
    Guid? TemporaryControlledIssueId,
    string EscalationType,
    string Severity,
    string RequiredRole,
    string Status,
    string Description,
    string? EvidenceReference,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? ResolvedAt);

/// <summary>MOD-0029-FU20 — the outcome of an escalation evaluation run for one downtime event.</summary>
public sealed record DowntimeEscalationEvaluationModel(
    Guid DowntimeEventId,
    int DurationWorkingDays,
    bool ExceedsTwoWorkingDays,
    bool RequiresBcpAssessment,
    IReadOnlyList<DowntimeEscalationModel> Escalations);

public static class DowntimeWire
{
    /// <summary>Stated on every read so the module's limits are never misread as a validation claim.</summary>
    public const string BoundaryStatement =
        "Downtime and temporary controlled issue governance: metadata and evidence references only. " +
        "MOD-0029-FU20 implements no e-signature, no qualified electronic signature provider, no CAPA/Quality " +
        "Event module and no BCP module — deviation, corrective action and BCP assessment are recorded as " +
        "references to records held elsewhere.";

    public static DowntimeType ParseDowntimeType(string? v) =>
        Enum.TryParse<DowntimeType>(v, true, out var r) ? r : DowntimeType.UnplannedOutage;

    public static OutsideNormalEnvironmentApprovalMechanism? ParseMechanism(string? v) =>
        Enum.TryParse<OutsideNormalEnvironmentApprovalMechanism>(v, true, out var r) ? r : null;

    public static ApprovalRequiredRole? ParseApproverRole(string? v) =>
        Enum.TryParse<ApprovalRequiredRole>(v, true, out var r) ? r : null;

    public static ControlledCopyLocationType ParseLocationType(string? v) =>
        Enum.TryParse<ControlledCopyLocationType>(v, true, out var r) ? r : ControlledCopyLocationType.PointOfUse;

    /// <summary>
    /// SOP §11.2/§11.3 — states what the linked repository can and cannot support. A native interim repository is
    /// NEVER presented as a validated DMS, and no e-signature capability is ever claimed by FU20.
    /// </summary>
    public static string RepositoryBoundary(RepositoryType? repositoryType) => repositoryType switch
    {
        RepositoryType.ValidatedDms =>
            "Linked repository is assessed as a validated DMS. FU20 still makes no e-signature claim: the recorded " +
            "approval mechanism is a statement by the approver, not a platform-verified signature.",
        RepositoryType.ApprovedInterimRepository =>
            "Linked repository is an approved INTERIM repository. It cannot be presented as a validated DMS and " +
            "cannot support regulated electronic signature; approvals must rely on wet signature or a separate " +
            "approved mechanism.",
        RepositoryType.SeparateApprovalMechanism =>
            "Linked repository relies on a separate approval mechanism. No validated DMS or e-signature capability " +
            "is claimed.",
        RepositoryType.UnapprovedRepository =>
            "Linked repository is UNAPPROVED. Controlled copies must not be issued from it: doing so would create " +
            "uncontrolled copies. Approve a repository assessment (FU16) before issuing.",
        _ =>
            "No repository assessment is linked to this downtime event, so no repository capability is claimed."
    };

    public static DowntimeEventModel ToEvent(DocumentRepositoryDowntimeEvent e, RepositoryType? repositoryType) => new(
        e.Id, e.DowntimeNumber, e.RepositoryAssessmentId, e.RepositoryName, e.DowntimeStatus.ToString(),
        e.DowntimeType.ToString(), e.StartedAt, e.StartedBy, e.DetectedByUserId, e.DetectionEvidenceReference,
        e.ImpactSummary, e.RestoredAt, e.RestoredBy, e.RestoreEvidenceReference, e.DurationWorkingDays,
        e.RequiresGqdItCsvEscalation, e.EscalatedAt, e.EscalationEvidenceReference, e.BcpAssessmentReference,
        e.ClosedAt, e.ClosedBy, e.ClosureNote, RepositoryBoundary(repositoryType), BoundaryStatement);

    public static TemporaryControlledIssueModel ToIssue(DocumentTemporaryControlledIssue i, DateTimeOffset now) => new(
        i.Id, i.DowntimeEventId, i.RegisterEntryId, i.ControlledDocumentId, i.ControlledDocumentVersionId,
        i.IssueNumber, i.IssueStatus.ToString(), i.IssueReason, i.RequestedAt, i.RequestedBy, i.ApprovedBy,
        i.ApprovedByRole, i.ApprovedAt, i.ApprovalMechanism?.ToString(), i.ApprovalEvidenceReference, i.IssuedAt,
        i.IssuedBy, i.IssuedCopyCount, i.TemporaryLocationDescription, i.RecipientUserIds.ToList(), i.RecipientRole,
        i.RecipientDepartment, i.RelatedControlledCopyIds.ToList(), i.ReconciliationDueDate, i.ReconciledAt,
        i.ReconciledBy, i.ReconciliationEvidenceReference, i.MissingReconciliationReason, i.DeviationReference,
        i.CorrectiveActionReference,
        i.IssueStatus is not (TemporaryIssueStatus.Reconciled or TemporaryIssueStatus.Cancelled)
            && i.ReconciliationDueDate is { } due && now > due,
        BoundaryStatement);

    public static DowntimeEscalationModel ToEscalation(DocumentDowntimeEscalation e) => new(
        e.Id, e.DowntimeEventId, e.TemporaryControlledIssueId, e.EscalationType.ToString(), e.Severity.ToString(),
        e.RequiredRole.ToString(), e.Status.ToString(), e.Description, e.EvidenceReference, e.AcknowledgedAt,
        e.ResolvedAt);
}
