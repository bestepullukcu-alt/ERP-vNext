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

    /// <summary>Resource key — the label is localized, never stored as display text.</summary>
    public required string LabelResourceKey { get; set; }

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

    /// <summary>Last instance stamp, used to avoid duplicate generation on a rerun.</summary>
    public string? LastProcessInstanceId { get; set; }

    public DateTimeOffset? LastGeneratedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeletedAt { get; set; }
}
