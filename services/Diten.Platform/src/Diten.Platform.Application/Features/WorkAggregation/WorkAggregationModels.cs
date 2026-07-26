using System.Text.Json.Serialization;

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

    // A label whose text is already final — user-entered content that needs no translation. The executable
    // contract requires `text` + `locale` and FORBIDS `key` on this form.
    public const string LabelDisplay = "display";

    // BCP-47 "undetermined": correct for content typed by a user, whose language we do not record.
    public const string LocaleUndetermined = "und";

    // action source
    public const string ActionSourceProvider = "provider";

    // lifecycle owner (differs from the source business object's module → workflow)
    public const string LifecycleOwnerWorkflow = "workflow";

    // source provider code (MOD-0023 provider)
    public const string ProviderCodeWorkflow = "workflow";

    // source provider code (MOD-0024 provider). Deliberately identical to the module manifest's ModuleCode
    // ("tasks") and to the permission namespace (platform.tasks.*) so provider, catalog and permissions cannot
    // drift apart — the workflow provider holds the same property.
    public const string ProviderCodeTasks = "tasks";

    public static readonly string[] NormalizedStatuses =
        [StatusPending, StatusInProgress, StatusWaiting, StatusDone, StatusCancelled];
}

/// <summary>
/// A discriminated label, in one of two forms the executable contract accepts:
/// <list type="bullet">
///   <item><c>{ kind: "resource", key, args? }</c> — translated client-side; <c>text</c> must be ABSENT.</item>
///   <item><c>{ kind: "display", text, locale }</c> — already-final text; <c>key</c> must be ABSENT.</item>
/// </list>
///
/// <para>The absences are load-bearing: <c>fixture-contract.js</c> checks <c>label.text === undefined</c> for a
/// resource label and <c>label.key === undefined</c> for a display one, and an item failing validation is dropped
/// from the Task Center. A serialized <c>"text": null</c> is NOT undefined, so every optional member is omitted
/// when null rather than written as null.</para>
/// </summary>
public sealed record WorkItemLabelDto(
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Key = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string>? Args = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Text = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Locale = null)
{
    public static WorkItemLabelDto Resource(string key, IReadOnlyDictionary<string, string>? args = null)
        => new(WorkItemContract.LabelResource, Key: key, Args: args);

    /// <summary>
    /// Text that is already what the user should read — a title they typed themselves. Wrapping such text in a
    /// resource key would demand a translation entry per provider and render the raw key when one is missing.
    /// </summary>
    public static WorkItemLabelDto Display(string text, string? locale = null)
        => new(WorkItemContract.LabelDisplay,
            Text: text,
            Locale: string.IsNullOrWhiteSpace(locale) ? WorkItemContract.LocaleUndetermined : locale);
}

/// <summary>
/// A person on a work item — who it is assigned to, and who requested (created) it.
///
/// <para>Shape matches what the fixtures already carry (<c>{ id, displayName }</c>) because mock and real items go
/// through the SAME client mapper; a different shape there would render blank or drop the item.</para>
///
/// <para><c>DisplayName</c> is nullable and omitted when null: Platform has no user-directory seam, so it cannot
/// resolve an AuthService user's name yet (see the pack — it lands with the person-picker work). Until then
/// <c>IsCurrentUser</c> lets the client say "Me" without the server holding localized text, and without shipping
/// the caller's user id to the browser just to compare it.</para>
/// </summary>
public sealed record WorkItemPersonDto(
    string Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? DisplayName = null,
    bool IsCurrentUser = false);

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
    DateTimeOffset? DueAt,
    // Action PLACEMENT: which of actions[] is the row's primary button and which sit behind the ··· overflow.
    // Optional and trailing, so providers that do not express placement (MOD-0023 today) compile and serialize
    // unchanged and the shell keeps deriving it. Both must reference codes present in actions[] — the executable
    // contract rejects a dangling reference.
    string? PrimaryActionCode = null,
    IReadOnlyList<string>? OverflowActionCodes = null,
    // WHO the work belongs to. Optional so a provider that cannot supply them (MOD-0023 today) is unchanged, and
    // omitted when null — a serialized "assignee": null would reach the client as a present-but-empty object.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemPersonDto? Assignee = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemPersonDto? Requester = null,
    // ── Phase 2 containers ───────────────────────────────────────────────────
    // The contract couples capability and container BOTH ways: data present without its capability is
    // CAPABILITY_REQUIRED_FOR_DATA, and a declared capability with the field absent is
    // CAPABILITY_CONTAINER_REQUIRED. So these stay null (omitted) unless the capability is declared, and are
    // then emitted even when empty.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemChecklistDto? Checklist = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemSubtasksDto? Subtasks = null,
    /// <summary>Set when this item IS a subtask, so the shell can show whose subtask it is.</summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ParentTaskItemId = null);

/// <summary>The task's live checklist. An empty <c>items</c> list is valid when the capability is declared.</summary>
public sealed record WorkItemChecklistDto(IReadOnlyList<WorkItemChecklistItemDto> Items);

public sealed record WorkItemChecklistItemDto(
    string Id,
    WorkItemLabelDto Label,
    bool Completed,
    bool Required,
    /// <summary>Blocking items are the ONLY ones that gate completion; `required` alone does not.</summary>
    bool Blocking,
    /// <summary>MOD-0031 owns evidence itself; this is the flag only (pack §12 E1).</summary>
    bool EvidenceRequired);

/// <summary>
/// Subtasks. <c>mode: "full"</c> because MOD-0024 IS their source and may create/complete them here; a consumer
/// that merely mirrors someone else's subtasks would send "readonly" and deep-link instead.
/// </summary>
public sealed record WorkItemSubtasksDto(string Mode, IReadOnlyList<WorkItemSubtaskDto> Items);

public sealed record WorkItemSubtaskDto(
    string Id,
    /// <summary>Plain text: a subtask title is a real user-typed title, never a resource key.</summary>
    string Title,
    /// <summary>Contract vocabulary: done | in-progress | not-started.</summary>
    string Status);

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
