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

public interface ITaskAssignmentRepository
{
    Task<TaskAssignment> CreateAsync(TaskAssignment assignment, CancellationToken ct = default);
    Task<IReadOnlyList<TaskAssignment>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default);
}

public interface ITaskDependencyRepository
{
    Task<TaskDependency> CreateAsync(TaskDependency dependency, CancellationToken ct = default);
    Task<IReadOnlyList<TaskDependency>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ITaskWatcherRepository
{
    Task<TaskWatcher> CreateAsync(TaskWatcher watcher, CancellationToken ct = default);
    Task<IReadOnlyList<TaskWatcher>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskWatcher>> ListByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ITaskFieldDefinitionRepository
{
    Task<TaskFieldDefinition> CreateAsync(TaskFieldDefinition definition, CancellationToken ct = default);
    Task<TaskFieldDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TaskFieldDefinition?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<TaskFieldDefinition>> ListActiveAsync(CancellationToken ct = default);
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
    Task<bool> UpdateAsync(TaskRecurrenceRule rule, int expectedVersion, CancellationToken ct = default);
}
