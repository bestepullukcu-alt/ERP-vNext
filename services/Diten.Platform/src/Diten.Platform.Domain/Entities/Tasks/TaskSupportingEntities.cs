using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Domain.Entities.Tasks;

// MOD-0024 — supporting aggregates around TaskItem. Every one is tenant-scoped and soft-deletable.
// Phase 1 creates the SCHEMA for all of them (so Phases 2–5 are additive); only TaskItem, TaskAssignment,
// TaskDependency, TaskWatcher and TaskFieldDefinition are exercised by Phase-1 behaviour.

/// <summary>
/// Append-only ownership/assignment history: who held the task, when, and how it changed hands. Kept separate
/// from <see cref="TaskItem"/> so the current holder stays a single mutable field while history is immutable.
/// </summary>
public sealed class TaskAssignment : TenantScopedEntity
{
    public required Guid TaskItemId { get; set; }
    public required TaskAssignmentEventType EventType { get; set; }

    /// <summary>Target user of the event (null for a pool offer, which targets a position).</summary>
    public Guid? UserId { get; set; }

    /// <summary>Target position of the event (pool offer / release back to pool).</summary>
    public Guid? PositionId { get; set; }

    /// <summary>Who performed it.</summary>
    public Guid? ActorUserId { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ReasonCode { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// WC-1 — the LIFECYCLE EVENT LOG: one immutable record per act that moved a task.
///
/// <para><b>Why it had to exist.</b> The work-item projection published only <c>kind:"comment"</c>, and its own
/// comment said why: with no event log, a timeline derived from the four timestamps a task carries
/// (created/started/completed/cancelled) would silently omit accept, plan, claim, release and inquire — a partial
/// history read as a complete one. The answer was never to derive better; it was to record.</para>
///
/// <para><b>Its own collection, not an array on <see cref="TaskItem"/>.</b> Three reasons, all of which this
/// module has already paid for once: <c>UpdateTaskItemRequest</c> is a FULL REPLACE, so an embedded array is one
/// forgetful writer away from deletion (the reason <see cref="TaskComment"/> is separate); an embedded list would
/// make every task read carry its whole history, and the list projection reads a page of tasks at a time; and
/// BL-030 — a <c>DateTimeOffset</c> inside a document array is stored as <c>[ticks, offsetMinutes]</c>, so an
/// embedded log could not be ordered at all.</para>
///
/// <para><b>Immutable, like a comment.</b> No edit, no delete, no endpoint for either. What happened is not
/// something a later writer gets to revise.</para>
///
/// <para><b>No <c>OccurredAt</c> of its own</b> — the base entity's <c>CreatedAt</c> IS the moment, and reusing it
/// is load-bearing rather than frugal: this log is merged with <see cref="TaskComment"/> into ONE time-ordered
/// feed, and two records sorted on two different clocks interleave wrongly at the seams. <see cref="TaskAssignment"/>
/// keeps its own <c>OccurredAt</c> and is not merged into anything.</para>
///
/// <para><b>The actor is an ID, not a snapshotted name</b> — the opposite of <see cref="TaskComment"/>, and
/// deliberately. A comment is a QUOTATION: who said it then must not change when a person is renamed. An event is
/// a fact about an identity, so it names the person as they are called today, resolved on read through the same
/// batched directory lookup the projection already runs for assignees.</para>
/// </summary>
public sealed class TaskTransition : TenantScopedEntity
{
    public required Guid TaskItemId { get; set; }
    public required TaskTransitionKind Kind { get; set; }

    /// <summary>The lifecycle BEFORE the act. Equal to <see cref="ToLifecycle"/> for an act that changed only
    /// ownership (claim, release, accept-while-planned) — those are transitions too, and dropping them would put
    /// exactly the holes back that this log exists to close.</summary>
    public required TaskLifecycle FromLifecycle { get; set; }

    /// <summary>The lifecycle AFTER the act.</summary>
    public required TaskLifecycle ToLifecycle { get; set; }

    /// <summary>Who performed it. Null only when no user context stood behind the write (a background sweep).</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>Machine-readable classification when the act carried one (closure reason, handover reason).</summary>
    public string? ReasonCode { get; set; }

    /// <summary>The reason in the actor's OWN WORDS, when the act required one (wait, return, reassign).
    /// Text, never a resource key.</summary>
    public string? Reason { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// A typed dependency between TWO MOD-0024 tasks. MOD-0024 may manage these because it is their source
/// (pack §12 Y3); the Task Center still renders dependencies read-only and hosts no editor.
/// </summary>
public sealed class TaskDependency : TenantScopedEntity
{
    /// <summary>The dependent task (the one that is blocked).</summary>
    public required Guid TaskItemId { get; set; }

    /// <summary>The predecessor task.</summary>
    public required Guid DependsOnTaskItemId { get; set; }

    public TaskDependencyType DependencyType { get; set; } = TaskDependencyType.FinishToStart;
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// A comment on a task (BL-034 item 7). Its OWN collection, not an array on <see cref="TaskItem"/>: an embedded
/// list would make every task read carry the whole conversation, and <c>UpdateTaskItemRequest</c> is a FULL
/// REPLACE — a writer that forgot to round-trip the array would delete the history. That has happened here before.
///
/// <para><b>Immutable by design.</b> There is no edit and no delete, and no endpoint for either. ServiceNow work
/// notes and SAP workflow notes behave the same way: once somebody has acted on what a comment said, removing it
/// rewrites the past. If retraction is ever needed it arrives as a "withdrawn" MARK, never as a deletion.</para>
///
/// <para>The author's display name is COPIED at write time rather than resolved on read. A comment is a record of
/// what was said and by whom at that moment; re-resolving would silently rename the speaker when a person is
/// renamed, and would also make reading a task depend on AuthService being up.</para>
/// </summary>
public sealed class TaskComment : TenantScopedEntity
{
    public required Guid TaskItemId { get; set; }

    /// <summary>The text the user typed. Never a resource key — a person wrote it.</summary>
    public required string Text { get; set; }

    public Guid? AuthorUserId { get; set; }

    /// <summary>Snapshot of the author's name, or null when it could not be resolved (never a GUID).</summary>
    public string? AuthorDisplayName { get; set; }
}

/// <summary>
/// Watcher/consultant participation. Grants VISIBILITY only — never action rights (pack §12 K3, OD-4:
/// summary + read-only). Phase 1 persists it; the "İzlediklerim" filter is a later phase.
/// </summary>
public sealed class TaskWatcher : TenantScopedEntity
{
    public required Guid TaskItemId { get; set; }
    public required Guid UserId { get; set; }
    public TaskWatcherRole Role { get; set; } = TaskWatcherRole.Watcher;

    /// <summary>Position the consultant was picked through, when applicable (display context only).</summary>
    public Guid? PositionId { get; set; }

    public Guid? AddedByUserId { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// A configurable field's DEFINITION (pack §12 K1). This is what makes the engine generic: Phase, Work Type,
/// Market/Country, Domain and External Party are rows here, never columns on <see cref="TaskItem"/>.
/// Option lists always come from an existing source (FG-004 forbids hard-coded lists).
/// </summary>
public sealed class TaskFieldDefinition : TenantScopedEntity
{
    /// <summary>Tenant-unique, lowercase-dotted (e.g. <c>regulatory.phase</c>).</summary>
    public required string Code { get; set; }

    /*
     * TWO label sources, exactly one set — the same split ChecklistTemplateItem already makes, and for the same
     * reason its comment gives: conflating them is how a raw resource key reaches the screen.
     *
     * The reason it had to arrive here: a tenant administrator cannot add a line to OUR resx files. With only a
     * resource key on the entity, every field a tenant defined would have rendered as the literal key —
     * "regulatory.phase" where a label belongs. A tenant's own words are not translatable content we own; they
     * are content, and the contract already carries that distinction (WorkItemLabelDto resource vs display).
     */

    /// <summary>
    /// SYSTEM definitions. Translated in all seven languages, identical in every tenant. Null when
    /// <see cref="LabelText"/> is used.
    /// </summary>
    public string? LabelResourceKey { get; set; }

    /// <summary>
    /// TENANT definitions — the administrator's own words, in the language they typed them in. Null when
    /// <see cref="LabelResourceKey"/> is used.
    ///
    /// <para>Single-language on purpose. A tenant field that translates into seven languages is a separate piece
    /// of work with its own editor and its own storage; inventing half of it here — a key that only one tenant
    /// has strings for, say — would put us straight back to raw keys on screen.</para>
    /// </summary>
    public string? LabelText { get; set; }

    public required TaskFieldValueType ValueType { get; set; }

    /// <summary>businessContext section this field renders in (contract caps sections at 6).</summary>
    public required string Section { get; set; }

    public TaskFieldImportance Importance { get; set; } = TaskFieldImportance.Secondary;
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }

    public TaskFieldOptionsSourceKind OptionsSourceKind { get; set; } = TaskFieldOptionsSourceKind.None;

    /// <summary>Lookup key / BRD set code when the field is option-based.</summary>
    public string? OptionsSourceKey { get; set; }

    /// <summary>Restricts the definition to one consuming module; null = available to all.</summary>
    public string? AppliesToModuleCode { get; set; }

    // BL-024-ready (stored, not yet evaluated).
    public TaskFieldClassification Classification { get; set; } = TaskFieldClassification.Normal;
    public TaskFieldAccessState DefaultAccessState { get; set; } = TaskFieldAccessState.Visible;

    /*
     * ── BL-024 Phase 2 — WHO MAY SEE, AND WHO MAY WRITE ──────────────────────────────────────────────────
     *
     * A PERMISSION KEY, not a role and not a user list. Roles, grants and the catalogue belong to MOD-0018;
     * naming a role here would be a second place that decides who is who, and this codebase has already paid
     * for that twice (the seat directory, the active-window rule). The definition names a REQUIREMENT and
     * MOD-0018 answers who meets it.
     *
     * NULL means unrestricted, which is what every definition written before this field existed carries — so
     * turning the feature on changes nothing until somebody deliberately restricts something. The opposite
     * default would have hidden every existing field on deploy.
     */

    /// <summary>
    /// Permission required to SEE this field's value. Null: anyone who can read the task can read the field.
    /// </summary>
    public string? ViewPermission { get; set; }

    /// <summary>
    /// Permission required to WRITE this field. Null: anyone who can edit the task can write it.
    ///
    /// <para><b>Read access is a FLOOR for write access, but not a substitute for it.</b> They are separate
    /// questions with separate keys — an approver who may read a salary band is not thereby allowed to change
    /// it — and each is tested on its own. What the floor rules out is the incoherent case: writing a value you
    /// are not allowed to see is a covert channel, and it also makes the full-replace edit hazard unanswerable
    /// (the client never received the value, so it cannot send it back).</para>
    /// </summary>
    public string? EditPermission { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>Reusable checklist definition (Phase 2 behaviour; schema laid down in Phase 1).</summary>
public sealed class ChecklistTemplate : TenantScopedEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public List<ChecklistTemplateItem> Items { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// A checklist item's text comes from exactly ONE of two sources, and conflating them is how a raw resource key
/// reaches the screen (it already happened once with task titles):
/// <list type="bullet">
/// <item><see cref="LabelResourceKey"/> — a SYSTEM or tenant template item. The text is ours, it is the same for
/// every tenant, and it must localize into all seven languages.</item>
/// <item><see cref="LabelText"/> — an AD-HOC item a user typed on this task. It is the user's own words in the
/// language they chose; there is no resource key and inventing one would render the key itself.</item>
/// </list>
/// Exactly one is set. The projection emits the matching contract label form (resource vs display).
/// </summary>
public sealed class ChecklistTemplateItem
{
    public required string Code { get; set; }

    /// <summary>Set for system/tenant template text that must localize. Null when <see cref="LabelText"/> is used.</summary>
    public string? LabelResourceKey { get; set; }

    /// <summary>Set for text an author typed. Null when <see cref="LabelResourceKey"/> is used.</summary>
    public string? LabelText { get; set; }
    public ChecklistItemRequirement Requirement { get; set; } = ChecklistItemRequirement.Optional;
    public int SortOrder { get; set; }

    /// <summary>Requires supporting evidence before it can be ticked (evidence itself is MOD-0031's).</summary>
    public bool EvidenceRequired { get; set; }
}

/// <summary>A checklist template instantiated on a task (Phase 2 behaviour; schema now).</summary>
public sealed class ChecklistRun : TenantScopedEntity
{
    public required Guid TaskItemId { get; set; }
    public Guid? ChecklistTemplateId { get; set; }
    public ChecklistRunStatus Status { get; set; } = ChecklistRunStatus.NotStarted;
    public List<ChecklistRunItem> Items { get; set; } = [];
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>An item on a task's live checklist. Label rules mirror <see cref="ChecklistTemplateItem"/>.</summary>
public sealed class ChecklistRunItem
{
    public required string Code { get; set; }

    /// <summary>Localizable text from a template. Null for an ad-hoc item.</summary>
    public string? LabelResourceKey { get; set; }

    /// <summary>Text the user typed on this task. Null for a template item.</summary>
    public string? LabelText { get; set; }
    public ChecklistItemRequirement Requirement { get; set; } = ChecklistItemRequirement.Optional;
    public int SortOrder { get; set; }
    public bool EvidenceRequired { get; set; }
    public bool Completed { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /*
     * WHO PUT THIS STEP ON THE LIST — and therefore who may take it off.
     *
     * Without this field the rule cannot be written at all, which is exactly how the hole opened: a delete
     * endpoint shipped that checked the task, the run, the item, the lifecycle and the version, and never asked
     * whose step it was. A BLOCKING item could then be removed by the very person it was blocking, and one
     * level down — Blocking → Optional — is the same escape through a different door. A gate anyone can lift is
     * decoration.
     *
     * OWNERSHIP, not a severity threshold. A threshold ("Blocking is protected, Expected is not") only moves the
     * argument to where the line sits, and leaves the escape open on either side of it. This is also how the
     * larger systems draw it: a step defined by the process owner is not the handler's to withdraw.
     *
     * NULL means one of two things, and both are answered the same way: an item written before this field
     * existed, or an item instantiated from a TEMPLATE (which has no author — the template is the author). Both
     * are treated as SOMEBODY ELSE'S. Wrongly refusing an edit costs a conversation; wrongly allowing a deletion
     * costs the gate, silently, and nobody finds out until the thing the gate existed to prevent has happened.
     */
    public Guid? AddedByUserId { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Reusable task shape, optionally carrying a checklist (Phase 2 — pack §12 E5).</summary>
public sealed class TaskTemplate : TenantScopedEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? TitleTemplate { get; set; }
    public string? DescriptionTemplate { get; set; }
    public TaskPriority DefaultPriority { get; set; } = TaskPriority.Medium;
    public TaskAssignmentTarget DefaultAssignmentTarget { get; set; } = TaskAssignmentTarget.SelfAssigned;
    public Guid? DefaultPoolPositionId { get; set; }
    public int? DefaultDueInDays { get; set; }
    public Guid? ChecklistTemplateId { get; set; }
    public List<TaskFieldValue> DefaultFieldValues { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// Recurrence definition (Phase 4). Execution reuses the existing Hangfire seam
/// (<c>IRecurringJobRegistrar</c> + <c>IBackgroundJobHandler&lt;T&gt;</c>) — no new engine (pack §12 K8).
/// </summary>
public sealed class TaskRecurrenceRule : TenantScopedEntity
{
    public required string Name { get; set; }
    public TaskRecurrenceFrequency Frequency { get; set; } = TaskRecurrenceFrequency.None;
    public int Interval { get; set; } = 1;
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public Guid? TaskTemplateId { get; set; }

    /*
     * WHO the generated work belongs to.
     *
     * These were missing, and their absence was not cosmetic: with no assignment on the rule the generator fell
     * back to SelfAssigned, and a background sweep has no "self" — the current-user context answers Guid.Empty
     * with no HTTP request behind it. Every task a template-less rule produced was therefore assigned to nobody,
     * appeared in no list, and still consumed its period. Invisible work that can never be regenerated.
     *
     * SelfAssigned is deliberately NOT a legal value here — see TaskAssignmentIntentRules.
     */
    public TaskAssignmentTarget AssignmentTarget { get; set; } = TaskAssignmentTarget.Person;

    /// <summary>The person each generated task goes to. Required when the target is a person.</summary>
    public Guid? AssigneeUserId { get; set; }

    /// <summary>The queue each generated task waits in. Required when the target is a pool.</summary>
    public Guid? PoolPositionId { get; set; }

    /// <summary>
    /// Optional. Task creation resolves a unit on its own (from the pool's position, the assignee's position, or
    /// the tenant root), so this is an override rather than a requirement — the same graded fallback a manual
    /// create gets, not a second rule.
    /// </summary>
    public Guid? OrganizationUnitId { get; set; }

    /// <summary>Last instance stamp, used to avoid duplicate generation on a rerun.</summary>
    public string? LastProcessInstanceId { get; set; }

    public DateTimeOffset? LastGeneratedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeletedAt { get; set; }
}
