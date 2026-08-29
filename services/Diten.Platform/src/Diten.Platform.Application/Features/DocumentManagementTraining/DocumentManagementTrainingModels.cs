using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementTraining;

// MOD-0029-FU11 — document training matrix + readiness contracts, reason codes and wire mapping (SOP §7.3, §17, §19).

/// <summary>
/// MOD-0029-FU11 — RECOMMENDED Layer 1 RBAC keys. NOT seeded in this FU (no AuthService change): the controller reuses
/// the already-seeded controlled-documents create/view keys. FU06A hardening should seed these.
/// </summary>
public static class DocumentTrainingPermissions
{
    public const string View = "platform.document-management.master-register.training.view";
    public const string Manage = "platform.document-management.master-register.training.manage";
    public const string Verify = "platform.document-management.master-register.training.verify";
}

public static class TrainingReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string RequirementNotFound = "REQUIREMENT_NOT_FOUND";
    public const string AssignmentNotFound = "ASSIGNMENT_NOT_FOUND";
    public const string EvidenceRequired = "EVIDENCE_REQUIRED";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string PermissionDenied = "PERMISSION_DENIED";
}

// ── inputs ───────────────────────────────────────────────────────────────────

public sealed record AddManualTrainingRequirementInput(
    string AudienceType,
    string? RequiredRole,
    Guid? RequiredUserId,
    string? RequiredDepartment,
    string TrainingType,
    bool IsCriticalProcessUserRequirement,
    bool EffectivenessCheckRequired,
    bool AcknowledgementRequired,
    bool MandatoryBeforeEffective);

public sealed record AssignTrainingInput(
    Guid RequirementId,
    Guid? AssignedToUserId,
    string? AssignedToRole,
    string? AssignedToDepartment,
    DateTimeOffset? DueDate);

public sealed record CompleteTrainingInput(string CompletionEvidenceReference);

public sealed record RecordEffectivenessInput(bool Passed, string EvidenceReference);

public sealed record RestrictTrainingInput(string Reason);

// ── output models ────────────────────────────────────────────────────────────

public sealed record TrainingRequirementModel(
    Guid Id,
    Guid RegisterEntryId,
    string RequirementKey,
    string AudienceType,
    string? RequiredRole,
    Guid? RequiredUserId,
    string? RequiredDepartment,
    string TrainingType,
    bool IsCriticalProcessUserRequirement,
    bool EffectivenessCheckRequired,
    bool AcknowledgementRequired,
    bool MandatoryBeforeEffective,
    string SourceRule,
    string Status);

public sealed record TrainingAssignmentModel(
    Guid Id,
    Guid RegisterEntryId,
    Guid RequirementId,
    Guid? AssignedToUserId,
    string? AssignedToRole,
    string? AssignedToDepartment,
    string TrainingType,
    string Status,
    DateTimeOffset AssignedAt,
    DateTimeOffset? DueDate,
    string? CompletionEvidenceReference,
    DateTimeOffset? CompletedAt,
    string EffectivenessCheckStatus,
    string? EffectivenessEvidenceReference,
    string? RestrictionReason);

public sealed record TrainingReadinessModel(
    Guid RegisterEntryId,
    int RequiredCount,
    int AssignedCount,
    int CompletedCount,
    int RestrictedCount,
    int PendingCount,
    int FailedCount,
    int MissingAssignmentCount,
    int EffectivenessPendingCount,
    bool Ready,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> WarningReasons);

public static class TrainingWire
{
    public static TrainingAudienceType? ParseAudience(string? v) =>
        Enum.TryParse<TrainingAudienceType>(v, true, out var r) ? r : null;

    public static DocumentTrainingType? ParseTrainingType(string? v) =>
        Enum.TryParse<DocumentTrainingType>(v, true, out var r) ? r : null;

    public static ApprovalRequiredRole? ParseRole(string? v) =>
        Enum.TryParse<ApprovalRequiredRole>(v, true, out var r) ? r : null;

    public static TrainingRequirementModel ToRequirement(DocumentTrainingMatrixRequirement r) => new(
        r.Id, r.RegisterEntryId, r.RequirementKey, r.AudienceType.ToString(), r.RequiredRole?.ToString(),
        r.RequiredUserId, r.RequiredDepartment, r.TrainingType.ToString(), r.IsCriticalProcessUserRequirement,
        r.EffectivenessCheckRequired, r.AcknowledgementRequired, r.MandatoryBeforeEffective, r.SourceRule.ToString(), r.Status.ToString());

    public static TrainingAssignmentModel ToAssignment(DocumentTrainingAssignment a) => new(
        a.Id, a.RegisterEntryId, a.RequirementId, a.AssignedToUserId, a.AssignedToRole?.ToString(), a.AssignedToDepartment,
        a.TrainingType.ToString(), a.Status.ToString(), a.AssignedAt, a.DueDate, a.CompletionEvidenceReference, a.CompletedAt,
        a.EffectivenessCheckStatus.ToString(), a.EffectivenessEvidenceReference, a.RestrictionReason);
}
