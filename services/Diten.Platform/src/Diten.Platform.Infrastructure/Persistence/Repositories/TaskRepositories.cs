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
    public TaskItemRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "task_items")
    {
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

    public async Task<bool> UpdateAsync(TaskItem task, int expectedVersion, CancellationToken ct = default)
    {
        task.Version = expectedVersion + 1;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<TaskItem>.Filter.And(
            ExecutionFilter,
            Builders<TaskItem>.Filter.Eq(x => x.Id, task.Id),
            Builders<TaskItem>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, task, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }
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
            Builders<TaskFieldDefinition>.Filter.Eq(x => x.IsActive, true));
        return await Collection.Find(filter).SortBy(x => x.SortOrder).ToListAsync(ct);
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
