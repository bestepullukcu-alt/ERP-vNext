using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementGDocPCorrection;

// MOD-0029-FU21 — GDocP correction trail contracts, reason codes and wire mapping (GMG-QMS-SOP-0001 §21).

/// <summary>
/// MOD-0029-FU21 — RECOMMENDED Layer 1 RBAC keys. NOT seeded in this FU (no AuthService change): the controller
/// reuses the already-seeded controlled-documents view/create keys. A later hardening FU should seed these —
/// reviewing a correction must not be grantable by the same key that records one, or the second-person review is
/// not actually a second person.
/// </summary>
public static class GDocPCorrectionPermissions
{
    public const string View = "platform.document-management.gdocp-corrections.view";
    public const string Record = "platform.document-management.gdocp-corrections.record";
    public const string Review = "platform.document-management.gdocp-corrections.review";
    public const string PolicyManage = "platform.document-management.gdocp-correction-policies.manage";
}

public static class GDocPCorrectionReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string CorrectionNotFound = "GDOCP_CORRECTION_NOT_FOUND";
    public const string PolicyNotFound = "GDOCP_CORRECTION_POLICY_NOT_FOUND";
    public const string PolicyKeyRequired = "POLICY_KEY_REQUIRED";
    public const string PolicyNameRequired = "POLICY_NAME_REQUIRED";
    public const string FieldPathPatternRequired = "FIELD_PATH_PATTERN_REQUIRED";
    public const string PolicyKeyDuplicate = "POLICY_KEY_DUPLICATE";
    public const string PolicyAlreadyRetired = "POLICY_ALREADY_RETIRED";

    public const string SubjectRequired = "CORRECTION_SUBJECT_REQUIRED";
    public const string FieldPathRequired = "CORRECTION_FIELD_PATH_REQUIRED";
    public const string ReasonRequired = "CORRECTION_REASON_REQUIRED";
    public const string NoChange = "CORRECTION_NO_CHANGE";
    public const string SnapshotTooLarge = "CORRECTION_VALUE_SNAPSHOT_TOO_LARGE";
    public const string EvidenceRequired = "CORRECTION_EVIDENCE_REFERENCE_REQUIRED";

    // Backdating / reconstruction protections (SOP §21).
    public const string BackdatingRequiresDeviation = "BACKDATING_REQUIRES_DEVIATION";
    public const string RegulatedTimestampRequiresReview = "REGULATED_TIMESTAMP_CORRECTION_REQUIRES_REVIEW";
    public const string ServerTimestampImmutable = "SERVER_TIMESTAMP_IMMUTABLE";
    public const string HighRiskRequiresDeviation = "HIGH_RISK_CORRECTION_REQUIRES_DEVIATION";
    public const string ReconstructionRequiresEvidence = "RECONSTRUCTION_REQUIRES_EVIDENCE_AND_DEVIATION";
    public const string CorrectionNotAllowedAfterEffective = "CORRECTION_NOT_ALLOWED_AFTER_EFFECTIVE";
    public const string CorrectionNotAllowedAfterApproval = "CORRECTION_NOT_ALLOWED_AFTER_APPROVAL";

    public const string ReviewerRequired = "REVIEWER_REQUIRED";
    public const string ReviewEvidenceRequired = "REVIEW_EVIDENCE_REQUIRED";
    public const string ReviewReasonRequired = "REVIEW_REJECTION_REASON_REQUIRED";
    public const string AlreadyReviewed = "CORRECTION_ALREADY_REVIEWED";
    public const string PermissionDenied = "PERMISSION_DENIED";
}

// ── inputs ───────────────────────────────────────────────────────────────────

public sealed record GDocPCorrectionPolicyInput(
    string PolicyKey,
    string PolicyName,
    string? SubjectType,
    string FieldPathPattern,
    bool RequiresCorrectionReason,
    bool RequiresEvidenceReference,
    bool RequiresReview,
    bool RequiresDeviationReferenceForHighRisk,
    bool AllowCorrectionAfterApproval,
    bool AllowCorrectionAfterEffective,
    bool IsBackdatingSensitive,
    bool IsStatusSensitive,
    bool IsEvidenceSensitive,
    string? Notes);

/// <summary>
/// MOD-0029-FU21 — a correction to record. NOTE what is deliberately ABSENT: there is no CorrectedAt field. The
/// correction timestamp is stamped by the server and can never be supplied, which is the structural half of the
/// backdating protection.
/// </summary>
public sealed record RecordGDocPCorrectionInput(
    string SubjectType,
    Guid SubjectId,
    string FieldPath,
    string? FieldDisplayName,
    string? PreviousValueSnapshot,
    string? NewValueSnapshot,
    string? ValueFormat,
    string? CorrectionType,
    string CorrectionReason,
    string? CorrectionEvidenceReference,
    string? DeviationReference,
    Guid? RegisterEntryId,
    Guid? ControlledDocumentId,
    Guid? CorrectedByUserId,
    string? CorrectedByRole,
    string? RequestedBy,
    bool SubjectIsApproved = false,
    bool SubjectIsEffective = false);

public sealed record ReviewGDocPCorrectionInput(
    Guid? ReviewerUserId,
    string? ReviewerRole,
    string ReviewEvidenceReference,
    string? ReviewComment);

public sealed record RejectGDocPCorrectionInput(
    Guid? ReviewerUserId,
    string? ReviewerRole,
    string Reason);

// ── output models ────────────────────────────────────────────────────────────

public sealed record GDocPCorrectionPolicyModel(
    Guid Id,
    string PolicyKey,
    string PolicyName,
    string PolicyStatus,
    string SubjectType,
    string FieldPathPattern,
    bool RequiresCorrectionReason,
    bool RequiresEvidenceReference,
    bool RequiresReview,
    bool RequiresDeviationReferenceForHighRisk,
    bool AllowCorrectionAfterApproval,
    bool AllowCorrectionAfterEffective,
    bool IsBackdatingSensitive,
    bool IsStatusSensitive,
    bool IsEvidenceSensitive,
    string? Notes);

public sealed record GDocPCorrectionRecordModel(
    Guid Id,
    string CorrectionNumber,
    string SubjectType,
    Guid SubjectId,
    Guid? RegisterEntryId,
    Guid? ControlledDocumentId,
    string FieldPath,
    string? FieldDisplayName,
    string PreviousValueSnapshot,
    string NewValueSnapshot,
    string ValueFormat,
    string CorrectionType,
    string CorrectionReason,
    string? CorrectionEvidenceReference,
    bool IsHighRiskCorrection,
    bool RequiresDeviationReference,
    string? DeviationReference,
    bool IsBackdatingCorrection,
    string? RiskAssessmentNote,
    Guid? CorrectedByUserId,
    string? CorrectedByRole,
    DateTimeOffset CorrectedAt,
    string? RequestedBy,
    DateTimeOffset? RequestedAt,
    string ReviewStatus,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewEvidenceReference,
    string? ReviewComment,
    string BoundaryStatement);

public sealed record GDocPCorrectionReviewModel(
    Guid Id,
    Guid CorrectionRecordId,
    string ReviewDecision,
    Guid? ReviewerUserId,
    string? ReviewerRole,
    string? ReviewerName,
    string? ReviewEvidenceReference,
    string? ReviewComment,
    DateTimeOffset ReviewedAt);

/// <summary>MOD-0029-FU21 — the resolved requirements for one correction, before it is accepted.</summary>
public sealed record GDocPCorrectionRequirementModel(
    bool RequiresCorrectionReason,
    bool RequiresEvidenceReference,
    bool RequiresReview,
    bool RequiresDeviationReference,
    bool AllowCorrectionAfterApproval,
    bool AllowCorrectionAfterEffective,
    bool IsHighRisk,
    bool IsBackdating,
    string RiskAssessmentNote,
    IReadOnlyList<string> AppliedPolicyKeys);

public static class GDocPCorrectionWire
{
    /// <summary>Maximum snapshot length. Oversized values are REFUSED, never truncated (see the service).</summary>
    public const int MaxSnapshotLength = 4000;

    public const string BoundaryStatement =
        "GDocP correction trail: an append-only record of a regulated field correction (previous value, new " +
        "value, reason, evidence, corrector, server-stamped time). MOD-0029-FU21 does not replace the platform " +
        "audit event store, does not itself mutate the corrected record, and implements no e-signature, CAPA or " +
        "data-integrity investigation module — deviation references point to records held elsewhere.";

    public static GDocPSubjectType? ParseSubjectType(string? v) =>
        Enum.TryParse<GDocPSubjectType>(v, true, out var r) ? r : null;

    public static GDocPValueFormat ParseValueFormat(string? v) =>
        Enum.TryParse<GDocPValueFormat>(v, true, out var r) ? r : GDocPValueFormat.Text;

    public static GDocPCorrectionType ParseCorrectionType(string? v) =>
        Enum.TryParse<GDocPCorrectionType>(v, true, out var r) ? r : GDocPCorrectionType.MetadataCorrection;

    public static GDocPCorrectionPolicyModel ToPolicy(DocumentGDocPCorrectionPolicy p) => new(
        p.Id, p.PolicyKey, p.PolicyName, p.PolicyStatus.ToString(), p.SubjectType.ToString(), p.FieldPathPattern,
        p.RequiresCorrectionReason, p.RequiresEvidenceReference, p.RequiresReview,
        p.RequiresDeviationReferenceForHighRisk, p.AllowCorrectionAfterApproval, p.AllowCorrectionAfterEffective,
        p.IsBackdatingSensitive, p.IsStatusSensitive, p.IsEvidenceSensitive, p.Notes);

    public static GDocPCorrectionRecordModel ToRecord(DocumentGDocPCorrectionRecord r) => new(
        r.Id, r.CorrectionNumber, r.SubjectType.ToString(), r.SubjectId, r.RegisterEntryId, r.ControlledDocumentId,
        r.FieldPath, r.FieldDisplayName, r.PreviousValueSnapshot, r.NewValueSnapshot, r.ValueFormat.ToString(),
        r.CorrectionType.ToString(), r.CorrectionReason, r.CorrectionEvidenceReference, r.IsHighRiskCorrection,
        r.RequiresDeviationReference, r.DeviationReference, r.IsBackdatingCorrection, r.RiskAssessmentNote,
        r.CorrectedByUserId, r.CorrectedByRole, r.CorrectedAt, r.RequestedBy, r.RequestedAt,
        r.ReviewStatus.ToString(), r.ReviewedBy, r.ReviewedAt, r.ReviewEvidenceReference, r.ReviewComment,
        BoundaryStatement);

    public static GDocPCorrectionReviewModel ToReview(DocumentGDocPCorrectionReview v) => new(
        v.Id, v.CorrectionRecordId, v.ReviewDecision.ToString(), v.ReviewerUserId, v.ReviewerRole, v.ReviewerName,
        v.ReviewEvidenceReference, v.ReviewComment, v.ReviewedAt);
}
