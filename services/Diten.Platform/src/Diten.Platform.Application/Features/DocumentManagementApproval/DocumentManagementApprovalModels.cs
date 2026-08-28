using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementApproval;

// MOD-0029-FU09 — approval route matrix + segregation contracts, reason codes, options and wire mapping, kept in one
// file (Golden Reference Compact).

/// <summary>
/// MOD-0029-FU09 — RECOMMENDED Layer 1 RBAC keys. NOT seeded in this FU (no AuthService change): the controller
/// reuses the already-seeded controlled-documents create/view keys. FU06A hardening should seed these.
/// </summary>
public static class DocumentApprovalPermissions
{
    public const string View = "platform.document-management.master-register.approval.view";
    public const string Manage = "platform.document-management.master-register.approval.manage";
    public const string RecordEvidence = "platform.document-management.master-register.approval.evidence.record";
}

public static class ApprovalReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string RequirementNotFound = "REQUIREMENT_NOT_FOUND";
    public const string WrongRole = "WRONG_APPROVER_ROLE";
    public const string SegregationFailed = "SEGREGATION_FAILED";
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string RoleConfigurationMissing = "APPROVAL_ROLE_CONFIGURATION_MISSING";
    public const string ApproverIdentityMismatch = "APPROVER_IDENTITY_MISMATCH";
    public const string ApproverNotAssigned = "APPROVER_NOT_ASSIGNED";
}

/// <summary>
/// Legacy configuration shape retained for configuration compatibility. Approval gating is now non-waivable:
/// InReview → ApprovedPendingEffective always requires complete approval evidence.
/// </summary>
public sealed class DocumentApprovalOptions
{
    public const string SectionName = "DocumentManagement:Approval";

    [Obsolete("Approval gating is mandatory and this switch is ignored.")]
    public bool RequireApprovalForApprovedPendingEffective { get; set; }
}

// ── inputs ───────────────────────────────────────────────────────────────────

/// <summary>
/// MOD-0029-FU09 — optional impact / identity overrides for route resolution. Nulls fall back to the values already
/// stored on the register entry (populated manually or by a future impact-assessment FU). Provided values are
/// persisted onto the entry so segregation and readiness use one consistent source.
/// </summary>
public sealed record ResolveApprovalRouteInput(
    bool? HasRaImpact = null,
    bool? HasPvImpact = null,
    bool? HasBatchReleaseImpact = null,
    bool? HasDmsCsvImpact = null,
    bool? HasQualityAgreementImpact = null,
    bool? IsGroupGovernance = null,
    bool? RequiresLegalReview = null,
    bool? RequiresCeoEndorsement = null,
    bool? RequiresIndependentTechnicalReview = null,
    Guid? AuthorUserId = null,
    Guid? RequestedByUserId = null);

public sealed record RecordApprovalEvidenceInput(
    Guid RequirementId,
    string Action,
    Guid PerformedByUserId,
    string PerformedByRole,
    string? EvidenceReference,
    string? Comment);

public sealed record RejectApprovalInput(
    Guid RequirementId,
    Guid PerformedByUserId,
    string PerformedByRole,
    string Reason,
    string? Comment);

// ── output models ────────────────────────────────────────────────────────────

public sealed record ApprovalRequirementModel(
    Guid Id,
    Guid RegisterEntryId,
    string RequirementKey,
    string RequirementType,
    string RequiredRole,
    Guid? RequiredRoleId,
    string? RequiredRoleName,
    string? RequiredRoleDisplayName,
    Guid? RequiredUserId,
    bool IsMandatory,
    bool IsNonDelegable,
    string SourceRule,
    string Status,
    Guid? CompletedByUserId,
    string? CompletedByRole,
    DateTimeOffset? CompletedAt,
    string? EvidenceReference,
    string? Comment);

public sealed record ApprovalEvidenceModel(
    Guid Id,
    Guid RegisterEntryId,
    Guid RequirementId,
    string Action,
    Guid PerformedByUserId,
    string PerformedByRole,
    DateTimeOffset PerformedAt,
    string? EvidenceReference,
    string? Comment,
    string SegregationResult,
    string? FailureReason);

public sealed record ApprovalReadinessModel(
    Guid RegisterEntryId,
    int RequiredCount,
    int CompletedCount,
    int PendingCount,
    int RejectedCount,
    int BlockedCount,
    IReadOnlyList<string> SegregationFailures,
    IReadOnlyList<string> MissingMandatoryRoles,
    bool Ready,
    string ApprovalEvidenceStatus);

public static class ApprovalWire
{
    public static ApprovalEvidenceAction? ParseAction(string? value) =>
        Enum.TryParse<ApprovalEvidenceAction>(value, true, out var v) ? v : null;

    public static ApprovalRequiredRole? ParseRole(string? value) =>
        Enum.TryParse<ApprovalRequiredRole>(value, true, out var v) ? v : null;

    public static ApprovalRequirementModel ToRequirement(DocumentApprovalRequirement r) => new(
        r.Id, r.RegisterEntryId, r.RequirementKey, r.RequirementType.ToString(), r.RequiredRole.ToString(),
        r.RequiredRoleId, r.RequiredRoleName, r.RequiredRoleDisplayName, r.RequiredUserId,
        r.IsMandatory, r.IsNonDelegable, r.SourceRule.ToString(), r.Status.ToString(),
        r.CompletedByUserId, r.CompletedByRole?.ToString(), r.CompletedAt, r.EvidenceReference, r.Comment);

    public static ApprovalEvidenceModel ToEvidence(DocumentApprovalEvidence e) => new(
        e.Id, e.RegisterEntryId, e.RequirementId, e.Action.ToString(), e.PerformedByUserId, e.PerformedByRole.ToString(),
        e.PerformedAt, e.EvidenceReference, e.Comment, e.SegregationResult.ToString(), e.FailureReason);
}
