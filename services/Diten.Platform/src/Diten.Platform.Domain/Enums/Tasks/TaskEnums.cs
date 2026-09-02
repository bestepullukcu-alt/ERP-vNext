namespace Diten.Platform.Domain.Enums.Tasks;

// MOD-0024 — Task & Checklist Engine. Values are explicitly numbered because they are persisted; append only,
// never renumber. The lifecycle mirrors the executable contract's TASK_LIFECYCLES (fixture-contract.js) minus
// `notApplicable`, which is a projection value for non-task intents rather than a stored state.
//
// Every enum that crosses the HTTP boundary carries [JsonConverter(typeof(JsonStringEnumConverter))] — the
// convention already used by Enums/EntitlementSource.cs. Without it System.Text.Json accepts ONLY integers, so a
// browser sending assignmentTarget:"SelfAssigned" gets a 400 before the handler ever runs. The attribute is applied
// per enum rather than through a global AddJsonOptions/JsonSerializerOptions change, which would alter the wire
// format of every other module in this service.
// Enums NOT annotated are ones that never appear in a request or response body (responses expose Lifecycle,
// AssignmentTarget, Role and DependencyType as plain `string`, mapped explicitly in the DTO projections).

/// <summary>Native task lifecycle. SYSTEM-owned: a user never picks this directly (pack §12 Y2).</summary>
public enum TaskLifecycle
{
    Open = 0,
    Planned = 1,
    InProgress = 2,
    Waiting = 3,
    PendingReview = 4,
    Done = 5,
    Cancelled = 6
}

/// <summary>Crosses the wire in CreateTaskItemRequest and UpdateTaskItemRequest.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2
}

/// <summary>
/// Who the task is for (pack §12 K5). This drives the contract projection triple
/// (assignmentMode / ownershipState / admissionState) — see TaskAssignmentResolver.
/// Crosses the wire in CreateTaskItemRequest.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TaskAssignmentTarget
{
    /// <summary>Created for the creator: owned + admitted immediately.</summary>
    SelfAssigned = 0,

    /// <summary>Assigned to a specific user: assigned + pendingAcceptance.</summary>
    Person = 1,

    /// <summary>Offered to a POSITION's holders: unowned + pendingClaim (pack §12 K4).</summary>
    PositionPool = 2
}

/// <summary>
/// Every act that moves a task — the vocabulary of the lifecycle event log (WC-1).
///
/// <para><b>Why this exists next to <see cref="TaskAssignmentEventType"/> rather than inside it.</b> That enum
/// answers "who HELD this task", and its seven values are the ownership story alone: it has no word for planned,
/// started, waiting, submitted or completed, and widening it would make one collection answer two questions —
/// which is the shape that produced BL-042 and BL-051 on this very module. This one answers "what HAPPENED", and
/// the two are recorded side by side.</para>
///
/// <para><b>Complete by construction, not by care.</b> The repository — not the handler — decides that a
/// transition happened: <c>TaskItemRepository.UpdateAsync</c> replaces the document with its PRE-IMAGE in hand and
/// compares lifecycle, holder and acceptance mark. A write that moved any of them records an entry whether or not
/// the writer remembered to declare one. A handler that forgets therefore does not lose history; it produces
/// <see cref="Unknown"/>, which is what turns a test red.</para>
///
/// <para>That is deliberately NOT "derive the history from state", the thing the projection refused to do. The
/// diff decides only THAT something moved and between which two states — facts the two documents actually carry.
/// WHICH act it was, and why, is declared by the handler through <c>TaskItem.Declare(...)</c>, because a return
/// and a reassignment to the requester leave identical diffs and no amount of looking could tell them apart.</para>
///
/// <para>Persisted; append only, never renumber.</para>
/// </summary>
public enum TaskTransitionKind
{
    /// <summary>The task came into existence. The first entry in every log written from WC-1 onwards.</summary>
    Created = 0,

    /// <summary>The assignee took the work on — the Inbox acceptance gate closed (BL-042).</summary>
    Accepted = 1,

    /// <summary>A personal plan date was set or moved.</summary>
    Planned = 2,

    /// <summary>Work began.</summary>
    Started = 3,

    /// <summary>Work resumed from <see cref="TaskLifecycle.Waiting"/>. Distinct from <see cref="Started"/>
    /// because "picked this back up" and "began this" are different sentences to whoever reads the history.</summary>
    Resumed = 4,

    /// <summary>The holder parked the task, saying what it waits for.</summary>
    Waiting = 5,

    /// <summary>Finished work was handed to a reviewer (the MOD-0023 instance carries the decision, not this).</summary>
    SubmittedForReview = 6,

    /// <summary>The review requirement was withdrawn, so the task left PendingReview rather than waiting on nobody.</summary>
    ReviewCancelled = 7,

    Completed = 8,
    Cancelled = 9,

    /// <summary>A pooled task was taken out of its queue.</summary>
    Claimed = 10,

    /// <summary>A claimed pool task went back to its queue.</summary>
    Released = 11,

    /// <summary>The task moved to another person.</summary>
    Reassigned = 12,

    /// <summary>Assigned work was handed back to whoever asked for it.</summary>
    Returned = 13,

    /// <summary>
    /// A task MOVED and nobody said why — the diff saw it, the writer declared nothing.
    ///
    /// <para>Never written on purpose, and never dead code either: it is what a new transition produces on the day
    /// someone adds one without declaring its kind, and <c>TaskTransitionCoverageTests</c> fails on it. The
    /// alternative — refusing to record what could not be named — would put the silent hole back that this whole
    /// log exists to close, so an unnamed record is kept and made loud rather than dropped and made invisible.</para>
    /// </summary>
    Unknown = 14,

    /// <summary>
    /// Somebody changed WHAT the work is, or WHEN it is expected — a field edit rather than a lifecycle move.
    ///
    /// <para>Its own kind because it answers a different question. Every other value here says the task MOVED;
    /// this one says it stayed exactly where it was and something about it changed. Folding it into
    /// <see cref="Unknown"/> would have buried real edits under the code that exists to shout about undeclared
    /// transitions.</para>
    ///
    /// <para>ONE entry per SAVE, never per field — see <c>TaskTransition.FieldChanges</c>. "Ali moved the due
    /// date and raised the priority" is how a person remembers it; five rows is not.</para>
    /// </summary>
    Edited = 15
}

/// <summary>Assignment/ownership history event kinds (append-only audit of who held the task).</summary>
public enum TaskAssignmentEventType
{
    Created = 0,
    Assigned = 1,
    Accepted = 2,
    Claimed = 3,
    Released = 4,
    Delegated = 5,
    Reassigned = 6
}

/// <summary>
/// Typed dependency edge between two MOD-0024 tasks (pack §12 Y3 — own tasks only).
///
/// <para>String-serialized because it now CROSSES THE WIRE both ways (AddTaskDependencyRequest in, the work-item
/// projection out). Without the converter it would travel as 0..3, and the executable contract's
/// DEPENDENCY_TYPES are names — the same defect that once sent a numeric status to the Task Center.</para>
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TaskDependencyType
{
    FinishToStart = 0,
    FinishToFinish = 1,
    StartToStart = 2,
    StartToFinish = 3
}

/// <summary>
/// Participation that grants VISIBILITY but never action rights (pack §12 K3 / OD-4: summary, read-only).
/// Phase 1 persists the shape; the "İzlediklerim" filter surface is a later phase.
/// Crosses the wire in TaskWatcherRequest (nested in create/update).
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TaskWatcherRole
{
    Watcher = 0,
    Consultant = 1
}

/// <summary>
/// Allowlisted value types for configurable fields. Deliberately identical to the executable contract's
/// VALUE_TYPES so a field definition can never produce a businessContext value the browser must reject.
/// Crosses the wire BOTH ways in TaskFieldValueDto (create/update requests and the detail response), so without
/// the converter the browser would have to send and read opaque integers.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TaskFieldValueType
{
    Text = 0,
    Number = 1,
    Currency = 2,
    Percentage = 3,
    Date = 4,
    DateTime = 5,
    Boolean = 6,
    Status = 7,
    Person = 8,
    Reference = 9,
    Link = 10
}

/// <summary>businessContext importance: `primary` fields are capped by the contract (max 8 per item).</summary>
// Crosses the wire in the Phase 5 field-definition requests, so it serializes as a STRING. An enum
// reaching a client as a number is a defect this module has already shipped twice.
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TaskFieldImportance
{
    Secondary = 0,
    Primary = 1
}

/// <summary>Where a field's option list comes from. FG-004: a hard-coded list is never allowed.</summary>
// Crosses the wire in the Phase 5 field-definition requests, so it serializes as a STRING. An enum
// reaching a client as a number is a defect this module has already shipped twice.
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TaskFieldOptionsSourceKind
{
    None = 0,

    /// <summary>A platform list: language, currency, timezone. Short and fixed.</summary>
    PlatformLookup = 1,

    /// <summary>A governed reference set: country, legal form, and a tenant's own sets. Short and fixed.</summary>
    BusinessReferenceData = 2,

    /// <summary>
    /// ANOTHER MODULE'S RECORDS — departments, positions, and later products or suppliers. Not a fixed list:
    /// there can be thousands, so the value is SEARCHED rather than enumerated, and what is stored is the
    /// record's identity rather than its label.
    ///
    /// <para>The pattern is old and has three names already: SAP's check table with its F4 search help, Oracle's
    /// table-validated value set behind a descriptive flexfield, ServiceNow's reference field. All three say the
    /// same sentence — the administrator defines the FIELD, and another module owns the VALUES.</para>
    /// </summary>
    ModuleRecord = 3
}

/// <summary>
/// Field-level authorization metadata carried from day one so BL-024 becomes additive with NO migration
/// (pack §12 K1). Phase 1 stores it; no evaluation happens yet.
/// </summary>
// Crosses the wire in the Phase 5 field-definition requests, so it serializes as a STRING. An enum
// reaching a client as a number is a defect this module has already shipped twice.
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TaskFieldClassification
{
    Normal = 0,
    Internal = 1,
    Confidential = 2,
    Restricted = 3
}

// Crosses the wire in the Phase 5 field-definition requests, so it serializes as a STRING. An enum
// reaching a client as a number is a defect this module has already shipped twice.
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TaskFieldAccessState
{
    Visible = 0,
    Masked = 1,
    Hidden = 2
}

/// <summary>
/// Checklist item semantics. Crosses the wire in AddChecklistItemRequest (Phase 2), so it carries the string
/// converter — without it the browser's <c>"Blocking"</c> is a 400 before the handler runs.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ChecklistItemRequirement
{
    Optional = 0,

    /// <summary>Must be completed, but does not block task completion.</summary>
    Required = 1,

    /// <summary>Blocks `complete` while incomplete → disabledReasonCode CHECKLIST_INCOMPLETE.</summary>
    Blocking = 2
}

public enum ChecklistRunStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2
}

/// <summary>
/// Recurrence cadence. Crosses the wire in CreateTaskRecurrenceRuleRequest and UpdateTaskRecurrenceRuleRequest
/// as of Phase 4, so it serializes as a STRING — an enum that reaches a client as a number is a defect this
/// module has already shipped once, and TaskJsonContractTests caught this one before it left the branch.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TaskRecurrenceFrequency
{
    None = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Quarterly = 4,
    Yearly = 5
}

/// <summary>
/// What KIND OF RECORD work of this type produces (DCP-005 §6.3).
///
/// <para><b>The default is deliberately "not a record", not quarantine.</b> An earlier design classified
/// everything and quarantined what it could not resolve; that collapses here, because manually created tasks are
/// daily work — quarantine would become the main path instead of the exception.</para>
///
/// <para><b>String on the wire</b>, like every other enum this module ships to a client: a value that reaches
/// the browser as a number is a defect this module has already shipped once.</para>
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TaskRecordClass
{
    /// <summary>The default. Work that produces no controlled record at all.</summary>
    NOT_A_RECORD = 0,

    /// <summary>A record the business keeps, outside any GxP obligation.</summary>
    OPERATIONAL_RECORD = 1,

    /// <summary>
    /// A GxP quality record. The control statement DCP-005 §6.3 commits to depends on this value only ever
    /// arriving from a task TYPE — a manually created, unclassified task may not produce one.
    /// </summary>
    GXP_QUALITY_RECORD = 2
}

/// <summary>
/// Which quality domain governs work of this type (DCP-005 §6.3).
///
/// <para><b>ONE VALUE, NEVER A LIST</b> — and the counterparty's reasoning is the reason, not ours: the folder
/// path is computed from this field, and a type carrying several domains makes that rule unresolvable. A
/// deviation is therefore four types (DEV-QMS · DEV-GMP · DEV-GDP · DEV-PV), not one type with four domains.</para>
///
/// <para><b>Empty is not "many".</b> Work outside any domain leaves this null and takes
/// <see cref="TaskRecordClass.OPERATIONAL_RECORD"/>.</para>
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TaskGqmsDomain
{
    QMS = 0,
    GMP = 1,
    GDP = 2,
    PV = 3,
    RAF = 4,
    NUT = 5,
    CSV = 6,
    RND = 7
}

/// <summary>
/// The business FUNCTION a task type belongs to — DCP-005 §6.7, quoted from the counterparty's own template.
///
/// <para><b>A closed list, and it was NOT one until the values existed.</b> The first build of the task type
/// left this as normalised free text with a documented seam, because an earlier prompt claimed the list was in
/// the pack and it was not. Nineteen values were not invented to make the field look finished: a guessed list
/// rejects the counterparty's real codes and accepts made-up ones, and every task typed with a wrong code has
/// to be re-typed later.</para>
///
/// <para><b>Codes, not names.</b> The member IS the stored value; the human-readable name is a label keyed off
/// it, so the counterparty's spelling of "Regulatory Affairs (operational)" is a translation question rather
/// than a data question.</para>
///
/// <para>⚠ <c>RND</c> appears here AND in <see cref="TaskGqmsDomain"/>, deliberately: one is the function that
/// owns the work, the other is the quality domain that governs it, and a type can legitimately carry the same
/// three letters in both. They are separate axes, not a duplication.</para>
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TaskFunctionCode
{
    QUA = 0,
    RA = 1,
    PV = 2,
    MFG = 3,
    SCM = 4,
    RND = 5,
    COM = 6,
    FIN = 7,
    HR = 8,
    LEG = 9,
    PRC = 10,
    ITG = 11,
    ISM = 12,
    FAC = 13,
    EHS = 14,
    PPM = 15,
    CORP = 16,
    CTY = 17,
    MED = 18
}

/// <summary>
/// WHICH CLOSURE an outcome belongs to — finishing the work, or calling it off.
///
/// <para><b>ONE dictionary with a discriminator, not two lists.</b> The alternative was
/// <c>CompletedOutcomes</c> and <c>CancelledOutcomes</c> side by side, and it loses twice. The code-uniqueness
/// rule would have to be asked in two places — and "is DUPLICATE already taken?" answered differently by each
/// half is the two-places-drift this module has already paid for with the transition body vocabulary. More
/// plainly: <see cref="TaskItem.ClosureReasonCode"/> is ONE field. A task carries one closure code whichever way
/// it ended, so a vocabulary split in two would be describing a storage shape that does not exist.</para>
///
/// <para>The discriminator is also exactly what the picker filters on, so the split it replaces would have been
/// re-derived at every call site anyway.</para>
///
/// <para>String on the wire, like every other enum this module ships to a client.</para>
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TaskClosureDisposition
{
    /// <summary>Offered when the task is being COMPLETED (<see cref="TaskLifecycle.Done"/>).</summary>
    Completed = 0,

    /// <summary>Offered when the task is being CANCELLED (<see cref="TaskLifecycle.Cancelled"/>).</summary>
    Cancelled = 1
}
