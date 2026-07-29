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
public enum TaskFieldImportance
{
    Secondary = 0,
    Primary = 1
}

/// <summary>Where a field's option list comes from. FG-004: a hard-coded list is never allowed.</summary>
public enum TaskFieldOptionsSourceKind
{
    None = 0,
    PlatformLookup = 1,
    BusinessReferenceData = 2
}

/// <summary>
/// Field-level authorization metadata carried from day one so BL-024 becomes additive with NO migration
/// (pack §12 K1). Phase 1 stores it; no evaluation happens yet.
/// </summary>
public enum TaskFieldClassification
{
    Normal = 0,
    Internal = 1,
    Confidential = 2,
    Restricted = 3
}

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

/// <summary>Recurrence cadence (Phase 4 behaviour; schema only in Phase 1).</summary>
public enum TaskRecurrenceFrequency
{
    None = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3,
    Quarterly = 4,
    Yearly = 5
}
