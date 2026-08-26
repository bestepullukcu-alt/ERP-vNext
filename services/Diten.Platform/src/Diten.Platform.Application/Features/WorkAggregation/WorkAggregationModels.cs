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

    // slaState (WC-2). The C# mirror of fixture-contract.js SLA_STATES — one vocabulary, declared on both sides,
    // because a value spelled differently across the seam is the defect this session met four times.
    public const string SlaOverdue = "overdue";
    public const string SlaDueSoon = "due-soon";
    public const string SlaOnTrack = "on-track";
    public const string SlaNoSla = "no-sla";

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
// and — when actionable — the effective approval actions.
//
// PERSONAL OVERLAY — THE DECISION THAT CHANGED, AND WHY (2026-08-14).
//
// This comment used to read: "Personal overlay (pin/snooze/note) is intentionally absent: it is owned by the
// frontend WorkCenter layer, not this backend projection." That was a real decision and it was never wrong on its
// own terms. What made it a defect is that the OTHER half never happened: the frontend layer it handed ownership
// to wrote to nowhere at all. A note lived in a JavaScript object until the next reload, a snooze with it — and
// the screen said "Not kaydedildi" over the top. Half a decision, whose visible shape was a save confirmation for
// a save that did not occur.
//
// So `personal` is now projected (snooze + note list, per reader), and the ownership line moved rather than
// vanished: the SHELL still decides what a personal layer is FOR — what it looks like, when it is offered, what
// pinning means — and the engine now stores it, because storing is not a presentation concern and there was
// nowhere else for it to go.
//
// PIN is deliberately still absent. Unlike a note and a snooze it has no behaviour behind it yet on either side;
// projecting a field nothing writes and nothing reads would recreate exactly the half this change closes.
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
    /// The task TYPE this work was opened under (DCP-005 slice 1), or absent for the tasks that predate types.
    ///
    /// <para>⚠ ABSENT IS NORMAL AND STAYS NORMAL — every task open today has no type, and the field is nullable
    /// for exactly that reason. A surface must render its absence, not treat it as a fault.</para>
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemTaskTypeDto? TaskType = null,
    /// <summary>
    /// WHICH queue this work is waiting in. Present exactly when <c>assignmentMode</c> is <c>groupQueue</c>: the
    /// Pool tab's entire question is "which queue is this in", and an item that cannot answer it makes the tab
    /// meaningless — it was answered for a while with a fabricated team name (BL-031).
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemPoolDto? Pool = null,
    /// <summary>
    /// How this work stands against its deadline: overdue | due-soon | on-track | no-sla (WC-2).
    ///
    /// <para>Computed on the SERVER, through <c>IWorkingTimeCalculator</c>, because it is a decision and the
    /// surface's own law is that the browser renders decisions rather than making them. It used to be derived in
    /// <c>mock-data.js</c> from calendar-day subtraction against a hard-coded threshold, which meant the working
    /// calendar (BL: Calendar) had nothing on the server to arrive at.</para>
    ///
    /// <para><b>No remaining-day COUNT travels with it, deliberately.</b> A count computed on the server is
    /// frozen the moment it is serialized: a tab left open overnight would still read "due in 2 days". This
    /// project already shipped that exact shape once (the <c>ago</c> field) and banned it. The absolute
    /// <see cref="DueAt"/> is on the wire already, so the client derives the WORDS late from it and takes only
    /// the STATE from here.</para>
    ///
    /// <para>Optional and omitted when null: a provider that does not track deadlines (MOD-0023's own approval
    /// items) says nothing rather than claiming <c>on-track</c>, and — the BL-038 lesson — the contract rule
    /// validates this field only when it is PRESENT, so a silent provider's items are never dropped.</para>
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SlaState = null,
    /// <summary>
    /// The configurable field values, grouped into the sections the definitions declare (Phase 5).
    ///
    /// <para>Container ⇔ the <c>businessContext</c> capability, BOTH ways, like every other Phase 2 container.
    /// That coupling is not decoration: the capability was declared without this field for a while, and the
    /// contract's CAPABILITY_CONTAINER_REQUIRED made <c>validateItems</c> DROP the whole item — so a task that
    /// had configurable values disappeared from the surface entirely. Half a capability is worse than none.</para>
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemBusinessContextDto? BusinessContext = null,
    /// <summary>
    /// WHEN this work finished — present only on terminal items (BL-046).
    ///
    /// <para>This is the one exception the "no day count on the wire" rule above needs, and it is not an
    /// exception at all: what travels is an ABSOLUTE instant, exactly like <see cref="DueAt"/>. The client
    /// subtracts the two and gets a number that is a fact about the task rather than a fact about today.</para>
    ///
    /// <para>Without it the History tab read "Completed · 11 days late" one morning and "12 days late" the next
    /// about a task nobody had touched, because the only date the client had to measure from was today. Freezing
    /// the <see cref="SlaState"/> alone was not enough — half of that fix shipped once, and the screen then said
    /// "-2 days LEFT". The state and the instant are one change.</para>
    ///
    /// <para>Optional and omitted when null, for the usual reason: a provider whose records carry no closing
    /// timestamp must be able to stay silent rather than have its work dropped. The client then reports the
    /// lateness WITHOUT a number instead of quoting one that would drift.</para>
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? ClosedAt = null,
    /// <summary>
    /// WHAT THE WORK IS, in the requester's own words.
    ///
    /// <para>Measured 2026-08-12: the detail page could say "15 days overdue" and could not say what the work
    /// WAS — the create form has collected a description since Phase 1 and the projection never carried it, so
    /// the one question a detail page exists to answer had no data behind it.</para>
    ///
    /// <para>A DISPLAY label, exactly like <see cref="Title"/>: it is text a person typed, not a resource key
    /// this module owns. Omitted when the description is absent OR blank — a whitespace-only description would
    /// otherwise render as an empty paragraph under a heading.</para>
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemLabelDto? Summary = null,
    /// <summary>
    /// The doer's own start date, the companion to <see cref="EstimateHours"/> and the counterpart of
    /// <see cref="DueAt"/>: the deadline is the requester's commitment, the start and the estimate are the plan
    /// of whoever does the work. Omitted when nobody has stated one.
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? StartAt = null,
    /// <summary>How long the doer expects this to take, in hours. Omitted when unestimated — zero is a real
    /// estimate and must not be how "nobody said" is spelled.</summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    decimal? EstimateHours = null,
    /// <summary>
    /// How many hours have actually gone into this, the counterpart of <see cref="EstimateHours"/>.
    /// </summary>
    /// <remarks>
    /// ⚠ ADDED 2026-08-24 (Tur B). MEASURED: the effort card on the detail page has existed and never once
    /// rendered — it reads <c>item.effort.spent</c> against <c>item.effort.estimate</c>, and while the create
    /// form collects both (<c>FieldEstimateHours</c>, <c>FieldSpentHours</c>) and <c>TaskItem</c> stores both
    /// (<c>EstimateHours</c>, <c>SpentHours</c>), only the estimate ever reached this DTO. The card was
    /// waiting on a field nobody had carried across.
    ///
    /// <para>
    /// ⚠ NOT NULLABLE ON THE ENTITY, so it is not nullable here: <c>TaskItem.SpentHours</c> is a
    /// <c>decimal</c>, and zero spent is a real answer ("nobody has worked on this yet"), not an absence.
    /// It is still omitted from the wire when zero AND no estimate exists — see the provider.
    /// </para>
    /// </remarks>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    decimal? SpentHours = null,
    /// <summary>
    /// The task's own tags. Omitted when there are none: an empty array would reach the client as a present
    /// container and render an empty chip strip, which is the same "labelled blank" this round removes
    /// everywhere else.
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Tags = null,
    /// <summary>
    /// THE READER'S OWN LAYER over this work: their private notes and their own snooze. See
    /// <see cref="WorkItemPersonalDto"/> — and see the note above the record for why this field's ABSENCE used to
    /// be a documented decision and is no longer one.
    ///
    /// <para>Per-VIEWER content on a per-item projection, which is the one thing in here that is not a fact about
    /// the task. That is safe only because the projection is already built for one actor (<c>WorkItemActor</c>)
    /// and never cached across readers; the repository ANDs the user id into the read, so the wrong reader's
    /// overlay cannot be assembled even by mistake.</para>
    ///
    /// <para>Omitted entirely when this reader has neither snoozed the task nor written a note on it — the
    /// overwhelming majority of items. An empty container on every row would put a personal layer on the wire for
    /// work nobody has laid one over.</para>
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemPersonalDto? Personal = null,
    /// <summary>
    /// WHO ELSE IS WATCHING. Visibility only — a watcher never gains an action (pack §12 K3), so this is
    /// reported, never acted on.
    ///
    /// <para>Collected by the create form since Phase 1 and never projected: the form could name watchers and no
    /// surface could name them back, which is the same store-it-and-never-show-it shape the plan date was in
    /// before f8d10259. Omitted when there are none rather than emitted empty.</para>
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<WorkItemWatcherDto>? Watchers = null,
    /// <summary>
    /// Whether this task MAY be handed to someone else. A policy flag only — eligibility remains MOD-0018's
    /// decision (pack §12 Y5), so nothing here should be read as "this delegation would succeed".
    ///
    /// <para>Nullable, and null means "this provider does not express delegation policy" rather than "no". A
    /// non-nullable bool would have every provider that has never heard of delegation assert that it is
    /// forbidden.</para>
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? DelegationAllowed = null,
    /// <summary>
    /// WHAT THIS TASK EMAILS ABOUT. See <see cref="WorkItemNotificationsDto"/> for why the event list is
    /// nullable INSIDE a non-null container.
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemNotificationsDto? Notifications = null,
    /// <summary>
    /// How many days BEFORE the deadline the due-soon reminder is sent; null when no reminder was asked for.
    ///
    /// <para>A COUNT OF DAYS, never a computed instant — the engine stores it that way on purpose (BL-030) so the
    /// reminder survives the due date moving, and a projected instant would freeze on the wire the way the banned
    /// <c>ago</c> field did.</para>
    ///
    /// <para>Kept OUT of <see cref="Notifications"/> deliberately: a reminder fires on a schedule while the
    /// notification preferences answer "which events", and folding a fifth thing into a container named for the
    /// other four is how a field ends up somewhere nobody looks for it.</para>
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? ReminderLeadDays = null);

/// <summary>
/// WC-1 — the personal overlay, projected. Private to ONE reader: the server filters it, the client does not hide
/// it. Another person's note is not sent and then concealed; it is never read.
/// </summary>
public sealed record WorkItemPersonalDto(
    /// <summary>
    /// When this reader's own snooze runs out, or null. It never appears in <c>normalizedStatus</c>,
    /// <c>taskLifecycle</c> or <c>waitingContext</c> — the contract rejects exactly that
    /// (<c>SNOOZE_MUST_NOT_CREATE_WAITING</c>), because a requester must not be able to tell that the holder
    /// parked their request.
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? SnoozedUntil,
    /// <summary>
    /// Whether this reader has pinned the task. Personal in exactly the way the snooze above is: the requester
    /// cannot observe it and it changes nothing about the task.
    /// </summary>
    bool Pinned,
    /// <summary>
    /// This reader's notes, OLDEST FIRST — the order they were written, which is the order they read as a train
    /// of thought. Always present when the container is (declared-and-empty is a state; a half is not).
    /// </summary>
    IReadOnlyList<WorkItemPersonalNoteDto> Notes);

/// <summary>
/// One private note. <c>Text</c> is a plain string rather than a <see cref="WorkItemLabelDto"/>: a label carries
/// the resource-vs-display distinction, and a note has no second possibility — a person wrote it, in their own
/// words, and there is nothing here that could ever be a resource key.
/// </summary>
public sealed record WorkItemPersonalNoteDto(string Id, string Text, DateTimeOffset CreatedAt);

/// <summary>
/// One watcher. The person shape is reused from <see cref="WorkItemPersonDto"/> so a watcher renders with the
/// same name resolution (and the same "Me" rule) as an assignee — a second person shape here would be a second
/// place for a display name to go missing.
/// </summary>
public sealed record WorkItemWatcherDto(
    WorkItemPersonDto Person,
    /// <summary>The engine's own <c>TaskWatcherRole</c> spelling — Watcher | Consultant | Informed.</summary>
    string Role);

/// <summary>
/// The task's email notification preferences.
///
/// <para><c>Events</c> is NULLABLE inside a non-null container, and the distinction is the whole point: null
/// means nobody ever chose, and every dispatchable event is sent; an EMPTY list means the owner chose none.
/// Collapsing the two would either silence a task nobody configured or claim a choice nobody made. The entity
/// carries the same nullable for the same reason — see <c>TaskItem.NotifyOnEvents</c>.</para>
///
/// <para><c>EmailEnabled</c> is the master switch: false means nothing is sent whatever <c>Events</c> lists.</para>
/// </summary>
public sealed record WorkItemNotificationsDto(
    bool EmailEnabled,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Events);

/// <summary>The configurable-field container. Sections cap at six — the contract's LIMITS.maxSections.</summary>
public sealed record WorkItemBusinessContextDto(IReadOnlyList<WorkItemBusinessSectionDto> Sections);

/// <summary>
/// One section. <c>Title</c> follows the same two-source label rule the fields do: a section name typed by a
/// tenant administrator is DISPLAY content, not a resource key we own.
/// </summary>
public sealed record WorkItemBusinessSectionDto(
    WorkItemLabelDto Title,
    IReadOnlyList<WorkItemBusinessFieldDto> Fields);

/// <summary>
/// One configurable value, rendered.
///
/// <para><c>ValueType</c> is the CONTRACT's spelling — lowercase (<c>text</c>, <c>datetime</c>) — not the
/// engine's PascalCase enum. The two vocabularies were declared to match value-for-value on purpose, and
/// shipping the enum's casing straight onto the wire is the shape that has already cost this module twice.</para>
/// </summary>
public sealed record WorkItemBusinessFieldDto(
    WorkItemLabelDto Label,
    string ValueType,
    string? Value,
    string Importance,
    /// <summary>
    /// BL-024 Phase 2 — the reader may not see this value, so it was withheld ON THE SERVER.
    ///
    /// <para>The executable contract has validated this since it was written — <c>REDACTED_VALUE_MUST_BE_OMITTED</c>
    /// fails any item that ships <c>redacted: true</c> next to a value — and nothing could ever set it, because
    /// the DTO had no such field. The rule was enforceable and unreachable at the same time.</para>
    ///
    /// <para>The field's LABEL still travels. What is secret is the content, not the existence: the catalogue is
    /// readable, so hiding the row would buy nothing and would make a withheld value indistinguishable from a
    /// field the task does not have.</para>
    /// </summary>
    bool Redacted = false);

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
/// One entry in the activity feed. MOD-0024 emits BOTH contract kinds: <c>comment</c> for what a person wrote,
/// <c>event</c> for what happened to the task.
///
/// <para><b>What changed, and what did not (WC-1).</b> This comment used to say there was no lifecycle event log
/// to draw from, and that deriving a timeline from the four timestamps a task carries would silently omit accept,
/// plan, claim, release and inquire — a partial history read as a complete one. The objection was never answered
/// by deriving more cleverly; it was answered by RECORDING. <c>TaskTransition</c> is now written on every task
/// write that moves the task, decided from the document's pre-image rather than from the writer's memory, so the
/// events published here are the ones that actually happened. <b>Nothing is derived from the timestamps, then or
/// now</b> — and a task written before that log existed still has no history, which the screen states outright
/// instead of filling in.</para>
///
/// <para><c>At</c> is ABSOLUTE, and there is deliberately no "3 days ago" field. A relative count computed on the
/// server is already stale by the time it is rendered, and stays wrong for as long as the tab is open — the same
/// defect class as a frozen "today".</para>
/// </summary>
public sealed record WorkItemActivityEntryDto(
    string Id,
    string Kind,
    /// <summary>
    /// The comment text — what a person typed, so never a resource key.
    ///
    /// <para>Optional because an EVENT has no text of its own: its sentence is built client-side from
    /// <see cref="Event"/>, in the reader's language. The executable contract already required text for
    /// <c>comment</c> only (ACTIVITY_COMMENT_TEXT_REQUIRED), so an event carrying none was always the intended
    /// shape. Sending an event's sentence from here instead would ship one server-side language to seven.</para>
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Text,
    /// <summary>
    /// For a comment: the author's name as recorded when it was written. For an event: who performed it, resolved
    /// as they are named TODAY. The difference is deliberate and lives on <c>TaskTransition</c> — a comment is a
    /// quotation, an event is a fact about an identity.
    ///
    /// <para>Null when the person could not be resolved. Never a GUID: an id is not a person.</para>
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Actor,
    DateTimeOffset At,
    /// <summary>Present on <c>kind: "event"</c> only, absent on a comment.</summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemActivityEventDto? Event = null,
    /// <summary>
    /// When the author last rewrote this comment — THE TRAIL that made editing acceptable at all (2026-08-14).
    ///
    /// <para>Comments were immutable, and the property that protected was "nothing changes silently". An edit
    /// that says WHEN it happened keeps that property: a reader can tell whether the sentence moved before or
    /// after they last read it, which a bare "edited" flag cannot answer.</para>
    ///
    /// <para>An INSTANT, like every other time on this feed, never a pre-computed phrase — the client derives
    /// the words late, in its own language.</para>
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? EditedAt = null,
    /// <summary>
    /// When the author withdrew this comment. The entry SURVIVES with no <see cref="Text"/>: the words are gone
    /// and the marker stays, so the feed still says somebody spoke here and took it back.
    ///
    /// <para>This is why the contract's <c>ACTIVITY_COMMENT_TEXT_REQUIRED</c> now exempts a withdrawn entry —
    /// a tombstone with text would not be a tombstone.</para>
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? WithdrawnAt = null,
    /// <summary>
    /// May THIS reader rewrite or withdraw this entry? The server decides; the client only draws.
    ///
    /// <para>False for everybody but the author, for an event, and for an already-withdrawn comment. Sent rather
    /// than derived client-side because the client has only the author's NAME — two people with one name would
    /// otherwise be handed each other's controls, and the handler would then refuse a button the screen offered.</para>
    /// </summary>
    bool Editable = false);

/// <summary>
/// What happened, as CODES rather than as a sentence (WC-1).
///
/// <para><c>Code</c> is the transition's stable name (<c>accepted</c>, <c>planned</c>, <c>released</c>…); the
/// client maps it to a localized line. A sentence composed here would be composed in one language, and this
/// product ships seven — the same rule every other label on this contract follows.</para>
///
/// <para><c>From</c> and <c>To</c> are the lifecycle either side of the act, in the engine's own spelling (the
/// contract's TASK_LIFECYCLES). They travel even though today's row renders only the act itself: they are the
/// substance of a transition, and a record that says an act occurred without saying between what is the partial
/// history this whole feature exists to stop shipping.</para>
///
/// <para><c>Reason</c> is the actor's own words when the act required them — a wait, a return, a reassignment.
/// TEXT, never a resource key, and absent rather than empty when none was given.</para>
/// </summary>
/// <summary>
/// ONE field that changed, inside an <c>edited</c> event.
///
/// <para><b>The values are already filtered when they get here.</b> A reader who may not see a field's VALUE may
/// not see its history either — otherwise the log is a back door around BL-024's field authorization. The
/// projection asks the same <c>TaskFieldAccessRules</c> the value itself goes through, and a field that fails it
/// arrives with <see cref="Redacted"/> set, no values, and NO NAME: the name alone can leak ("Salary band"
/// tells you the task carries salary data). What survives is that somebody edited something, which the entry's
/// actor and timestamp already say.</para>
/// </summary>
public sealed record WorkItemFieldChangeDto(
    /// <summary>
    /// The stable field code (<c>dueAt</c>, <c>priority</c>, <c>customField</c>…), or ABSENT when the reader may
    /// not know which field this was. The client turns it into a sentence in its own language.
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Field,
    /// <summary>The label for a tenant-defined field, when the reader may see it. Never the raw code.</summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WorkItemLabelDto? Label,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? From,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? To,
    /// <summary>
    /// The values were too long to keep at write time, so this row says only that the field changed. DIFFERENT
    /// from <see cref="Redacted"/>: nobody is being kept out, there is simply nothing short to show.
    /// </summary>
    bool ValuesOmitted = false,
    /// <summary>THIS reader may not see this field. The values are omitted and so is the field's identity.</summary>
    bool Redacted = false);

public sealed record WorkItemActivityEventDto(
    string Code,
    string From,
    string To,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Reason = null,
    /// <summary>
    /// WHICH FIELDS this act changed. Present on an <c>edited</c> event, and also on any other act whose save
    /// happened to move a recorded field — a reassign carries its own code AND the field row, from one entry.
    ///
    /// <para>Omitted when nothing recorded changed, which is most acts. Already filtered for THIS reader — see
    /// <see cref="WorkItemFieldChangeDto"/>.</para>
    /// </summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<WorkItemFieldChangeDto>? FieldChanges = null);

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
/// The TASK TYPE a work item was opened under (DCP-005 slice 1), or absent.
///
/// <para>Code and name travel together for the reason the frozen document reference gives: an id alone is not
/// something a person can read, and re-resolving it on the client would be a second authority over the same
/// fact.</para>
/// </summary>
public sealed record WorkItemTaskTypeDto(string Id, string Code, string Name);

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
    bool EvidenceRequired,
    /// <summary>
    /// May the CALLER change this item — reword it, re-level it, re-flag it, remove it? Ticking is not covered:
    /// doing the work is everyone's.
    ///
    /// <para>Sent as a DECIDED ANSWER rather than an author id the client would compare for itself. Two reasons.
    /// The rule then exists once, on the side that enforces it, instead of twice with a chance to disagree — and
    /// nothing has to publish who wrote which line to every reader of the list. Defaulted true so that a
    /// provider which has no concept of authorship keeps behaving as it did.</para>
    /// </summary>
    bool Editable = true);

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
/// <summary>
/// BL-023 — WHOSE work the caller is asking about.
///
/// <para>Not a tab and not a filter: the axis law is locked (tab = ownership, segment = state, chip = type +
/// signal), so a manager wanting to see their team changes the SCOPE and keeps every tab exactly as it was. This
/// is the SAP My Inbox shape.</para>
/// </summary>
public enum WorkItemScope
{
    /// <summary>Work that is mine — assigned to me, or pooled to a position I hold. The default and the
    /// behaviour every caller had before this existed.</summary>
    Self = 0,

    /// <summary>
    /// My subordinates' OWN work, including tasks I never assigned. Deliberately NOT a superset of
    /// <see cref="Self"/>: merging the two would double every row and answer neither question.
    /// </summary>
    Team = 1
}

public sealed record WorkItemActor(
    Guid UserId,
    bool IsPlatformActor,
    IReadOnlySet<string> GrantedPermissions)
{
    /// <summary>
    /// BL-023 — whose work to list. Additive with a <see cref="WorkItemScope.Self"/> default so every existing
    /// provider and caller keeps its behaviour unchanged; a provider that has no team concept simply ignores it
    /// (WorkflowApprovalWorkItemProvider does, and says so).
    /// </summary>
    public WorkItemScope Scope { get; init; } = WorkItemScope.Self;

    // Effective permission check mirroring the API enforcement semantics: platform actors pass all; otherwise
    // the key must be in the granted set the controller evaluated from claims.
    public bool Has(string permissionKey) => IsPlatformActor || GrantedPermissions.Contains(permissionKey);
}
