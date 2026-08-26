using Diten.Platform.Domain.Entities.Tasks;

namespace Diten.Platform.Domain.Repositories;

// MOD-0024 — repository seams for the task engine. All reads go through the live TenantRepository<T> execution
// filter (tenant + IsDeleted), so a cross-tenant read returns null/empty with no metadata leak. Mutation uses
// expected-version optimistic concurrency: that is what makes the pool CLAIM race resolve to a single owner.

public interface ITaskItemRepository
{
    Task<TaskItem> CreateAsync(TaskItem task, CancellationToken ct = default);
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetAllForTenantAsync(CancellationToken ct = default);

    /// <summary>Tasks the user holds (assignee), regardless of lifecycle.</summary>
    Task<IReadOnlyList<TaskItem>> ListByAssigneeAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Subtasks of a parent task (Phase 2 — pack §12 E2). One level only, so this never recurses.</summary>
    Task<IReadOnlyList<TaskItem>> ListByParentAsync(Guid parentTaskItemId, CancellationToken ct = default);

    /// <summary>
    /// Subtasks of MANY parents in one read. The projection renders a page of tasks, and fetching children per
    /// task would be an N+1 against Mongo.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> ListByParentsAsync(
        IReadOnlyCollection<Guid> parentTaskItemIds,
        CancellationToken ct = default);

    /// <summary>
    /// Tasks by id, in one read. Dependency edges point at tasks that need not be on the current page, so the
    /// projection has to fetch the OTHER end of every edge without an N+1.
    /// </summary>
    Task<IReadOnlyList<TaskItem>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>Unclaimed pool tasks offered to any of the supplied positions.</summary>
    Task<IReadOnlyList<TaskItem>> ListUnclaimedByPositionsAsync(
        IReadOnlyCollection<Guid> positionIds,
        CancellationToken ct = default);

    /// <summary>
    /// Optimistic-concurrency update. Returns false when the expected version no longer matches — the caller
    /// turns that into a controlled 409 rather than silently overwriting (pack §13).
    /// </summary>
    Task<bool> UpdateAsync(TaskItem task, int expectedVersion, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// WC-1 — the append-only lifecycle event log. Written by <see cref="ITaskItemRepository"/> as it commits, never
/// by a handler: see <see cref="Domain.Enums.Tasks.TaskTransitionKind"/> for why the decision that a transition
/// happened belongs to the write and not to the writer.
///
/// <para>There is no update and no delete, and that is the whole interface — an event log with a mutator is a
/// story someone can revise.</para>
/// </summary>
public interface ITaskTransitionRepository
{
    Task<TaskTransition> CreateAsync(TaskTransition transition, CancellationToken ct = default);

    /// <summary>One task's history, NEWEST FIRST and stably ordered — the same order and the same tie-break
    /// <see cref="ITaskCommentRepository"/> uses, because the two are merged into one feed.</summary>
    Task<IReadOnlyList<TaskTransition>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default);

    /// <summary>A whole page of tasks' history in ONE read — the projection renders many tasks at once.</summary>
    Task<IReadOnlyList<TaskTransition>> ListByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken ct = default);
}

public interface ITaskAssignmentRepository
{
    Task<TaskAssignment> CreateAsync(TaskAssignment assignment, CancellationToken ct = default);
    Task<IReadOnlyList<TaskAssignment>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default);
}

public interface ITaskDependencyRepository
{
    Task<TaskDependency> CreateAsync(TaskDependency dependency, CancellationToken ct = default);
    Task<IReadOnlyList<TaskDependency>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default);

    /// <summary>
    /// Every edge touching any of these tasks, in EITHER direction, in one read. Both directions because a task's
    /// detail names what it waits on AND what waits on it, and one read because the list projection needs the
    /// edges for a whole page at once.
    /// </summary>
    Task<IReadOnlyList<TaskDependency>> ListByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken ct = default);

    /// <summary>Satisfied by the base repository — declared so the handler can read an edge before removing it.</summary>
    Task<TaskDependency?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ITaskCommentRepository
{
    Task<TaskComment> CreateAsync(TaskComment comment, CancellationToken ct = default);

    /// <summary>
    /// A task's comments, NEWEST FIRST and stably ordered (ties broken by id), because the composer sits at the
    /// top of the feed and a wobbling order on equal timestamps reads as the list rearranging itself.
    /// </summary>
    Task<IReadOnlyList<TaskComment>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default);

    /// <summary>Comments for a whole page of tasks in ONE read — the projection renders many tasks at once.</summary>
    Task<IReadOnlyList<TaskComment>> ListByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken ct = default);

    /// <summary>Satisfied by the base repository — declared so a handler can read a comment before rewriting it.</summary>
    Task<TaskComment?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Rewrite one comment in place — an edit, or the withdrawal that clears its text and stamps a tombstone.
    ///
    /// <para>There is still NO delete on this interface, and that is the half of the old immutability decision
    /// that survives intact: a withdrawn comment keeps its row so the feed keeps a marker where somebody spoke.
    /// A hard delete would renumber a conversation other people quoted.</para>
    ///
    /// <para>No expected version: a comment has exactly one writer — its author — so there is no race to lose,
    /// and refusing an author's own correction on a stale token would invent a conflict nobody could cause.</para>
    /// </summary>
    Task UpdateAsync(TaskComment comment, CancellationToken ct = default);
}

public interface ITaskWatcherRepository
{
    Task<TaskWatcher> CreateAsync(TaskWatcher watcher, CancellationToken ct = default);
    Task<IReadOnlyList<TaskWatcher>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskWatcher>> ListByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Watchers for a whole page of tasks in ONE read — the projection renders many tasks at once, and a
    /// per-task read here would be an N+1 across the surface.
    /// </summary>
    Task<IReadOnlyList<TaskWatcher>> ListByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// WC-1 — the personal overlay (note list + snooze) for ONE reader over ONE task.
///
/// <para>Every method takes the user id explicitly and every implementation ANDs it into the filter. That is the
/// authorization: "only the author sees their notes" is a READ rule enforced here, not a rendering rule the
/// client is trusted to apply. A repository that returned another user's overlay would make the projection leak
/// it regardless of what the screen chose to draw.</para>
/// </summary>
public interface ITaskPersonalOverlayRepository
{
    Task<TaskPersonalOverlay?> GetAsync(Guid taskItemId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// One reader's overlays for a whole page of tasks, in ONE read — the same N+1 rule every other container in
    /// the projection follows.
    /// </summary>
    Task<IReadOnlyList<TaskPersonalOverlay>> ListForUserAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Create-or-replace for (task, user). Not an expected-version write, and deliberately so: this document has
    /// exactly one writer, so there is no race to lose — and making a private note refuse on a stale token would
    /// invent a conflict nobody else could have caused.
    /// </summary>
    Task UpsertAsync(TaskPersonalOverlay overlay, CancellationToken ct = default);
}

public interface ITaskFieldDefinitionRepository
{
    Task<TaskFieldDefinition> CreateAsync(TaskFieldDefinition definition, CancellationToken ct = default);
    Task<TaskFieldDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TaskFieldDefinition?> GetByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Definitions the value validator offers — active and not retired.</summary>
    Task<IReadOnlyList<TaskFieldDefinition>> ListActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Every definition, retired ones included. The management surface needs them: a definition that vanished
    /// when it was switched off could not be switched back on, and the section cap has to be counted against
    /// what actually exists.
    /// </summary>
    Task<IReadOnlyList<TaskFieldDefinition>> ListAllAsync(CancellationToken ct = default);

    Task<bool> UpdateAsync(TaskFieldDefinition definition, int expectedVersion, CancellationToken ct = default);
}

// ── Phase 2+ seams. Declared now so the schema and repository surface are stable; no Phase-1 caller. ──

public interface IChecklistTemplateRepository
{
    Task<ChecklistTemplate> CreateAsync(ChecklistTemplate template, CancellationToken ct = default);
    Task<ChecklistTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ChecklistTemplate>> ListActiveAsync(CancellationToken ct = default);
}

public interface IChecklistRunRepository
{
    Task<ChecklistRun> CreateAsync(ChecklistRun run, CancellationToken ct = default);
    Task<ChecklistRun?> GetByTaskIdAsync(Guid taskItemId, CancellationToken ct = default);

    /// <summary>Runs for MANY tasks in one read — same N+1 reason as ListByParentsAsync.</summary>
    Task<IReadOnlyList<ChecklistRun>> ListByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken ct = default);
    Task<bool> UpdateAsync(ChecklistRun run, int expectedVersion, CancellationToken ct = default);
}

public interface ITaskTemplateRepository
{
    Task<TaskTemplate> CreateAsync(TaskTemplate template, CancellationToken ct = default);
    Task<TaskTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TaskTemplate>> ListActiveAsync(CancellationToken ct = default);
}

public interface ITaskRecurrenceRuleRepository
{
    Task<TaskRecurrenceRule> CreateAsync(TaskRecurrenceRule rule, CancellationToken ct = default);
    Task<TaskRecurrenceRule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TaskRecurrenceRule>> ListActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Every rule the tenant can see, active or not. The management surface has to show a paused rule — a rule
    /// that vanishes when it is switched off cannot be switched back on.
    /// </summary>
    Task<IReadOnlyList<TaskRecurrenceRule>> ListAllAsync(CancellationToken ct = default);

    Task<bool> UpdateAsync(TaskRecurrenceRule rule, int expectedVersion, CancellationToken ct = default);
}

/// <summary>
/// Task types (DCP-005 slice 1). Shaped like <see cref="ITaskFieldDefinitionRepository"/> for the same reasons
/// its comments give — including the important one: the management surface must be able to see a RETIRED type,
/// or a type switched off could never be switched back on.
/// </summary>
public interface ITaskTypeRepository
{
    Task<TaskType> CreateAsync(TaskType type, CancellationToken ct = default);
    Task<TaskType?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TaskType?> GetByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Types a NEW task may be given — active and not retired.</summary>
    Task<IReadOnlyList<TaskType>> ListActiveAsync(CancellationToken ct = default);

    /// <summary>Every type, retired ones included — the management screen and the uniqueness check.</summary>
    Task<IReadOnlyList<TaskType>> ListAllAsync(CancellationToken ct = default);

    Task UpdateAsync(TaskType type, CancellationToken ct = default);
}

/// <summary>
/// The controlled-document reference list (DCP-005 slice 2) — versions and their entries.
///
/// <para>⚠ <b>THERE IS NO UPDATE.</b> The write path is the import; the read path is the search. A row that
/// could be edited here would be the second authority over a document that §6.1 exists to prevent.</para>
/// </summary>
public interface IDocumentReferenceListRepository
{
    Task<DocumentReferenceListVersion> CreateVersionAsync(
        DocumentReferenceListVersion version, CancellationToken ct = default);

    /// <summary>
    /// The LIVE version a re-upload of identical bytes would collide with, or null.
    ///
    /// <para>⚠ WITHDRAWN VERSIONS DO NOT COLLIDE. That is precisely how the trap is undone: the bytes of a
    /// version taken out of service can be loaded again, while two people importing the same file into a live
    /// list still get the 409 that stops two "current" lists existing.</para>
    /// </summary>
    Task<DocumentReferenceListVersion?> FindLiveVersionByHashAsync(
        string contentHash, CancellationToken ct = default);

    Task<DocumentReferenceListVersion?> GetVersionAsync(Guid id, CancellationToken ct = default);

    /// <summary>Stamp a withdrawal. There is no delete — see <c>DocumentReferenceListVersion.WithdrawnAt</c>.</summary>
    Task UpdateVersionAsync(DocumentReferenceListVersion version, CancellationToken ct = default);

    /// <summary>Newest first — the list screen and "which version did this task resolve against".</summary>
    Task<IReadOnlyList<DocumentReferenceListVersion>> ListVersionsAsync(CancellationToken ct = default);

    /// <summary>
    /// The current version: the newest one NOT withdrawn. Null before the first import, and null again if every
    /// version has been withdrawn — an honest "there is no list" rather than a stale one.
    /// </summary>
    Task<DocumentReferenceListVersion?> GetLatestVersionAsync(CancellationToken ct = default);

    Task AddEntriesAsync(IReadOnlyList<DocumentReferenceEntry> entries, CancellationToken ct = default);

    /// <summary>
    /// Search WITHIN one version. Blocked rows are returned like any other — they are shown and refused, not
    /// hidden (see <see cref="DocumentReferenceEntry.LinkableInErp"/>).
    /// </summary>
    Task<IReadOnlyList<DocumentReferenceEntry>> SearchAsync(
        Guid listVersionId, string? term, int limit, CancellationToken ct = default);
}
