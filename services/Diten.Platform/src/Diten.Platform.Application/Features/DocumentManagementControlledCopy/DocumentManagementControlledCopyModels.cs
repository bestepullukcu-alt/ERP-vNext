using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementControlledCopy;

// MOD-0029-FU17 — controlled copy / withdrawal plan / obsolete reconciliation contracts, reason codes and wire mapping
// (GMG-QMS-SOP-0001 §9.12–9.13, §18 LOG-0002, §19 gate 6).

/// <summary>
/// MOD-0029-FU17 — RECOMMENDED Layer 1 RBAC keys. NOT seeded in this FU (no AuthService change): the controller reuses
/// the already-seeded controlled-documents create/view keys. FU06A hardening should seed these.
/// </summary>
public static class DocumentControlledCopyPermissions
{
    public const string View = "platform.document-management.master-register.controlled-copy.view";
    public const string Manage = "platform.document-management.master-register.controlled-copy.manage";
    public const string Reconcile = "platform.document-management.master-register.controlled-copy.reconcile";
}

public static class ControlledCopyReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string CopyNotFound = "COPY_NOT_FOUND";
    public const string PlanNotFound = "PLAN_NOT_FOUND";
    public const string FindingNotFound = "FINDING_NOT_FOUND";
    public const string NotEligibleForActiveCopy = "NOT_ELIGIBLE_FOR_ACTIVE_COPY";
    public const string DuplicateCopyNumber = "DUPLICATE_COPY_NUMBER";
    public const string HolderOrLocationRequired = "HOLDER_OR_LOCATION_REQUIRED";
    public const string EvidenceRequired = "EVIDENCE_REQUIRED";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string PlanIncomplete = "PLAN_INCOMPLETE";
    public const string DeviationRequired = "DEVIATION_REQUIRED";
    public const string PermissionDenied = "PERMISSION_DENIED";
}

// ── inputs ───────────────────────────────────────────────────────────────────

public sealed record RegisterControlledCopyInput(
    string CopyType,
    int? CopyNumber,
    string? LocationType,
    string? LocationDescription,
    Guid? HolderUserId,
    string? HolderRole,
    string? HolderDepartment,
    Guid? ControlledDocumentId,
    Guid? ControlledDocumentVersionId,
    Guid? RepositoryAssessmentId);

public sealed record UpdateControlledCopyLocationInput(
    string? LocationType, string? LocationDescription, Guid? HolderUserId, string? HolderRole, string? HolderDepartment);

public sealed record WithdrawControlledCopyInput(string WithdrawalEvidenceReference);

public sealed record ReconcileControlledCopyInput(string ReconciliationEvidenceReference);

public sealed record MarkControlledCopyMissingInput(string? Comment);

public sealed record MarkControlledCopyObsoleteInput(string ObsoleteReason, string? LocationDescription);

public sealed record GenerateWithdrawalPlanInput(string? TriggerType, DateTimeOffset? DueDate);

public sealed record CompleteWithdrawalPlanInput(string? PlanEvidenceReference, string? MissingDeviationReference);

public sealed record ResolveObsoleteFindingInput(string ResolutionEvidenceReference, string? DeviationReference, string? QualityEventReference);

// ── output models ────────────────────────────────────────────────────────────

public sealed record ControlledCopyModel(
    Guid Id,
    Guid RegisterEntryId,
    int CopyNumber,
    string CopyType,
    string CopyStatus,
    string LocationType,
    string? LocationDescription,
    Guid? HolderUserId,
    string? HolderRole,
    string? HolderDepartment,
    bool WithdrawalRequired,
    DateTimeOffset? WithdrawalDueDate,
    DateTimeOffset? WithdrawnAt,
    string? WithdrawalEvidenceReference,
    DateTimeOffset? ReconciledAt,
    string? ReconciliationEvidenceReference,
    DateTimeOffset? ObsoleteDetectedAt,
    string? ObsoleteReason,
    string? DeviationReference,
    DateTimeOffset IssuedAt);

public sealed record WithdrawalPlanModel(
    Guid Id,
    Guid RegisterEntryId,
    string TriggerType,
    string PlanStatus,
    int RequiredCopyCount,
    int WithdrawnCopyCount,
    int MissingCopyCount,
    int ObsoleteCopyCount,
    DateTimeOffset? DueDate,
    string? PlanEvidenceReference,
    DateTimeOffset? CompletedAt);

public sealed record ObsoleteCopyFindingModel(
    Guid Id,
    Guid RegisterEntryId,
    Guid? ControlledCopyId,
    string FindingKey,
    string FindingType,
    string Severity,
    string Status,
    string Description,
    string? LocationDescription,
    string? DeviationReference,
    string? QualityEventReference);

public sealed record CopyWithdrawalReadinessModel(
    Guid RegisterEntryId,
    bool Ready,
    bool HasControlledCopyData,
    int ActiveCopyCount,
    int PendingWithdrawalCount,
    int WithdrawnCount,
    int ObsoleteCount,
    int OpenCriticalFindingCount,
    string? PlanStatus,
    IReadOnlyList<string> BlockingReasons);

public static class ControlledCopyWire
{
    public static ControlledCopyType? ParseType(string? v) => Enum.TryParse<ControlledCopyType>(v, true, out var r) ? r : null;
    public static ControlledCopyLocationType ParseLocation(string? v) => Enum.TryParse<ControlledCopyLocationType>(v, true, out var r) ? r : ControlledCopyLocationType.PointOfUse;
    public static CopyWithdrawalTriggerType ParseTrigger(string? v) => Enum.TryParse<CopyWithdrawalTriggerType>(v, true, out var r) ? r : CopyWithdrawalTriggerType.Manual;

    public static ControlledCopyModel ToCopy(DocumentControlledCopy c) => new(
        c.Id, c.RegisterEntryId, c.CopyNumber, c.CopyType.ToString(), c.CopyStatus.ToString(), c.LocationType.ToString(),
        c.LocationDescription, c.HolderUserId, c.HolderRole, c.HolderDepartment, c.WithdrawalRequired, c.WithdrawalDueDate,
        c.WithdrawnAt, c.WithdrawalEvidenceReference, c.ReconciledAt, c.ReconciliationEvidenceReference, c.ObsoleteDetectedAt,
        c.ObsoleteReason, c.DeviationReference, c.IssuedAt);

    public static WithdrawalPlanModel ToPlan(DocumentCopyWithdrawalPlan p) => new(
        p.Id, p.RegisterEntryId, p.TriggerType.ToString(), p.PlanStatus.ToString(), p.RequiredCopyCount, p.WithdrawnCopyCount,
        p.MissingCopyCount, p.ObsoleteCopyCount, p.DueDate, p.PlanEvidenceReference, p.CompletedAt);

    public static ObsoleteCopyFindingModel ToFinding(DocumentObsoleteCopyFinding f) => new(
        f.Id, f.RegisterEntryId, f.ControlledCopyId, f.FindingKey, f.FindingType.ToString(), f.Severity.ToString(),
        f.Status.ToString(), f.Description, f.LocationDescription, f.DeviationReference, f.QualityEventReference);
}
