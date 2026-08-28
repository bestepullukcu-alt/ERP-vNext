using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementLifecycle;

// MOD-0029-FU08 — controlled document lifecycle (GMG-QMS-SOP-0001 §6.2) contracts, reason codes, options and wire
// mapping, kept in one file (Golden Reference Compact).

/// <summary>
/// MOD-0029-FU08 — RECOMMENDED Layer 1 RBAC key. NOT seeded in this FU (no AuthService change): the controller reuses
/// the already-seeded controlled-documents create/view keys. FU06A/FU09 hardening should seed this dedicated key.
/// </summary>
public static class DocumentLifecyclePermissions
{
    public const string Manage = "platform.document-management.master-register.lifecycle.manage";
    public const string View = "platform.document-management.master-register.lifecycle.view";
}

public static class LifecycleReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string InvalidTransition = "INVALID_TRANSITION";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string MissingIdentifier = "MISSING_IDENTIFIER";
    public const string RetroactiveEffectiveDate = "RETROACTIVE_EFFECTIVE_DATE";
    public const string ReleaseGateBlocked = "RELEASE_GATE_BLOCKED";
    public const string ApprovalEvidenceMissing = "APPROVAL_EVIDENCE_MISSING";
    public const string DuplicateEffective = "DUPLICATE_EFFECTIVE";
    public const string StaleVersion = "STALE_VERSION";
    public const string ApprovalIncomplete = "APPROVAL_EVIDENCE_INCOMPLETE"; // MOD-0029-FU09 approval-gate block
    public const string ReleaseGateIncomplete = "RELEASE_GATE_INCOMPLETE";   // MOD-0029-FU10 non-waivable gate block
    public const string PermissionDenied = "PERMISSION_DENIED";
}

/// <summary>
/// MOD-0029-FU08 — lifecycle policy toggles. <see cref="RequireReleaseGateForEffective"/> defaults FALSE so the
/// product is not locked before the FU10 non-waivable release-gate engine exists; a missing gate on a controlled/
/// critical document produces a WARNING instead. FU10 can flip this to true. Section <c>DocumentManagement:Lifecycle</c>.
/// </summary>
public sealed class DocumentLifecycleOptions
{
    public const string SectionName = "DocumentManagement:Lifecycle";

    public bool RequireReleaseGateForEffective { get; set; }
}

// ── inputs ───────────────────────────────────────────────────────────────────

public sealed record TransitionDocumentLifecycleInput(
    string TargetStatus,
    string? Reason,
    string? EvidenceReference,
    string? Comment,
    DateTimeOffset? EffectiveDate,
    Guid? RelatedReplacementRegisterEntryId,
    int? ExpectedVersion);

// ── output models ────────────────────────────────────────────────────────────

public sealed record LifecycleStateModel(
    Guid RegisterEntryId,
    string CurrentStatus,
    bool OperationalUseAllowed,
    bool CanStartReview,
    bool CanReturnToDraft,
    bool CanMarkApprovedPendingEffective,
    bool CanMarkEffective,
    bool CanStartRevision,
    bool CanSuspend,
    bool CanRetire,
    bool CanMarkSuperseded,
    string? StatusReasonSummary,
    DateTimeOffset? LastTransitionAt,
    string? LastTransitionBy,
    IReadOnlyList<string> Warnings);

public sealed record LifecycleTransitionRecordModel(
    Guid Id,
    Guid RegisterEntryId,
    string FromStatus,
    string ToStatus,
    string? TransitionReason,
    string? EvidenceReference,
    string? Comment,
    DateTimeOffset? EffectiveDate,
    Guid? RelatedReplacementRegisterEntryId,
    DateTimeOffset PerformedAt,
    string? PerformedBy);

public static class LifecycleWire
{
    public static ControlledDocumentLifecycleStatus? ParseStatus(string? value) =>
        Enum.TryParse<ControlledDocumentLifecycleStatus>(value, true, out var v) ? v : null;

    public static LifecycleStateModel ToState(DocumentMasterRegisterEntry e, IReadOnlyList<string>? warnings = null)
    {
        var targets = e.LifecycleStatus.AllowedTargets();
        return new LifecycleStateModel(
            e.Id,
            e.LifecycleStatus.ToString(),
            e.LifecycleStatus.IsOperationallyEffective(),
            targets.Contains(ControlledDocumentLifecycleStatus.InReview),
            targets.Contains(ControlledDocumentLifecycleStatus.Draft),
            targets.Contains(ControlledDocumentLifecycleStatus.ApprovedPendingEffective),
            targets.Contains(ControlledDocumentLifecycleStatus.Effective),
            targets.Contains(ControlledDocumentLifecycleStatus.UnderRevision),
            targets.Contains(ControlledDocumentLifecycleStatus.Suspended),
            targets.Contains(ControlledDocumentLifecycleStatus.Retired),
            targets.Contains(ControlledDocumentLifecycleStatus.Superseded),
            e.StatusReason,
            e.LastTransitionAt,
            e.LastTransitionBy,
            warnings ?? []);
    }

    public static LifecycleTransitionRecordModel ToRecord(DocumentLifecycleTransitionRecord r) => new(
        r.Id, r.RegisterEntryId, r.FromStatus.ToString(), r.ToStatus.ToString(), r.TransitionReason,
        r.EvidenceReference, r.Comment, r.EffectiveDate, r.RelatedReplacementRegisterEntryId, r.PerformedAt, r.PerformedBy);
}
