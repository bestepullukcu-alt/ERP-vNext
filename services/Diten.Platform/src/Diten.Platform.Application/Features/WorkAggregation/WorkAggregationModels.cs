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

    /// <summary>An unsatisfied dependency edge. Used as the ACTION's disabled reason and as the BLOCKER's code,
    /// deliberately the same string: they describe one fact from two directions.</summary>
    public const string DependencyBlocked = "DEPENDENCY_BLOCKED";

    /// <summary>An open subtask. Blocks COMPLETION only — its parent can still be started, and still cancelled.</summary>
    public const string SubtaskBlocked = "SUBTASK_BLOCKED";
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

/// <summary>
/// waitingContext { type, waitingOn?, reason?, since?, expectedUntil? } — present iff normalizedStatus == Waiting.
///
/// <para><b>waitingOn and reason are different questions.</b> <c>waitingOn</c> answers WHO/WHAT we are waiting on
/// and is a typed identity ({id, displayName}) that the client renders as a person. <c>reason</c> answers WHY, in
/// the user's own words. Putting the reason text into <c>waitingOn</c> made the client read
/// <c>waitingOn.displayName</c> off a string and render nothing at all — the sentence the user typed was on the
/// wire and invisible.</para>
///
/// <para>There is no directory seam that resolves a waiting-on identity yet, so <c>waitingOn</c> is null today.
/// It stays declared rather than removed: null is "we do not know", which is the truth, and the field is where a
/// real identity belongs when one exists.</para>
/// </summary>
public sealed record WorkItemWaitingContextDto(
    string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemPersonDto? WaitingOn,
    /// <summary>
    /// Why the work is parked, as the holder typed it — a DISPLAY label, never a resource key. Routing user text
    /// through a key is what puts the raw key on screen.
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemLabelDto? Reason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? Since,
    /// <summary>
    /// When the WAIT is expected to end. Omitted unless something actually knows it: nothing collects this today,
    /// and filling it from the task's own due date announced "waiting until 22 July" on a date already in the past.
    /// Giving `inquire` a date of its own is a separate decision.
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
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
    string? ParentTaskItemId = null,
    /// <summary>
    /// Governance gates, REPORTED so the holder can see why work is waiting. Optional and trailing, so a provider
    /// that has no gates (MOD-0023's own items) serializes unchanged and the field is simply absent.
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemGatesDto? Gates = null,
    /// <summary>
    /// How urgent the work is: Low|Medium|High, the engine's own <c>TaskPriority</c> spelling (BL-032, owner
    /// decision 2026-07-29). Optional and omitted when null, because a provider that does not rank its work must
    /// say nothing rather than imply Medium.
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Priority = null,
    /// <summary>
    /// Typed dependency edges, read-only. Container ⇔ the <c>dependencies</c> capability, like every other
    /// Phase 2 container.
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<WorkItemDependencyDto>? Dependencies = null,
    /// <summary>
    /// What is stopping this work, and which actions it stops. Absent when nothing blocks — a
    /// <c>blocked: false</c> object would make every unblocked item carry a blocked state.
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemBlockedStateDto? BlockedState = null,
    /// <summary>
    /// The activity feed. Container ⇔ the <c>activity</c> capability: declared and empty is a valid state (a task
    /// nobody has commented on yet), declared-without-container and container-without-capability are both contract
    /// errors.
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<WorkItemActivityEntryDto>? Activity = null,
    /// <summary>
    /// The holder's OWN plan for when to do this, distinct from <see cref="DueAt"/> — the source's deadline and
    /// the basis for SLA. Optional and omitted when nobody has planned yet: a task nobody has scheduled has no
    /// plan, not today's date and not the due date.
    ///
    /// <para>This is a projection of what <c>POST .../plan</c> writes. Without it, a plan write would be REAL on
    /// the server and INVISIBLE on the screen — the reader could never see their own plan again, and re-planning
    /// could never seed from the date they actually chose, only ever from the source due date. That is the same
    /// half-a-feature shape a declared-but-empty capability with no container would be.</para>
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? PlannedDate = null,
    /// <summary>
    /// WHICH queue this work is waiting in. Present exactly when <c>assignmentMode</c> is <c>groupQueue</c>: the
    /// Pool tab's entire question is "which queue is this in", and an item that cannot answer it makes the tab
    /// meaningless — it was answered for a while with a fabricated team name (BL-031).
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemPoolDto? Pool = null);

/// <summary>
/// The queue a pooled item waits in (WC-3 / BL-031).
///
/// <para><c>Label</c> is a DISPLAY label: a position's name is data someone typed, not translatable content, so
/// routing it through a resource key would put the raw key on screen.</para>
///
/// <para><b>Label may be null while Id is not.</b> A position that cannot be read — archived out from under the
/// task, or in an organization unit that no longer resolves — leaves the queue UNNAMED rather than taking either
/// wrong exit: printing the GUID as if it were a team name, or omitting the field so the contract drops the task
/// and it vanishes from the Pool tab entirely. The identity stays on the wire; the screen just shows no queue
/// name for it.</para>
/// </summary>
public sealed record WorkItemPoolDto(
    string Id,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemLabelDto? Label);

/// <summary>
/// One entry in the activity feed. Today MOD-0024 emits only <c>kind: "comment"</c>: there is no lifecycle event
/// log to draw from, and deriving a timeline from the four timestamps a task happens to carry
/// (created/started/completed/cancelled) would silently omit accept, plan, claim, release and inquire. A partial
/// history is worse than none, because it is read as complete.
///
/// <para><c>At</c> is ABSOLUTE, and there is deliberately no "3 days ago" field. A relative count computed on the
/// server is already stale by the time it is rendered, and stays wrong for as long as the tab is open — the same
/// defect class as a frozen "today".</para>
/// </summary>
public sealed record WorkItemActivityEntryDto(
    string Id,
    string Kind,
    /// <summary>The comment text — what a person typed, so never a resource key.</summary>
    string Text,
    /// <summary>Author's name as recorded when it was written, or null when it could not be resolved.</summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Actor,
    DateTimeOffset At);

/// <summary>
/// One typed dependency edge. <c>State</c> is the OTHER task's state in the subtask vocabulary
/// (not-started|in-progress|done|cancelled) because that is the same question, and <c>Direction</c> says which
/// way the arrow points: <c>pred</c> is a task this one waits on, <c>succ</c> is one that waits on this.
/// </summary>
public sealed record WorkItemDependencyDto(
    string Id,
    WorkItemLabelDto Title,
    string Type,
    string State,
    string Direction,
    /// <summary>Whether this edge is what actually holds the work up right now.</summary>
    bool Blocking);

/// <summary>
/// blockedState { blocked, affectedActionCodes[], blockers[] } — the shape the executable contract validates.
///
/// <para>Every code in <c>AffectedActionCodes</c> MUST appear in <c>actions[]</c>, disabled, with a reason: the
/// contract rejects a blocker that points at an action nobody can see. Blocked work therefore shows its button
/// greyed out WITH the reason beside it, never a hidden button — a control that vanishes teaches the reader
/// nothing about why.</para>
/// </summary>
public sealed record WorkItemBlockedStateDto(
    bool Blocked,
    IReadOnlyList<string> AffectedActionCodes,
    IReadOnlyList<WorkItemBlockerDto> Blockers);

/// <summary>
/// One reason work cannot move. <c>Label</c> names the thing in the way (a task title, so a DISPLAY label);
/// the three optional fields let the client build a typed sentence — "FS: X must close before this can start" —
/// without any localized text crossing the wire.
///
/// <para>They are optional because a blocker is not always a dependency: a blocking checklist item, and later a
/// subtask (BL-035), fit this same shape with those fields left null.</para>
/// </summary>
public sealed record WorkItemBlockerDto(
    string Code,
    WorkItemLabelDto Label,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? TaskItemId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? DependencyType = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? AffectedActionCode = null);

/// <summary>
/// The task's live checklist. An empty <c>items</c> list is valid when the capability is declared.
///
/// <para><c>Version</c> is the RUN's concurrency token, not the task's: ticking an item is an expected-version
/// write against the checklist. Without it on the wire the client cannot make that conditional write at all, so
/// two people ticking at once would silently overwrite each other.</para>
/// </summary>
public sealed record WorkItemChecklistDto(IReadOnlyList<WorkItemChecklistItemDto> Items, int Version);

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
    /// <summary>
    /// Contract vocabulary: done | in-progress | not-started | cancelled.
    ///
    /// <para><c>cancelled</c> is its own value and not folded into <c>not-started</c>: called-off work is not
    /// waiting to begin, and reading three cancelled subtasks as "not started" invites someone to go and do
    /// them. It is also the distinction BL-035 needs — a cancelled subtask must not gate its parent.</para>
    /// </summary>
    string Status,
    /// <summary>
    /// Who holds the subtask. A TYPED identity or null — a subtask nobody has taken genuinely has no holder, and
    /// the display name is null when Platform cannot resolve one rather than guessed.
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemPersonDto? Assignee = null,
    /// <summary>When the subtask is due, omitted when it has no date of its own.</summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? DueAt = null,
    /// <summary>
    /// Whether THIS actor may call the subtask off, evaluated by the same rule as any other task: the requester,
    /// or administrative authority. Sent per row because the shell cannot work it out — a subtask's requester is
    /// its own, not the parent's — and a row must not offer an action the server will refuse.
    /// </summary>
    bool CanCancel = false);

/// <summary>
/// The governance gates on a work item: what must happen before it can proceed, and where that stands.
///
/// <para><b>This REPORTS; it never decides.</b> The aggregator surfaces gate state so a holder can see why work
/// is waiting and on whom — it must never offer an approve/reject control and must never write gate state.
/// Approval and review decisions belong to MOD-0023 (charter Binding A); MOD-0024 was already caught growing a
/// second approval engine once, from a local flag rather than the workflow's actual state.</para>
///
/// <para>Modelled as a policy object beside <c>reviewMeetingPolicy</c> rather than as a separate fetch: the Task
/// Center aggregates many providers, so a detail page that called one provider's own API would work for that
/// provider alone.</para>
/// </summary>
public sealed record WorkItemGatesDto(WorkItemGateDto Approval, WorkItemGateDto Review);

/// <summary>
/// One gate. <see cref="Decider"/> is a TYPED identity or null — never a name this service guessed. Platform has
/// no user-directory seam, so the id crosses and the client renders what it can.
/// </summary>
public sealed record WorkItemGateDto(
    bool Required,
    /// <summary>notRequired | required | pending | approved | rejected.</summary>
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemPersonDto? Decider);

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
