using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementReleaseGates;

// MOD-0029-FU10 — non-waivable release gate contracts, catalog, reason codes, options and wire mapping (SOP §19/§21).

/// <summary>
/// MOD-0029-FU10 — RECOMMENDED Layer 1 RBAC keys. NOT seeded in this FU (no AuthService change): the controller reuses
/// the already-seeded controlled-documents create/view keys. FU06A hardening should seed these.
/// </summary>
public static class DocumentReleaseGatePermissions
{
    public const string View = "platform.document-management.master-register.release-gate.view";
    public const string Evaluate = "platform.document-management.master-register.release-gate.evaluate";
    public const string RecordEvidence = "platform.document-management.master-register.release-gate.evidence.record";
}

public static class ReleaseGateReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string InvalidGateKey = "INVALID_GATE_KEY";
    public const string EvidenceIncomplete = "EVIDENCE_INCOMPLETE";
    public const string PermissionDenied = "PERMISSION_DENIED";
}

/// <summary>
/// MOD-0029-FU10 — release gate policy toggles. Training (gate 5) is always required for Critical; this makes it
/// required for Major/Minor too when set. Section <c>DocumentManagement:ReleaseGates</c>.
/// </summary>
public sealed class DocumentReleaseGateOptions
{
    public const string SectionName = "DocumentManagement:ReleaseGates";

    public bool RequireTrainingEvidenceForNonCritical { get; set; }
}

/// <summary>MOD-0029-FU10 — the fixed, service-controlled catalog of the six non-waivable gates (SOP §19).</summary>
public static class ReleaseGateCatalog
{
    public sealed record Definition(ReleaseGateKey Key, int Number, string Name, bool IsManual);

    public static readonly IReadOnlyList<Definition> Gates =
    [
        new(ReleaseGateKey.MasterRegisterActive, 1, "Master Register active and document registered with UID", IsManual: false),
        new(ReleaseGateKey.ApprovedRepositoryAvailable, 2, "Approved repository or validated DMS with an authorised release route", IsManual: true),
        new(ReleaseGateKey.MandatoryApprovalEvidence, 3, "Approval evidence from mandatory approvers; author not sole approver", IsManual: false),
        new(ReleaseGateKey.RequiredExecutionMaterialsEffective, 4, "Every form/template/register required to execute is effective and available", IsManual: true),
        new(ReleaseGateKey.TrainingReadiness, 5, "Training assigned; critical-process users trained or formally restricted", IsManual: true),
        new(ReleaseGateKey.SupersededCopyWithdrawalMethod, 6, "A method exists to withdraw superseded copies from point of use", IsManual: true),
    ];

    public static Definition ByKey(ReleaseGateKey key) => Gates.First(g => g.Key == key);

    public static ReleaseGateKey? ParseKey(string? value) =>
        Enum.TryParse<ReleaseGateKey>(value, true, out var v) ? v : null;
}

// ── inputs ───────────────────────────────────────────────────────────────────

public sealed record RecordReleaseGateEvidenceInput(
    string GateKey,
    string EvidenceReference,
    Guid? VerifiedByUserId,
    string? VerifiedByRole,
    DateTimeOffset? VerificationDate,
    string? Comment);

// ── output models ────────────────────────────────────────────────────────────

public sealed record ReleaseGateResultModel(
    int GateNumber,
    string GateKey,
    string GateName,
    string GateResult,
    bool IsNonWaivable,
    bool ExceptionPermitted,
    string? EvidenceReference,
    Guid? VerifiedByUserId,
    string? VerifiedByRole,
    DateTimeOffset? VerificationDate,
    string Source,
    string? BlockingReason,
    string? WarningReason);

public sealed record ReleaseGateEvaluationModel(
    Guid EvaluationId,
    Guid RegisterEntryId,
    string EvaluationStatus,
    bool Ready,
    int GateCount,
    int CompletedGateCount,
    int BlockingCount,
    int WarningCount,
    DateTimeOffset EvaluatedAt,
    string? EvaluatedBy,
    IReadOnlyList<ReleaseGateResultModel> Gates);

public static class ReleaseGateWire
{
    public static ReleaseGateResultModel ToResult(DocumentReleaseGateResult r) => new(
        r.GateNumber, r.GateKey.ToString(), r.GateName, r.GateResult.ToString(), r.IsNonWaivable, r.ExceptionPermitted,
        r.EvidenceReference, r.VerifiedByUserId, r.VerifiedByRole, r.VerificationDate, r.Source.ToString(),
        r.BlockingReason, r.WarningReason);

    public static ReleaseGateEvaluationModel ToEvaluation(DocumentReleaseGateEvaluation e, IReadOnlyList<DocumentReleaseGateResult> results) => new(
        e.Id, e.RegisterEntryId, e.EvaluationStatus.ToString(), e.EvaluationStatus == ReleaseGateEvaluationStatus.Complete,
        e.GateCount, e.CompletedGateCount, e.BlockingCount, e.WarningCount, e.EvaluatedAt, e.EvaluatedBy,
        results.Select(ToResult).ToList());
}
