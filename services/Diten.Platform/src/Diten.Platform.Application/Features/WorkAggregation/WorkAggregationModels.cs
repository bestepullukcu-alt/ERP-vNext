namespace Diten.Platform.Application.Features.WorkAggregation;

// WC-1 (DCP-004) — Unified Work-Item Provider Contract & Projection.
//
// READ / PROJECTION ONLY. Nothing in this feature writes business state or exposes a command endpoint; the
// approve/reject/delegate transitions stay on MOD-0023's existing endpoints. This file holds ALL projection
// DTOs (single models file per the live Platform CQRS convention), the contract value constants that mirror
// the executable authority (frontend/.../WorkCenterNext/fixture-contract.js), the read permission CONSTANT
// (seed is a separate MOD-0018 task), and the actor context assembled by the API layer.

// The read permission key. CONSTANT only — the seed/grant lives in MOD-0018 / Diten.AuthService and is a
// separate task (WC-1b / BL-022). Nothing here writes to AuthService or a manifest.
public static class WorkAggregationPermissions
{
    public const string InboxView = "platform.work-aggregation.inbox.view";
}

// Disabled-action reason codes surfaced by the projection (stable, localizable on the frontend).
public static class WorkAggregationReasonCodes
{
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string EvidenceRequired = "EVIDENCE_REQUIRED";
}

// Mirror of the executable contract's enumerations (fixture-contract.js). Used by the projection AND by the
// tests as the conformance oracle so the C# projection cannot drift from the JS contract silently.
public static class WorkItemContract
{
    public const string FixtureKindWorkItem = "workItem";

    // workIntent
    public const string IntentApproval = "approval";

    // assignmentMode
    public const string AssignmentApproval = "approval";

    // ownershipState / admissionState / taskLifecycle / executionState / timerState
    public const string NotApplicable = "notApplicable";

    // normalizedStatus
    public const string StatusPending = "Pending";
    public const string StatusInProgress = "InProgress";
    public const string StatusWaiting = "Waiting";
    public const string StatusDone = "Done";
    public const string StatusCancelled = "Cancelled";

    // systemState
    public const string SystemFresh = "fresh";

    // actionDepth
    public const string DepthInline = "inline";
    public const string DepthDeeplink = "deeplink";

    // label kind
    public const string LabelResource = "resource";

    // action source
    public const string ActionSourceProvider = "provider";

    // lifecycle owner (differs from the source business object's module → workflow)
    public const string LifecycleOwnerWorkflow = "workflow";

    // source provider code (MOD-0023 provider)
    public const string ProviderCodeWorkflow = "workflow";

    public static readonly string[] NormalizedStatuses =
        [StatusPending, StatusInProgress, StatusWaiting, StatusDone, StatusCancelled];
}

// A discriminated resource label: { kind: "resource", key, args }. The projection always uses the resource
// form so the same payload localizes in all seven tenant languages (wiring is WC-1b).
public sealed record WorkItemLabelDto(
    string Kind,
    string Key,
    IReadOnlyDictionary<string, string>? Args)
{
    public static WorkItemLabelDto Resource(string key, IReadOnlyDictionary<string, string>? args = null)
        => new(WorkItemContract.LabelResource, key, args);
}

// nativeStatus { code, label } — the provider's raw status code plus a localizable label. The raw code is
// never parsed to infer normalized lifecycle/eligibility.
public sealed record WorkItemNativeStatusDto(string Code, WorkItemLabelDto Label);

// source { providerCode, providerContractVersion, objectType, objectId, deepLink? }. objectType/objectId come
// from the joined WorkflowInstance; deepLink is provider-owned and null in the MOD-0023-only phase.
public sealed record WorkItemSourceDto(
    string ProviderCode,
    string ProviderContractVersion,
    string ObjectType,
    string ObjectId,
    string? DeepLink);

// One entry of the single authoritative actions[] array. Contract-conformant: unique code, localizable label,
// explicit enabled + source; a disabled action carries disabledReasonCode + a localizable disabledReason.
// No per-action concurrency token is ever emitted (the projection carries one concurrency token).
public sealed record WorkItemActionDto(
    string Code,
    WorkItemLabelDto Label,
    string SemanticType,
    bool Enabled,
    string Source,
    string? DisabledReasonCode,
    WorkItemLabelDto? DisabledReason,
    bool RequiresConfirmation,
    bool RequiresReason,
    bool RequiresEvidence,
    bool SupportsBulk,
    string RiskLevel);

// waitingContext { type, waitingOn?, since?, expectedUntil? } — present iff normalizedStatus == Waiting.
public sealed record WorkItemWaitingContextDto(
    string Type,
    string? WaitingOn,
    DateTimeOffset? Since,
    DateTimeOffset? ExpectedUntil);

// concurrency { kind, token } — one projection-level optimistic-concurrency token (from the provider's
// technical Version). A future command envelope copies this token; the projection never repeats it per action.
public sealed record WorkItemConcurrencyDto(string Kind, string Token);

// Provisional escalation signal (charter §10.1: "Escalated → Pending + escalation signal (chip/notice, not a
// status)"). The executable contract has no dedicated escalation field yet, so this is an ADDITIVE, validator-
// ignored signal; the canonical UI/contract representation of the chip is a WC-1b / contract-owner follow-up.
public sealed record WorkItemEscalationDto(bool Escalated, int Level, DateTimeOffset? Since);

// The canonical, source-agnostic work-item projection. Field-by-field conformant to fixture-contract.js
// (validateWorkItem). Approval items carry notApplicable lifecycle/execution/timer, an empty capability set,
// and — when actionable — the effective approval actions. Personal overlay (pin/snooze/note) is intentionally
// absent: it is owned by the frontend WorkCenter layer, not this backend projection.
public sealed record WorkItemProjectionDto(
    string FixtureKind,
    string Id,
    string WorkIntent,
    string AssignmentMode,
    string OwnershipState,
    string AdmissionState,
    string NormalizedStatus,
    string TaskLifecycle,
    string ExecutionState,
    string TimerState,
    string SystemState,
    string ActionDepth,
    WorkItemLabelDto Title,
    WorkItemNativeStatusDto NativeStatus,
    WorkItemSourceDto Source,
    string LifecycleOwner,
    IReadOnlyList<string> WorkItemCapabilities,
    IReadOnlyList<WorkItemActionDto> Actions,
    WorkItemConcurrencyDto Concurrency,
    WorkItemWaitingContextDto? WaitingContext,
    WorkItemEscalationDto? Escalation,
    DateTimeOffset? DueAt);

// The caller's effective context, assembled by the API layer from the authenticated principal. UserId is
// resolved server-side (never from the client payload); permission flags are evaluated from the principal's
// claims via the existing PermissionClaimEvaluator seam. A platform actor passes every permission.
public sealed record WorkItemActor(
    Guid UserId,
    bool IsPlatformActor,
    IReadOnlySet<string> GrantedPermissions)
{
    // Effective permission check mirroring the API enforcement semantics: platform actors pass all; otherwise
    // the key must be in the granted set the controller evaluated from claims.
    public bool Has(string permissionKey) => IsPlatformActor || GrantedPermissions.Contains(permissionKey);
}
