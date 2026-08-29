using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementRetention;

// MOD-0029-FU15 — retention / legal hold / disposition contracts, reason codes and wire mapping (SOP §22).

/// <summary>
/// MOD-0029-FU15 — RECOMMENDED Layer 1 RBAC keys. NOT seeded in this FU (no AuthService change): the controller
/// reuses the already-seeded controlled-documents view/create keys. A later hardening FU should seed these —
/// legal-hold release in particular deserves its own key rather than reusing a document-create permission.
/// </summary>
public static class DocumentRetentionPermissions
{
    public const string RetentionView = "platform.document-management.retention.view";
    public const string RetentionManage = "platform.document-management.retention.manage";
    public const string LegalHoldView = "platform.document-management.legal-hold.view";
    public const string LegalHoldManage = "platform.document-management.legal-hold.manage";
    public const string LegalHoldRelease = "platform.document-management.legal-hold.release";
    public const string DispositionManage = "platform.document-management.disposition.manage";
    public const string DispositionApprove = "platform.document-management.disposition.approve";
}

public static class RetentionReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string PolicyNotFound = "RETENTION_POLICY_NOT_FOUND";
    public const string PolicyKeyRequired = "POLICY_KEY_REQUIRED";
    public const string PolicyNameRequired = "POLICY_NAME_REQUIRED";
    public const string RetentionYearsInvalid = "RETENTION_YEARS_INVALID";
    public const string PolicyKeyDuplicate = "POLICY_KEY_DUPLICATE";
    public const string PolicyAlreadyRetired = "POLICY_ALREADY_RETIRED";
    public const string SubjectNotFound = "RETENTION_SUBJECT_NOT_FOUND";

    public const string HoldNotFound = "LEGAL_HOLD_NOT_FOUND";
    public const string HoldTitleRequired = "HOLD_TITLE_REQUIRED";
    public const string HoldScopeRequired = "HOLD_SCOPE_REQUIRED";
    public const string HoldLegalApprovalRequired = "HOLD_LEGAL_APPROVAL_EVIDENCE_REQUIRED";
    public const string HoldReleaseApprovalRequired = "HOLD_RELEASE_LEGAL_APPROVAL_REQUIRED";
    public const string HoldReleaseConcurrenceRequired = "HOLD_RELEASE_GQD_CONCURRENCE_REQUIRED";
    public const string HoldNotActive = "LEGAL_HOLD_NOT_ACTIVE";
    public const string HoldAlreadyDecided = "LEGAL_HOLD_ALREADY_DECIDED";

    public const string DispositionNotFound = "DISPOSITION_REQUEST_NOT_FOUND";
    public const string DispositionBlockedByHold = "DISPOSITION_BLOCKED_BY_LEGAL_HOLD";
    public const string DispositionNotEligible = "DISPOSITION_SUBJECT_NOT_ELIGIBLE";
    public const string DispositionNotEvaluated = "DISPOSITION_SUBJECT_NOT_EVALUATED";
    public const string DispositionApprovalEvidenceRequired = "DISPOSITION_APPROVAL_EVIDENCE_REQUIRED";
    public const string DispositionInvalidState = "DISPOSITION_INVALID_STATE";
    public const string PermissionDenied = "PERMISSION_DENIED";
}

// ── inputs ───────────────────────────────────────────────────────────────────

public sealed record RetentionPolicyFieldsInput(
    string PolicyKey,
    string PolicyName,
    string? SubjectType,
    string? RetentionClass,
    int MinimumRetentionYears,
    string? RetentionTrigger,
    bool RetainWhileEffective,
    int? RetainAfterRetirementYears,
    int? RetainAfterSupersessionYears,
    bool IsPermanentRetention,
    string? RegulatoryBasis,
    string? Jurisdiction,
    bool IsLongestApplicableCandidate = true);

/// <summary>
/// Evaluation input. <c>TriggerDate</c> is supplied by the caller for subject types whose owning repository does
/// not yet expose a by-id lookup — see <c>DocumentRetentionTriggerDateResolver</c> for the resolution table.
/// </summary>
public sealed record EvaluateRetentionInput(
    string SubjectType,
    Guid SubjectId,
    Guid? RegisterEntryId,
    Guid? ControlledDocumentId,
    DateTimeOffset? TriggerDate,
    string? RetentionClass);

public sealed record LegalHoldFieldsInput(
    string HoldTitle,
    string? HoldKey,
    string? HoldReason,
    string? ScopeType,
    IReadOnlyList<Guid>? RegisterEntryIds,
    IReadOnlyList<Guid>? ControlledDocumentIds,
    IReadOnlyList<string>? SubjectTypes,
    IReadOnlyList<Guid>? ExternalDocumentIds,
    string? ScopeDescription,
    Guid? IssuedByLegalUserId,
    string? IssuedByLegalRole,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveUntil);

public sealed record ActivateLegalHoldInput(
    string LegalApprovalEvidenceReference,
    Guid? GqdConcurrenceUserId,
    string? GqdConcurrenceEvidenceReference);

/// <summary>SOP §22 — release requires BOTH Legal written approval AND GQD concurrence. Neither alone suffices.</summary>
public sealed record ReleaseLegalHoldInput(
    string ReleaseLegalApprovalReference,
    string ReleaseGqdConcurrenceReference);

public sealed record CreateDispositionRequestInput(
    string SubjectType,
    Guid SubjectId,
    Guid? RegisterEntryId,
    string? Comment);

public sealed record ApproveDispositionInput(string ApprovalEvidenceReference, Guid? ApprovedByUserId);

public sealed record RejectDispositionInput(string Reason);

public sealed record ExecuteDispositionMarkerInput(string ExecutionEvidenceReference);

// ── output models ────────────────────────────────────────────────────────────

public sealed record RetentionPolicyModel(
    Guid Id,
    string PolicyKey,
    string PolicyName,
    string PolicyStatus,
    string SubjectType,
    string? RetentionClass,
    int MinimumRetentionYears,
    int EffectiveRetentionYears,
    string RetentionTrigger,
    bool RetainWhileEffective,
    int? RetainAfterRetirementYears,
    int? RetainAfterSupersessionYears,
    bool IsPermanentRetention,
    string? RegulatoryBasis,
    string? Jurisdiction,
    bool IsLongestApplicableCandidate);

public sealed record RetentionSubjectModel(
    Guid Id,
    string SubjectType,
    Guid SubjectId,
    Guid? RegisterEntryId,
    Guid? ControlledDocumentId,
    Guid? PolicyId,
    string? PolicyKey,
    string? RetentionClass,
    DateTimeOffset? RetentionTriggerDate,
    DateTimeOffset? RetentionDueDate,
    DateTimeOffset? DispositionEligibleAt,
    bool IsDispositionEligible,
    bool IsBlockedByLegalHold,
    IReadOnlyList<Guid> ActiveLegalHoldIds,
    bool IsPermanentRetention,
    DateTimeOffset? LastEvaluatedAt,
    string? LastEvaluatedBy,
    string EvaluationStatus,
    string? EvaluationNote);

public sealed record LegalHoldModel(
    Guid Id,
    string HoldKey,
    string HoldTitle,
    string HoldStatus,
    string HoldReason,
    string ScopeType,
    IReadOnlyList<Guid> RegisterEntryIds,
    IReadOnlyList<Guid> ControlledDocumentIds,
    IReadOnlyList<string> SubjectTypes,
    IReadOnlyList<Guid> ExternalDocumentIds,
    string? ScopeDescription,
    Guid? IssuedByLegalUserId,
    string? IssuedByLegalRole,
    DateTimeOffset? IssuedAt,
    string? LegalApprovalEvidenceReference,
    Guid? GqdConcurrenceUserId,
    DateTimeOffset? GqdConcurrenceAt,
    string? GqdConcurrenceEvidenceReference,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveUntil,
    DateTimeOffset? ReleaseRequestedAt,
    string? ReleaseRequestedBy,
    string? ReleaseLegalApprovalReference,
    string? ReleaseGqdConcurrenceReference,
    DateTimeOffset? ReleasedAt,
    string? ReleasedBy);

public sealed record LegalHoldSubjectModel(
    Guid Id,
    Guid LegalHoldId,
    string SubjectType,
    Guid SubjectId,
    Guid? RegisterEntryId,
    DateTimeOffset HoldAppliedAt,
    DateTimeOffset? HoldReleasedAt,
    string Status);

public sealed record DispositionRequestModel(
    Guid Id,
    string RequestNumber,
    string SubjectType,
    Guid SubjectId,
    Guid? RegisterEntryId,
    Guid? PolicyId,
    string RequestStatus,
    DateTimeOffset? EligibilityCheckedAt,
    string EligibilityResult,
    string? RequestedBy,
    DateTimeOffset RequestedAt,
    string? ApprovalEvidenceReference,
    string? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? RejectionReason,
    string? ExecutionEvidenceReference,
    DateTimeOffset? ExecutedAt,
    string? ExecutedBy,
    string? Comment,
    bool SubjectWasDeleted,
    string BoundaryStatement);

public static class RetentionWire
{
    public static RetentionSubjectType? ParseSubjectType(string? v) =>
        Enum.TryParse<RetentionSubjectType>(v, true, out var r) ? r : null;

    public static RetentionTrigger ParseTrigger(string? v) =>
        Enum.TryParse<RetentionTrigger>(v, true, out var r) ? r : RetentionTrigger.CreationDate;

    public static LegalHoldReason ParseHoldReason(string? v) =>
        Enum.TryParse<LegalHoldReason>(v, true, out var r) ? r : LegalHoldReason.Litigation;

    public static LegalHoldScopeType ParseScopeType(string? v) =>
        Enum.TryParse<LegalHoldScopeType>(v, true, out var r) ? r : LegalHoldScopeType.RegisterEntry;

    /// <summary>The standing FU15 boundary statement: a disposition marker is not a deletion.</summary>
    public const string DispositionBoundaryStatement =
        "Disposition is recorded as a governance marker only. MOD-0029-FU15 performs no deletion, purge or " +
        "archival — the subject record remains fully intact and retrievable.";

    public static RetentionPolicyModel ToPolicy(DocumentRetentionPolicy p) => new(
        p.Id, p.PolicyKey, p.PolicyName, p.PolicyStatus.ToString(), p.SubjectType.ToString(), p.RetentionClass,
        p.MinimumRetentionYears, p.EffectiveRetentionYears(), p.RetentionTrigger.ToString(), p.RetainWhileEffective,
        p.RetainAfterRetirementYears, p.RetainAfterSupersessionYears, p.IsPermanentRetention, p.RegulatoryBasis,
        p.Jurisdiction, p.IsLongestApplicableCandidate);

    public static RetentionSubjectModel ToSubject(DocumentRetentionSubject s) => new(
        s.Id, s.SubjectType.ToString(), s.SubjectId, s.RegisterEntryId, s.ControlledDocumentId, s.PolicyId,
        s.PolicyKey, s.RetentionClass, s.RetentionTriggerDate, s.RetentionDueDate, s.DispositionEligibleAt,
        s.IsDispositionEligible, s.IsBlockedByLegalHold, s.ActiveLegalHoldIds.ToList(), s.IsPermanentRetention,
        s.LastEvaluatedAt, s.LastEvaluatedBy, s.EvaluationStatus.ToString(), s.EvaluationNote);

    public static LegalHoldModel ToHold(DocumentLegalHold h) => new(
        h.Id, h.HoldKey, h.HoldTitle, h.HoldStatus.ToString(), h.HoldReason.ToString(), h.ScopeType.ToString(),
        h.RegisterEntryIds.ToList(), h.ControlledDocumentIds.ToList(),
        h.SubjectTypes.Select(x => x.ToString()).ToList(), h.ExternalDocumentIds.ToList(), h.ScopeDescription,
        h.IssuedByLegalUserId, h.IssuedByLegalRole, h.IssuedAt, h.LegalApprovalEvidenceReference,
        h.GqdConcurrenceUserId, h.GqdConcurrenceAt, h.GqdConcurrenceEvidenceReference, h.EffectiveFrom,
        h.EffectiveUntil, h.ReleaseRequestedAt, h.ReleaseRequestedBy, h.ReleaseLegalApprovalReference,
        h.ReleaseGqdConcurrenceReference, h.ReleasedAt, h.ReleasedBy);

    public static LegalHoldSubjectModel ToHoldSubject(DocumentLegalHoldSubject s) => new(
        s.Id, s.LegalHoldId, s.SubjectType.ToString(), s.SubjectId, s.RegisterEntryId, s.HoldAppliedAt,
        s.HoldReleasedAt, s.Status.ToString());

    public static DispositionRequestModel ToDisposition(DocumentDispositionRequest r) => new(
        r.Id, r.RequestNumber, r.SubjectType.ToString(), r.SubjectId, r.RegisterEntryId, r.PolicyId,
        r.RequestStatus.ToString(), r.EligibilityCheckedAt, r.EligibilityResult.ToString(), r.RequestedBy,
        r.RequestedAt, r.ApprovalEvidenceReference, r.ApprovedBy, r.ApprovedAt, r.RejectionReason,
        r.ExecutionEvidenceReference, r.ExecutedAt, r.ExecutedBy, r.Comment,
        SubjectWasDeleted: false, DispositionBoundaryStatement);
}
