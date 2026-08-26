using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0024 — tenant-scoped repository implementations over the live TenantRepository<T> base. CreateAsync /
// GetByIdAsync / DeleteAsync (soft) are inherited: the base stamps TenantId from context on create and ANDs the
// tenant + IsDeleted execution filter into every read. Collections use the Platform snake_case plural convention.

public sealed class TaskItemRepository : TenantRepository<TaskItem>, ITaskItemRepository
{
    private readonly ITaskTransitionRepository _transitions;
    private readonly ITenantContext _tenantContext;

    public TaskItemRepository(
        IPlatformDbContext dbContext,
        ITenantContext tenantContext,
        ITaskTransitionRepository transitions)
        : base(dbContext.Database, tenantContext, "task_items")
    {
        _transitions = transitions;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllForTenantAsync(CancellationToken ct = default)
        => await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<TaskItem>> ListByAssigneeAsync(Guid userId, CancellationToken ct = default)
    {
        var filter = Builders<TaskItem>.Filter.And(
            ExecutionFilter,
            Builders<TaskItem>.Filter.Eq(x => x.AssigneeUserId, userId));
        return await Collection.Find(filter).SortBy(x => x.DueAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskItem>> ListByParentAsync(
        Guid parentTaskItemId,
        CancellationToken ct = default)
    {
        var filter = Builders<TaskItem>.Filter.And(
            ExecutionFilter,
            Builders<TaskItem>.Filter.Eq(x => x.ParentTaskItemId, parentTaskItemId));
        return await Collection.Find(filter).SortBy(x => x.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskItem>> ListByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var filter = Builders<TaskItem>.Filter.And(
            ExecutionFilter,
            Builders<TaskItem>.Filter.In(x => x.Id, ids));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskItem>> ListByParentsAsync(
        IReadOnlyCollection<Guid> parentTaskItemIds,
        CancellationToken ct = default)
    {
        if (parentTaskItemIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<TaskItem>.Filter.And(
            ExecutionFilter,
            Builders<TaskItem>.Filter.In(x => x.ParentTaskItemId, parentTaskItemIds.Cast<Guid?>()));
        return await Collection.Find(filter).SortBy(x => x.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskItem>> ListUnclaimedByPositionsAsync(
        IReadOnlyCollection<Guid> positionIds,
        CancellationToken ct = default)
    {
        if (positionIds.Count == 0)
        {
            return [];
        }

        // Unclaimed = a pool task with no holder yet. Terminal states are excluded: a finished task is not
        // claimable and must never appear in the pool.
        var filter = Builders<TaskItem>.Filter.And(
            ExecutionFilter,
            Builders<TaskItem>.Filter.Eq(x => x.AssignmentTarget, TaskAssignmentTarget.PositionPool),
            Builders<TaskItem>.Filter.In(x => x.PoolPositionId, positionIds.Cast<Guid?>()),
            Builders<TaskItem>.Filter.Eq(x => x.AssigneeUserId, (Guid?)null),
            Builders<TaskItem>.Filter.Nin(x => x.Lifecycle, new[] { TaskLifecycle.Done, TaskLifecycle.Cancelled }));
        return await Collection.Find(filter).SortBy(x => x.DueAt).ToListAsync(ct);
    }

    /// <summary>
    /// A new task, with the <see cref="TaskTransitionKind.Created"/> entry that opens its history.
    ///
    /// <para>The entry is what makes "this task has no history" ANSWERABLE rather than ambiguous. Every task
    /// written from WC-1 onwards opens its log with one, so a feed with no `created` in it is a task that predates
    /// the log — and the screen can say so instead of presenting a hole as a complete story. Nothing is
    /// backfilled: the moments before the log existed were never recorded, and inventing them is the exact defect
    /// the projection refused to ship.</para>
    /// </summary>
    public override async Task<TaskItem> CreateAsync(TaskItem task, CancellationToken ct = default)
    {
        var created = await base.CreateAsync(task, ct);

        var intent = task.ReadDeclaredIntent();
        await RecordAsync(
            task.Id,
            intent?.Kind ?? TaskTransitionKind.Created,
            from: task.Lifecycle,
            to: task.Lifecycle,
            intent,
            // No field changes on creation: there is no BEFORE to differ against, and listing every field a new
            // task was born with would say "changed" about values nobody changed.
            fieldChanges: null,
            ct);

        return created;
    }

    /*
     * The conditional write, and the ONLY place a lifecycle event is decided.
     *
     * FindOneAndReplace rather than ReplaceOne, for the PRE-IMAGE: the previous document comes back in the same
     * round trip, so "did this task actually move, and from what" is answered by comparing two real documents
     * rather than by trusting the caller. A null answer means the version filter did not match — the write was
     * refused, so nothing moved and nothing is recorded. That ordering is the point: a handler that declares a
     * transition and then loses a concurrency race writes NO history, because the history follows the commit.
     *
     * A handler that forgets to declare still gets its transition recorded, as Unknown. Refusing to record what
     * could not be named would restore the silent hole this log exists to close; the coverage test is what makes
     * Unknown loud.
     */
    public async Task<bool> UpdateAsync(TaskItem task, int expectedVersion, CancellationToken ct = default)
    {
        task.Version = expectedVersion + 1;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<TaskItem>.Filter.And(
            ExecutionFilter,
            Builders<TaskItem>.Filter.Eq(x => x.Id, task.Id),
            Builders<TaskItem>.Filter.Eq(x => x.Version, expectedVersion));

        var previous = await Collection.FindOneAndReplaceAsync(
            filter,
            task,
            new FindOneAndReplaceOptions<TaskItem> { ReturnDocument = ReturnDocument.Before },
            ct);

        if (previous is null)
        {
            return false;
        }

        await RecordIfMovedAsync(previous, task, ct);
        return true;
    }

    /// <summary>
    /// Records an entry when the write moved the task, and stays silent when it did not.
    ///
    /// <para>THREE fields count as movement: the lifecycle, the holder, and the acceptance mark. The third is
    /// there because BL-042 gave acceptance its own field — accepting a PLANNED task changes neither of the other
    /// two, so a diff that watched only lifecycle and assignee would drop accept from the history of exactly the
    /// tasks whose acceptance was worth recording.</para>
    ///
    /// <para>⚠ THE SECOND HALF OF THIS COMMENT USED TO SAY: "An edit that changes a title, a due date or a
    /// checklist moves none of the three and records nothing. This is a LIFECYCLE log, not a field-level audit
    /// trail; conflating the two would bury the six entries that tell the task's story under sixty that do
    /// not."</para>
    ///
    /// <para>The objection was right and it is ANSWERED rather than dismissed (owner decision, 2026-08-23):
    /// "who changed the due date" had no answer anywhere, and the burial it warned about is avoided by recording
    /// ONE entry per SAVE carrying the list of fields that moved — not one entry per field. Five fields changed
    /// together is one act and reads as one line. A CHECKLIST write still records nothing, deliberately: that is
    /// progress on the work, and it has its own container with its own state.</para>
    ///
    /// <para>The lifecycle and the field diff share ONE entry when a save does both, so a reassign is not
    /// reported twice — once as an act and once as a changed field.</para>
    /// </summary>
    private async Task RecordIfMovedAsync(TaskItem previous, TaskItem current, CancellationToken ct)
    {
        var moved = previous.Lifecycle != current.Lifecycle
                    || previous.AssigneeUserId != current.AssigneeUserId
                    || previous.AcceptedByUserId != current.AcceptedByUserId;

        var intent = current.ReadDeclaredIntent();
        /*
         * The diff runs on the pair already in hand — no extra read, and nothing at all when the save touched
         * none of the recorded fields.
         */
        var fieldChanges = TaskFieldDiff.Between(previous, current);

        /*
         * ⚠ AN `Edited` DECLARATION IS NOT ITSELF NEWS. The edit handler declares one on every save so the entry
         * can name its actor — but a save that changed nothing recorded must still write nothing, or every
         * "Kaydet" on an unmodified form would add a row saying so. Any OTHER declared intent is an act in its
         * own right (a claim, a plan) and is recorded whether or not a field moved.
         */
        var editedWithNothingToSay = intent?.Kind == TaskTransitionKind.Edited && fieldChanges.Count == 0;

        if ((!moved && intent is null && fieldChanges.Count == 0) || (!moved && editedWithNothingToSay))
        {
            return;
        }

        /*
         * WHICH KIND. A declared intent wins, then movement, and only a save that moved NOTHING but changed
         * fields is an `Edited`. Ordering it the other way would relabel every reassign as an edit, because a
         * reassign changes the assignee field too.
         */
        var kind = intent?.Kind
            ?? (moved ? TaskTransitionKind.Unknown : TaskTransitionKind.Edited);

        await RecordAsync(
            current.Id,
            kind,
            from: previous.Lifecycle,
            to: current.Lifecycle,
            intent,
            fieldChanges,
            ct);
    }

    private Task RecordAsync(
        Guid taskItemId,
        TaskTransitionKind kind,
        TaskLifecycle from,
        TaskLifecycle to,
        TaskTransitionIntent? intent,
        IReadOnlyList<TaskFieldChange>? fieldChanges,
        CancellationToken ct)
        => _transitions.CreateAsync(new TaskTransition
        {
            TenantId = _tenantContext.TenantId,
            TaskItemId = taskItemId,
            Kind = kind,
            FromLifecycle = from,
            ToLifecycle = to,
            ActorUserId = intent?.ActorUserId,
            Reason = intent?.Reason,
            ReasonCode = intent?.ReasonCode,
            FieldChanges = fieldChanges is null ? [] : [.. fieldChanges]
        }, ct);
}

/// <summary>
/// WC-1 — the lifecycle event log. Its own collection (see <see cref="TaskTransition"/> for why it is not an
/// array on the task).
/// </summary>
public sealed class TaskTransitionRepository : TenantRepository<TaskTransition>, ITaskTransitionRepository
{
    public TaskTransitionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "task_transitions")
    {
    }

    public async Task<IReadOnlyList<TaskTransition>> ListByTaskIdAsync(
        Guid taskItemId,
        CancellationToken ct = default)
    {
        var filter = Builders<TaskTransition>.Filter.And(
            ExecutionFilter,
            Builders<TaskTransition>.Filter.Eq(x => x.TaskItemId, taskItemId));
        return Order(await Collection.Find(filter).ToListAsync(ct));
    }

    public async Task<IReadOnlyList<TaskTransition>> ListByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken ct = default)
    {
        if (taskItemIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<TaskTransition>.Filter.And(
            ExecutionFilter,
            Builders<TaskTransition>.Filter.In(x => x.TaskItemId, taskItemIds));
        return Order(await Collection.Find(filter).ToListAsync(ct));
    }

    /*
     * Sorted IN MEMORY for the same reason TaskCommentRepository.Order is (BL-030): CreatedAt is a DateTimeOffset,
     * which this driver stores as a BSON ARRAY [ticks, offsetMinutes], and a server-side sort with the Id
     * tie-break below fails at runtime with "cannot sort with keys that are parallel arrays".
     *
     * Newest first and STABLE, matching the comment feed key for key — the two lists are merged into one stream,
     * and two halves ordered by different rules interleave wrongly wherever their timestamps meet.
     */
    private static IReadOnlyList<TaskTransition> Order(IEnumerable<TaskTransition> transitions)
        => transitions
            .OrderByDescending(transition => transition.CreatedAt)
            .ThenByDescending(transition => transition.Id)
            .ToList();
}

public sealed class TaskAssignmentRepository : TenantRepository<TaskAssignment>, ITaskAssignmentRepository
{
    public TaskAssignmentRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "task_assignments")
    {
    }

    public async Task<IReadOnlyList<TaskAssignment>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default)
    {
        var filter = Builders<TaskAssignment>.Filter.And(
            ExecutionFilter,
            Builders<TaskAssignment>.Filter.Eq(x => x.TaskItemId, taskItemId));
        return await Collection.Find(filter).SortBy(x => x.OccurredAt).ToListAsync(ct);
    }
}

public sealed class TaskDependencyRepository : TenantRepository<TaskDependency>, ITaskDependencyRepository
{
    public TaskDependencyRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "task_dependencies")
    {
    }

    public async Task<IReadOnlyList<TaskDependency>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default)
    {
        var filter = Builders<TaskDependency>.Filter.And(
            ExecutionFilter,
            Builders<TaskDependency>.Filter.Eq(x => x.TaskItemId, taskItemId));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskDependency>> ListByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken ct = default)
    {
        if (taskItemIds.Count == 0)
        {
            return [];
        }

        // EITHER end: an edge is part of both tasks' stories, and the cycle check walks it forwards too.
        var filter = Builders<TaskDependency>.Filter.And(
            ExecutionFilter,
            Builders<TaskDependency>.Filter.Or(
                Builders<TaskDependency>.Filter.In(x => x.TaskItemId, taskItemIds),
                Builders<TaskDependency>.Filter.In(x => x.DependsOnTaskItemId, taskItemIds)));
        return await Collection.Find(filter).ToListAsync(ct);
    }
}

/// <summary>
/// Task comments. Its own collection (see <see cref="TaskComment"/> for why it is not embedded).
/// </summary>
public sealed class TaskCommentRepository : TenantRepository<TaskComment>, ITaskCommentRepository
{
    public TaskCommentRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "task_comments")
    {
    }

    public async Task<IReadOnlyList<TaskComment>> ListByTaskIdAsync(
        Guid taskItemId,
        CancellationToken ct = default)
    {
        var filter = Builders<TaskComment>.Filter.And(
            ExecutionFilter,
            Builders<TaskComment>.Filter.Eq(x => x.TaskItemId, taskItemId));
        return Order(await Collection.Find(filter).ToListAsync(ct));
    }

    public async Task<IReadOnlyList<TaskComment>> ListByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken ct = default)
    {
        if (taskItemIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<TaskComment>.Filter.And(
            ExecutionFilter,
            Builders<TaskComment>.Filter.In(x => x.TaskItemId, taskItemIds));
        return Order(await Collection.Find(filter).ToListAsync(ct));
    }

    public async Task UpdateAsync(TaskComment comment, CancellationToken ct = default)
    {
        comment.UpdatedAt = DateTimeOffset.UtcNow;
        // The tenant filter still applies: another tenant's comment matches nothing and is not overwritten.
        var filter = Builders<TaskComment>.Filter.And(
            ExecutionFilter,
            Builders<TaskComment>.Filter.Eq(x => x.Id, comment.Id));
        await Collection.ReplaceOneAsync(filter, comment, new ReplaceOptions(), ct);
    }

    /*
     * Sorted IN MEMORY, deliberately (BL-030). CreatedAt is a DateTimeOffset, which this driver stores as a BSON
     * ARRAY [ticks, offsetMinutes]; a server-side sort that touches a second date key fails at runtime with
     * "cannot sort with keys that are parallel arrays", and the tie-break on Id below would be exactly that kind
     * of multi-key sort. A comment list is bounded by one task's conversation, so ordering it here costs nothing.
     *
     * Newest first, because the composer sits at the top of the feed. The tie-break on Id makes it STABLE: two
     * comments written in the same instant must not swap places between reads.
     */
    private static IReadOnlyList<TaskComment> Order(IEnumerable<TaskComment> comments)
        => comments
            .OrderByDescending(comment => comment.CreatedAt)
            .ThenByDescending(comment => comment.Id)
            .ToList();
}

public sealed class TaskWatcherRepository : TenantRepository<TaskWatcher>, ITaskWatcherRepository
{
    public TaskWatcherRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "task_watchers")
    {
    }

    public async Task<IReadOnlyList<TaskWatcher>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default)
    {
        var filter = Builders<TaskWatcher>.Filter.And(
            ExecutionFilter,
            Builders<TaskWatcher>.Filter.Eq(x => x.TaskItemId, taskItemId));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskWatcher>> ListByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken ct = default)
    {
        if (taskItemIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<TaskWatcher>.Filter.And(
            ExecutionFilter,
            Builders<TaskWatcher>.Filter.In(x => x.TaskItemId, taskItemIds));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskWatcher>> ListByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var filter = Builders<TaskWatcher>.Filter.And(
            ExecutionFilter,
            Builders<TaskWatcher>.Filter.Eq(x => x.UserId, userId));
        return await Collection.Find(filter).ToListAsync(ct);
    }
}

public sealed class TaskFieldDefinitionRepository
    : TenantRepository<TaskFieldDefinition>, ITaskFieldDefinitionRepository
{
    public TaskFieldDefinitionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "task_field_definitions")
    {
    }

    public Task<TaskFieldDefinition?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var filter = Builders<TaskFieldDefinition>.Filter.And(
            ExecutionFilter,
            Builders<TaskFieldDefinition>.Filter.Eq(x => x.Code, code));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public async Task<IReadOnlyList<TaskFieldDefinition>> ListActiveAsync(CancellationToken ct = default)
    {
        var filter = Builders<TaskFieldDefinition>.Filter.And(
            ExecutionFilter,
            Builders<TaskFieldDefinition>.Filter.Eq(x => x.IsActive, true),
            // A retired definition must not be offered to the value validator, even if somebody flipped IsActive
            // back on afterwards: DeletedAt is the stronger statement of the two.
            Builders<TaskFieldDefinition>.Filter.Eq(x => x.DeletedAt, null));
        return await Collection.Find(filter).SortBy(x => x.SortOrder).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskFieldDefinition>> ListAllAsync(CancellationToken ct = default)
        => await Collection.Find(ExecutionFilter).SortBy(x => x.SortOrder).ToListAsync(ct);

    public async Task<bool> UpdateAsync(
        TaskFieldDefinition definition, int expectedVersion, CancellationToken ct = default)
    {
        definition.Version = expectedVersion + 1;
        definition.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<TaskFieldDefinition>.Filter.And(
            ExecutionFilter,
            Builders<TaskFieldDefinition>.Filter.Eq(x => x.Id, definition.Id),
            Builders<TaskFieldDefinition>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, definition, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }
}

/// <summary>
/// Task types (DCP-005 slice 1). Shaped after <see cref="TaskFieldDefinitionRepository"/> above — the sibling
/// this whole slice is modelled on.
/// </summary>
public sealed class TaskTypeRepository : TenantRepository<TaskType>, ITaskTypeRepository
{
    public TaskTypeRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "task_types")
    {
    }

    public Task<TaskType?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var filter = Builders<TaskType>.Filter.And(
            ExecutionFilter,
            Builders<TaskType>.Filter.Eq(x => x.Code, code));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public async Task<IReadOnlyList<TaskType>> ListActiveAsync(CancellationToken ct = default)
    {
        var filter = Builders<TaskType>.Filter.And(
            ExecutionFilter,
            Builders<TaskType>.Filter.Eq(x => x.IsActive, true),
            // Same two-lock rule the field definitions use: DeletedAt is the stronger statement of the two.
            Builders<TaskType>.Filter.Eq(x => x.DeletedAt, null));
        return await Collection.Find(filter).SortBy(x => x.Code).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskType>> ListAllAsync(CancellationToken ct = default)
        => await Collection.Find(ExecutionFilter).SortBy(x => x.Code).ToListAsync(ct);

    public async Task UpdateAsync(TaskType type, CancellationToken ct = default)
    {
        type.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<TaskType>.Filter.And(
            ExecutionFilter,
            Builders<TaskType>.Filter.Eq(x => x.Id, type.Id));
        await Collection.ReplaceOneAsync(filter, type, new ReplaceOptions(), ct);
    }
}

// ── Phase 2+ repositories. Registered now so the schema/collections exist and later phases are additive. ──

public sealed class ChecklistTemplateRepository : TenantRepository<ChecklistTemplate>, IChecklistTemplateRepository
{
    public ChecklistTemplateRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "checklist_templates")
    {
    }

    public async Task<IReadOnlyList<ChecklistTemplate>> ListActiveAsync(CancellationToken ct = default)
    {
        var filter = Builders<ChecklistTemplate>.Filter.And(
            ExecutionFilter,
            Builders<ChecklistTemplate>.Filter.Eq(x => x.IsActive, true));
        return await Collection.Find(filter).SortBy(x => x.Name).ToListAsync(ct);
    }
}

public sealed class ChecklistRunRepository : TenantRepository<ChecklistRun>, IChecklistRunRepository
{
    public ChecklistRunRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "checklist_runs")
    {
    }

    public Task<ChecklistRun?> GetByTaskIdAsync(Guid taskItemId, CancellationToken ct = default)
    {
        var filter = Builders<ChecklistRun>.Filter.And(
            ExecutionFilter,
            Builders<ChecklistRun>.Filter.Eq(x => x.TaskItemId, taskItemId));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public async Task<IReadOnlyList<ChecklistRun>> ListByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken ct = default)
    {
        if (taskItemIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<ChecklistRun>.Filter.And(
            ExecutionFilter,
            Builders<ChecklistRun>.Filter.In(x => x.TaskItemId, taskItemIds));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<bool> UpdateAsync(ChecklistRun run, int expectedVersion, CancellationToken ct = default)
    {
        run.Version = expectedVersion + 1;
        run.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<ChecklistRun>.Filter.And(
            ExecutionFilter,
            Builders<ChecklistRun>.Filter.Eq(x => x.Id, run.Id),
            Builders<ChecklistRun>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, run, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }
}

public sealed class TaskTemplateRepository : TenantRepository<TaskTemplate>, ITaskTemplateRepository
{
    public TaskTemplateRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "task_templates")
    {
    }

    public async Task<IReadOnlyList<TaskTemplate>> ListActiveAsync(CancellationToken ct = default)
    {
        var filter = Builders<TaskTemplate>.Filter.And(
            ExecutionFilter,
            Builders<TaskTemplate>.Filter.Eq(x => x.IsActive, true));
        return await Collection.Find(filter).SortBy(x => x.Name).ToListAsync(ct);
    }
}

public sealed class TaskRecurrenceRuleRepository
    : TenantRepository<TaskRecurrenceRule>, ITaskRecurrenceRuleRepository
{
    public TaskRecurrenceRuleRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "task_recurrence_rules")
    {
    }

    public async Task<IReadOnlyList<TaskRecurrenceRule>> ListActiveAsync(CancellationToken ct = default)
    {
        var filter = Builders<TaskRecurrenceRule>.Filter.And(
            ExecutionFilter,
            Builders<TaskRecurrenceRule>.Filter.Eq(x => x.IsActive, true));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskRecurrenceRule>> ListAllAsync(CancellationToken ct = default)
    {
        /*
         * Sorted by ONE DateTimeOffset field, in the driver.
         *
         * BL-030: this entity carries three of them (StartsAt, EndsAt, LastGeneratedAt) and MongoDB serializes a
         * DateTimeOffset as an ARRAY, so a two-key server-side sort over any pair of them fails at runtime with
         * "cannot sort with keys that are parallel arrays". That is exactly how GetLatestByObjectRefAsync broke.
         * One key is safe; a second one belongs in memory.
         */
        return await Collection.Find(ExecutionFilter)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<bool> UpdateAsync(TaskRecurrenceRule rule, int expectedVersion, CancellationToken ct = default)
    {
        rule.Version = expectedVersion + 1;
        rule.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<TaskRecurrenceRule>.Filter.And(
            ExecutionFilter,
            Builders<TaskRecurrenceRule>.Filter.Eq(x => x.Id, rule.Id),
            Builders<TaskRecurrenceRule>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, rule, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }
}

/// <summary>
/// WC-1 — one reader's private overlay per task. The user id is ANDed into every filter here, so "only the
/// author sees their notes" holds even if a caller forgets to ask for it.
/// </summary>
public sealed class TaskPersonalOverlayRepository
    : TenantRepository<TaskPersonalOverlay>, ITaskPersonalOverlayRepository
{
    public TaskPersonalOverlayRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "task_personal_overlays")
    {
    }

    public async Task<TaskPersonalOverlay?> GetAsync(
        Guid taskItemId,
        Guid userId,
        CancellationToken ct = default)
    {
        var filter = Builders<TaskPersonalOverlay>.Filter.And(
            ExecutionFilter,
            Builders<TaskPersonalOverlay>.Filter.Eq(x => x.TaskItemId, taskItemId),
            Builders<TaskPersonalOverlay>.Filter.Eq(x => x.UserId, userId));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TaskPersonalOverlay>> ListForUserAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        Guid userId,
        CancellationToken ct = default)
    {
        if (taskItemIds.Count == 0)
        {
            return [];
        }

        var filter = Builders<TaskPersonalOverlay>.Filter.And(
            ExecutionFilter,
            Builders<TaskPersonalOverlay>.Filter.In(x => x.TaskItemId, taskItemIds),
            Builders<TaskPersonalOverlay>.Filter.Eq(x => x.UserId, userId));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task UpsertAsync(TaskPersonalOverlay overlay, CancellationToken ct = default)
    {
        overlay.UpdatedAt = DateTimeOffset.UtcNow;

        /*
         * The tenant is NOT re-stamped here — TenantId is init-only, so it is set where the document is built and
         * cannot drift afterwards. The filter below still pins the tenant, so an overlay carrying somebody else's
         * tenant id would match nothing and insert nothing rather than overwrite across the boundary.
         *
         * Matched on (tenant, task, user) rather than on the document id: the caller may have built a brand-new
         * overlay for a reader who has never written one, and matching on its freshly generated id would insert a
         * SECOND overlay for the same pair every time. The pair is the natural key — the unique index in
         * MongoDbIndexConfigurations says so too.
         */
        var filter = Builders<TaskPersonalOverlay>.Filter.And(
            Builders<TaskPersonalOverlay>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<TaskPersonalOverlay>.Filter.Eq(x => x.TaskItemId, overlay.TaskItemId),
            Builders<TaskPersonalOverlay>.Filter.Eq(x => x.UserId, overlay.UserId));
        await Collection.ReplaceOneAsync(filter, overlay, new ReplaceOptions { IsUpsert = true }, ct);
    }
}
