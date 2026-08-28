using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment;

// MOD-0029-FU16 — repository assessment / DMS boundary contracts, reason codes and wire mapping (SOP §11, §11.1, §11.2).

/// <summary>
/// MOD-0029-FU16 — RECOMMENDED Layer 1 RBAC keys. NOT seeded in this FU (no AuthService change): the controller reuses
/// the already-seeded controlled-documents create/view keys. FU06A hardening should seed these.
/// </summary>
public static class DocumentRepositoryAssessmentPermissions
{
    public const string View = "platform.document-management.repository-assessment.view";
    public const string Manage = "platform.document-management.repository-assessment.manage";
    public const string Approve = "platform.document-management.repository-assessment.approve";
}

public static class RepositoryAssessmentReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string AssessmentNotFound = "ASSESSMENT_NOT_FOUND";
    public const string NameAndTypeRequired = "NAME_AND_TYPE_REQUIRED";
    public const string RequiredFieldsMissing = "REQUIRED_FIELDS_MISSING";
    public const string AlreadyDecided = "ALREADY_DECIDED";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string ApproverRoleInvalid = "APPROVER_ROLE_INVALID";
    public const string LinkStatusInvalid = "LINK_STATUS_INVALID";
    public const string PermissionDenied = "PERMISSION_DENIED";
}

/// <summary>SOP §11.2: a DMS / e-signature / CSV procedure requires GQD + IT/CSV technical approval.</summary>
public static class RepositoryAssessmentApprovers
{
    public static bool IsPermitted(ApprovalRequiredRole role) =>
        role is ApprovalRequiredRole.GQD or ApprovalRequiredRole.GQDDeputy or ApprovalRequiredRole.ITCSVOwner;
}

// ── inputs ───────────────────────────────────────────────────────────────────

/// <summary>All editable assessment fields. Used for create and update.</summary>
public sealed record RepositoryAssessmentFieldsInput(
    string RepositoryName,
    string RepositoryType,
    string? LocationType,
    Guid? RepositoryOwnerUserId,
    string? RepositoryOwnerRole,
    string? ExactLocation,
    string? AccessModelDescription,
    string? AccessReviewFrequency,
    string? BackupMethodDescription,
    string? RestoreTestFrequency,
    string? ApprovalMechanismDescription,
    string? EffectiveCopyControlDescription,
    string? AuditTrailDescription,
    string? ChangeControlDescription,
    string? ValidationEvidenceReference,
    int? MaxInterimPeriodDays,
    DateTimeOffset? InterimCheckpointDueDate,
    bool MigrationReconciliationRequired,
    string? MigrationReconciliationReference,
    string? AssessmentEvidenceReference);

public sealed record ApproveRepositoryAssessmentInput(string ApprovedByRole, DateTimeOffset? ValidUntil);

public sealed record RejectRepositoryAssessmentInput(string Reason);

public sealed record LinkRepositoryAssessmentInput(Guid RepositoryAssessmentId);

// ── output models ────────────────────────────────────────────────────────────

public sealed record RepositoryAssessmentModel(
    Guid Id,
    string RepositoryKey,
    string RepositoryName,
    string RepositoryType,
    string AssessmentStatus,
    Guid? RepositoryOwnerUserId,
    string? RepositoryOwnerRole,
    string? ExactLocation,
    string LocationType,
    string? AccessModelDescription,
    string? AccessReviewFrequency,
    string? BackupMethodDescription,
    string? RestoreTestFrequency,
    string? ApprovalMechanismDescription,
    string? EffectiveCopyControlDescription,
    string? AuditTrailDescription,
    string? ChangeControlDescription,
    string? ValidationEvidenceReference,
    int? MaxInterimPeriodDays,
    DateTimeOffset? InterimCheckpointDueDate,
    bool MigrationReconciliationRequired,
    string? MigrationReconciliationReference,
    string? AssessmentEvidenceReference,
    string? ApprovedByRole,
    DateTimeOffset? ApprovedAt,
    string? RejectionReason,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil);

public sealed record RepositoryAssessmentFindingModel(
    Guid Id,
    Guid RepositoryAssessmentId,
    string FindingKey,
    string FindingType,
    string Severity,
    string Status,
    string Description,
    string? EvidenceReference);

public sealed record RepositoryAssessmentReadinessModel(
    Guid RepositoryAssessmentId,
    string RepositoryType,
    string AssessmentStatus,
    bool Ready,
    bool CanSupportReleaseGate,
    bool CanSupportRegulatedESignature,
    string BoundaryStatement,
    IReadOnlyList<RepositoryAssessmentFindingModel> BlockingFindings,
    IReadOnlyList<RepositoryAssessmentFindingModel> WarningFindings);

public static class RepositoryAssessmentWire
{
    public static RepositoryType? ParseType(string? v) => Enum.TryParse<RepositoryType>(v, true, out var r) ? r : null;
    public static RepositoryLocationType ParseLocation(string? v) => Enum.TryParse<RepositoryLocationType>(v, true, out var r) ? r : RepositoryLocationType.InHouseSoftware;
    public static ApprovalRequiredRole? ParseRole(string? v) => Enum.TryParse<ApprovalRequiredRole>(v, true, out var r) ? r : null;

    public static RepositoryAssessmentModel ToModel(DocumentRepositoryAssessment a) => new(
        a.Id, a.RepositoryKey, a.RepositoryName, a.RepositoryType.ToString(), a.AssessmentStatus.ToString(),
        a.RepositoryOwnerUserId, a.RepositoryOwnerRole, a.ExactLocation, a.LocationType.ToString(),
        a.AccessModelDescription, a.AccessReviewFrequency, a.BackupMethodDescription, a.RestoreTestFrequency,
        a.ApprovalMechanismDescription, a.EffectiveCopyControlDescription, a.AuditTrailDescription, a.ChangeControlDescription,
        a.ValidationEvidenceReference, a.MaxInterimPeriodDays, a.InterimCheckpointDueDate, a.MigrationReconciliationRequired,
        a.MigrationReconciliationReference, a.AssessmentEvidenceReference, a.ApprovedByRole, a.ApprovedAt, a.RejectionReason,
        a.ValidFrom, a.ValidUntil);

    public static RepositoryAssessmentFindingModel ToFinding(DocumentRepositoryAssessmentFinding f) => new(
        f.Id, f.RepositoryAssessmentId, f.FindingKey, f.FindingType.ToString(), f.Severity.ToString(), f.Status.ToString(),
        f.Description, f.EvidenceReference);
}
