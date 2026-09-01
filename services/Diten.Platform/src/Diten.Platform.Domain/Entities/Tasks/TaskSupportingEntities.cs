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

    /// <summary>
    /// WHICH FIELDS this save changed — empty for a pure lifecycle move, populated for an edit.
    ///
    /// <para><b>A LIST ON ONE ENTRY, not one entry per field.</b> Five fields changed in one save is one act, and
    /// that is how the person who did it remembers it: "Ali moved the due date and raised the priority", not five
    /// separate events an hour apart. Five rows would also bury the six entries that tell the task's story under
    /// sixty that do not — the exact objection <c>RecordIfMovedAsync</c> raises against field logging, answered
    /// rather than ignored.</para>
    ///
    /// <para>Embedded rather than a collection of its own for the same reason the log is one collection: the
    /// reader has ONE question, and it deserves one ordered answer.</para>
    /// </summary>
    public List<TaskFieldChange> FieldChanges { get; set; } = [];

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
/// <para><b>EDITABLE AND RETRACTABLE — WITH A TRAIL (2026-08-14, owner decision).</b> This type used to say:
/// "Immutable by design. There is no edit and no delete… If retraction is ever needed it arrives as a 'withdrawn'
/// MARK, never as a deletion." That reasoning was never wrong — changing a sentence somebody has already replied
/// to can make their reply nonsense, and in an ERP that is rewriting history.</para>
///
/// <para>What changed is that the compromise was found, and it is the one the old text itself gestured at: THE
/// TRAIL. What immutability protected was "nothing disappears or changes silently" — and an edit that says it was
/// edited, and a deletion that leaves a marker where the comment stood, do not break that. So:</para>
/// <list type="bullet">
///   <item><description><b>Edit</b> — the text changes and <see cref="EditedAt"/> is stamped. The screen says
///   "edited" beside it, so a reader can tell a sentence has moved since they last read it.</description></item>
///   <item><description><b>Delete</b> — a TOMBSTONE, never a removal. <see cref="DeletedAt"/> is stamped and
///   <see cref="Text"/> is CLEARED, so the words are genuinely gone while the fact that somebody spoke here, and
///   then withdrew it, remains in the feed. A row that vanished entirely would renumber a conversation other
///   people quoted.</description></item>
///   <item><description><b>Only the author.</b> Not a manager, not an administrator — nobody asked for that
///   exception in the decision that opened this, and an authority to edit other people's words is far easier to
///   grant than to take back.</description></item>
/// </list>
///
/// <para>The row is still never hard-deleted, and the author snapshot is still never re-resolved. Those two were
/// the load-bearing halves of the old decision and they are untouched.</para>
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

    /// <summary>
    /// When the author last rewrote this comment; null while it still says what it originally said.
    ///
    /// <para>The INSTANT, not a flag. "Edited" alone cannot answer "before or after I read it?", which is the
    /// only question the mark exists to settle — and it is the same absolute-instant rule the whole feed follows,
    /// so the words on screen are derived late in the reader's own language.</para>
    /// </summary>
    public DateTimeOffset? EditedAt { get; set; }

    /// <summary>
    /// When the author withdrew this comment. The row SURVIVES: <see cref="Text"/> is cleared and this is
    /// stamped, so the feed keeps a marker saying somebody spoke here and took it back.
    ///
    /// <para>Deliberately NOT the inherited soft-delete flag. `IsDeleted` takes the row out of every read through
    /// the repository's execution filter, which is exactly what a tombstone must not do — the marker has to keep
    /// arriving. Two different meanings, two different fields.</para>
    /// </summary>
    public DateTimeOffset? WithdrawnAt { get; set; }
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

    /// <summary>
    /// WHICH COMPANY this template belongs to. Null means it applies to every legal entity in the tenant.
    ///
    /// <para><b>ONE entity, not a list, and that is the whole decision.</b> A multi-select rots: the day a new
    /// company is opened, every template that should also cover it has to be found and edited one at a time, and
    /// nobody does that — so the list silently means "the companies we had when somebody last looked". A single
    /// nullable owner has no such drift: either the template is global or it names exactly one company, and a
    /// shape three companies share is three templates, each editable in its own company without touching the
    /// other two. That is the shape the larger systems settle on for the same reason.</para>
    ///
    /// <para>⚠ Stored and displayed; it is NOT a read filter yet. MOD-0024 carries no "current legal entity"
    /// context to filter against, and inventing one here would be a second answer to which company a user is
    /// acting for. Scoping the pickers is a follow-on that needs that context first — recording the intent now
    /// is what lets it arrive without a migration.</para>
    /// </summary>
    public Guid? LegalEntityId { get; set; }

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

/// <summary>
/// WC-1 — THE PERSONAL OVERLAY: what ONE reader has laid over somebody else's work. One document per
/// (tenant, task, user).
///
/// <para><b>Why it exists.</b> The projection's own note said the personal overlay was "owned by the frontend
/// WorkCenter layer, not this backend projection". Measured 2026-08-14, that frontend layer wrote to NOWHERE:
/// a note lived in a JavaScript object until the next reload and a snooze with it, while the screen said "Not
/// kaydedildi". The decision was half-made — one side declined to store it and the other never picked it up —
/// and the visible shape of that half was a save confirmation for a save that did not happen.</para>
///
/// <para><b>Why one document and not three.</b> A note and a snooze are the same KIND of thing: private to one
/// reader, invisible to everyone else, worthless to the task itself. Splitting them across collections would
/// mean solving authorization, deletion and tenant clean-up once per collection — and the third solution is
/// always the one that forgets a rule.</para>
///
/// <para><b>Why the PLAN DATE is not in here.</b> It looks personal and is not: <see cref="TaskItem.PlannedDate"/>
/// sits on the shared task row, moves the shared lifecycle to <c>Planned</c> and is read back by everyone who can
/// read the task. Moving it here would change WHAT IT MEANS, not merely where it lives — a re-plan would stop
/// being visible to the requester. It stays where it is, and its "Kişisel" label on screen is the thing that is
/// wrong, not its storage.</para>
///
/// <para><b>The notes are EMBEDDED</b> rather than a collection of their own, and this is the opposite call from
/// <see cref="TaskComment"/> on purpose. A comment is shared, immutable and unbounded, so an embedded array would
/// make every task read carry the whole conversation. A personal note list belongs to exactly one reader, is
/// never read by anyone else and is deleted by its author — the whole list is always fetched together and never
/// alone.</para>
/// </summary>
public sealed class TaskPersonalOverlay : TenantScopedEntity
{
    public required Guid TaskItemId { get; set; }

    /// <summary>WHOSE overlay. Every read filters on this — it is not a display rule.</summary>
    public required Guid UserId { get; set; }

    /// <summary>
    /// Hide this task from the reader's own inbox until this date. NEVER changes the task's lifecycle,
    /// normalized status or waiting context — the contract states that outright
    /// (<c>SNOOZE_MUST_NOT_CREATE_WAITING</c>), and the requester must not be able to tell that the holder
    /// snoozed anything.
    /// </summary>
    public DateTimeOffset? SnoozedUntil { get; set; }

    /// <summary>
    /// The reader marked this task to come back to. Personal, like the snooze beside it: the requester cannot
    /// tell, and it changes nothing about the task itself.
    ///
    /// <para>It lives HERE rather than on the task because pinning is an opinion, not a property — two people
    /// looking at the same task can disagree about it, and the overlay is the row that already models that.</para>
    /// </summary>
    public bool Pinned { get; set; }

    /// <summary>The reader's own notes, oldest first. Empty is the normal state.</summary>
    public List<TaskPersonalNote> Notes { get; set; } = [];

    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// One personal note. Embedded in <see cref="TaskPersonalOverlay"/>; see that type for why.
///
/// <para><b>No edit, by decision.</b> Add and delete only — a note is a sentence to oneself, and delete-then-write
/// is the same act with one less endpoint, one less concurrency question and one less audit story.</para>
/// </summary>
public sealed class TaskPersonalNote
{
    /// <summary>Stable id, so a delete names a note rather than an index into a list that may have moved.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The text the reader typed. Never a resource key — a person wrote it.</summary>
    public required string Text { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Who wrote it. Today this is always the overlay's own <see cref="TaskPersonalOverlay.UserId"/> — it is
    /// recorded anyway because a note that cannot say who wrote it cannot survive the overlay ever being shared
    /// (delegation, a handover, an export), and a note with no author is the one thing that cannot be repaired
    /// afterwards.
    /// </summary>
    public Guid AuthorUserId { get; set; }
}

/// <summary>
/// A TASK TYPE — the carrier for classification, quality domain, the quality-event flag and the governing
/// documents (DCP-005 slice 1).
///
/// <para><b>Why this exists at all.</b> Every comparable system has one — order type in SAP, task type in
/// Oracle, issue type elsewhere — because it is the thing that carries defaults, permissions and
/// classification. This product had none: two rounds of design with QA failed because both assumed a
/// classification master that does not exist here, and the third put it on a type this system would build.</para>
///
/// <para><b>Only the container is ours.</b> The 31 seed types arrive with <c>record_class</c>,
/// <c>gqms_domain</c>, <c>is_quality_event</c> and <c>governing_documents</c> already decided by QA.</para>
/// </summary>
public sealed class TaskType : TenantScopedEntity
{
    /// <summary>
    /// Tenant-unique and IMMUTABLE. Changing a code after tasks have been opened with it rewrites the identity
    /// of those tasks — the same reason a folder code cannot be re-pointed. Read-only once created.
    /// </summary>
    public required string Code { get; set; }

    /// <summary>What a person sees when choosing a type. The administrator's own words.</summary>
    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>What kind of record work of this type produces. See <see cref="TaskRecordClass"/>.</summary>
    public TaskRecordClass RecordClass { get; set; } = TaskRecordClass.NOT_A_RECORD;

    /// <summary>
    /// The single governing quality domain, or null for work outside any of them. NEVER a collection — see
    /// <see cref="TaskGqmsDomain"/> for the counterparty's reasoning.
    /// </summary>
    public TaskGqmsDomain? GqmsDomain { get; set; }

    /// <summary>
    /// The business function this type belongs to, or null. One of the nineteen codes in DCP-005 §6.7.
    ///
    /// <para>⚠ <b>STORED AS A STRING, VALIDATED AS A CLOSED LIST — and the two halves are deliberate.</b>
    /// It was briefly an enum, and that broke the screen: a row written before the list existed carried
    /// <c>"QA"</c> (the pack's code is <c>QUA</c>), and the Mongo driver threw
    /// <c>FormatException: Requested value 'QA' was not found</c> on DESERIALISATION — so ONE stale value took
    /// the entire task-type list down with a 500, measured live 2026-08-26.</para>
    ///
    /// <para>A document store has no schema migration to lean on: whatever was written stays written. A type
    /// that cannot REPRESENT what is already stored converts a data problem into an outage, and the two honest
    /// alternatives — dropping the value on read, or refusing to load the row — are both silent data loss.
    /// So the boundary that closes the list is the WRITE (<c>TaskTypeRules.ParseFunctionCode</c>): nothing new
    /// enters outside the nineteen, and what is already there stays readable and visibly non-conforming.</para>
    /// </summary>
    public string? FunctionCode { get; set; }

    /// <summary>Whether work of this type is itself a quality event (deviation, complaint, recall …).</summary>
    public bool IsQualityEvent { get; set; }

    /// <summary>
    /// Controlled-document UIDs that govern this type in EVERY organisation (DCP-005 §6.4, the group layer).
    ///
    /// <para>UIDs, not records: the ERP holds a lookup of controlled documents and never a table of them, so
    /// there is nothing here to correct and a refresh simply overwrites the list.</para>
    /// </summary>
    public List<string> GroupDocuments { get; set; } = [];

    /// <summary>
    /// Extra governing documents for ONE organisation (DCP-005 §6.4, the local layer). Sparse on purpose: 24
    /// types × 5 orgs would be 120 cells, and the counterparty's own registers are split exactly this way.
    /// </summary>
    public Dictionary<string, List<string>> LocalDocuments { get; set; } = [];

    /// <summary>
    /// Whether this type may be chosen on a NEW task. Retiring one never removes it: tasks already opened with
    /// it keep reading correctly, which is the same rule folders and documents follow.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// One IMPORT of the controlled-document reference list (DCP-005 slice 2) — the version, not the documents.
///
/// <para><b>Why a version at all.</b> The list is a snapshot, and snapshots age. When it is refreshed a title
/// can change or a code can be reallocated; a task that resolved a reference against an earlier list has to be
/// able to say WHICH list it saw, or a closed record's basis becomes unknowable. Same reason the folder
/// taxonomy imports as a <c>BaselineRelease</c> rather than as loose rows.</para>
///
/// <para><b>The pattern is the taxonomy's, not a second one:</b> a semantic version supplied by the importer, a
/// deterministic content hash, and a source key — see <see cref="DocumentManagement.BaselineRelease"/>.</para>
/// </summary>
public sealed class DocumentReferenceListVersion : TenantScopedEntity
{
    /// <summary>Stable source key for the register this list came from.</summary>
    public required string SourceKey { get; set; }

    /// <summary>The importer's semantic version — never the inherited technical one.</summary>
    public required string ListVersion { get; set; }

    /// <summary>
    /// SHA-256 over the imported content. Two imports of the same bytes produce the same hash, which is what
    /// lets a re-upload be RECOGNISED rather than silently duplicated.
    /// </summary>
    public required string ContentHash { get; set; }

    public required string FileName { get; set; }

    /// <summary>How many entries this version carries — the number the import result reports back.</summary>
    public int EntryCount { get; set; }

    /// <summary>How many of them a task may actually cite. The rest are visible and blocked.</summary>
    public int LinkableCount { get; set; }

    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;


    /// <summary>
    /// When this version was withdrawn, or null.
    ///
    /// <para><b>⚠ NOT <see cref="DeletedAt"/>, and the difference is the whole point.</b> Soft delete takes the
    /// row out of every read through the repository's execution filter — which is exactly what a withdrawal
    /// must NOT do: a closed task may have resolved its references against this version, and the answer to
    /// "which list did it see" has to keep arriving. Same reasoning and same shape as
    /// <c>TaskComment.WithdrawnAt</c>, the one reversal pattern this service already has.</para>
    ///
    /// <para><b>Why it exists:</b> measured live — importing a wrong file AFTER the right one stranded the real
    /// register as an older version, because identical bytes are refused and the newest wins. Two correct rules
    /// made an irreversible trap between them. Withdrawal is the way out that keeps both.</para>
    /// </summary>
    public DateTimeOffset? WithdrawnAt { get; set; }

    /// <summary>
    /// WHY it was withdrawn. Required — a version taken out of service without a reason leaves the same
    /// unanswerable "why" the import already refuses for a blocked row with no explanation.
    /// </summary>
    public string? WithdrawnReason { get; set; }

    public string? WithdrawnBy { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// One controlled document, as the ERP KNOWS OF it (DCP-005 slice 2).
///
/// <para><b>⚠ THIS IS A LOOKUP ROW, NOT A DOCUMENT RECORD, and the difference is the whole design.</b> The
/// pack (§6.1) settles it: if the ERP holds a documents TABLE, somebody eventually corrects a title or a
/// version in it, and at that moment a second authority over the document exists. A lookup has nothing to
/// correct — a refresh overwrites it. That converts a discipline problem into an architectural one.</para>
///
/// <para><b>⚠ NOT <c>ControlledDocument</c>.</b> That entity requires a <c>CollectionInstanceId</c>, which
/// would force every referenced document into a provisioned folder. There are no folders on the reference
/// side — QA confirmed — so a row here has no folder and needs none.</para>
///
/// <para><b>⚠ NOTHING EDITS THIS.</b> There is no update command and no edit screen, deliberately. The write
/// path is the import; the read path is the search. A row that could be edited here would be the second
/// authority the pack exists to prevent.</para>
/// </summary>
public sealed class DocumentReferenceEntry : TenantScopedEntity
{
    /// <summary>Which imported version this row belongs to. Rows never move between versions.</summary>
    public required Guid ListVersionId { get; set; }

    /// <summary>The register's own identifier — the join key a task will freeze in slice 3.</summary>
    public required string DocumentUid { get; set; }

    /// <summary>The human-readable code (e.g. <c>GMG-QMS-SOP-0005</c>).</summary>
    public required string DocumentCode { get; set; }

    public required string Title { get; set; }

    public string? GqmsDomain { get; set; }
    public string? GqmsType { get; set; }
    public string? ErpDocumentType { get; set; }
    public string? DocumentVersion { get; set; }

    /// <summary>The register's own status word, passed through unchanged — including "NOT REGISTERED".</summary>
    public string? Status { get; set; }

    public string? Criticality { get; set; }
    public string? Owner { get; set; }
    public string? EffectiveDate { get; set; }
    public string? ReviewCycle { get; set; }

    /// <summary>Register folder id/path. Carried for traceability; the ERP instantiates no folder from it.</summary>
    public string? FolderId { get; set; }
    public string? FolderPath { get; set; }

    public bool IsMandatoryGroupSop { get; set; }

    /// <summary>
    /// Whether a task may cite this document.
    ///
    /// <para>⚠ A blocked row is IMPORTED AND SHOWN, never dropped. 36 of the 358 cannot be linked — 23 planned,
    /// 7 void, 6 declared mandatory with a UID but absent from the master register (QA's own open finding).
    /// Hiding them would leave the reader asking "where is that SOP" with nowhere to look; showing them with a
    /// reason answers it. This is the OPPOSITE of the zero-count chip decision, and deliberately so: there the
    /// population did not exist, here the document exists and cannot be cited.</para>
    /// </summary>
    public bool LinkableInErp { get; set; }

    /// <summary>Why it cannot be cited — the register's own words.</summary>
    public string? LinkBlockedReason { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
