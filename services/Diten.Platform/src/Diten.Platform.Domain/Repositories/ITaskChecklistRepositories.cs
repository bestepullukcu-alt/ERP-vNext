using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public sealed record WorkTaskListQuery(
    string? Search,
    string? Status,
    string? AssigneeId,
    int Page,
    int PageSize);

public sealed record ChecklistTemplateListQuery(
    string? Search,
    string? Status,
    int Page,
    int PageSize);

public sealed record ChecklistRunListQuery(
    string? Search,
    string? Status,
    int Page,
    int PageSize);

public interface IWorkTaskRepository
{
    Task<(IReadOnlyList<WorkTask> Items, long TotalCount)> QueryAsync(WorkTaskListQuery query, CancellationToken ct = default);
    Task<WorkTask?> GetByIdAsync(Guid workTaskId, CancellationToken ct = default);
    Task<WorkTask> CreateAsync(WorkTask entity, CancellationToken ct = default);
    Task<bool> UpdateAsync(WorkTask entity, long expectedRowVersion, CancellationToken ct = default);

    Task CreateAssignmentAsync(TaskAssignment assignment, CancellationToken ct = default);
    Task DeactivateAssignmentsAsync(Guid workTaskId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskAssignment>> GetAssignmentsByTaskAsync(Guid workTaskId, CancellationToken ct = default);
}

public interface IChecklistRepository
{
    Task<(IReadOnlyList<ChecklistTemplate> Items, long TotalCount)> QueryTemplatesAsync(ChecklistTemplateListQuery query, CancellationToken ct = default);
    Task<ChecklistTemplate?> GetTemplateByIdAsync(Guid templateId, CancellationToken ct = default);
    Task<ChecklistTemplate?> GetTemplateByCodeAsync(string code, CancellationToken ct = default);
    Task<ChecklistTemplate> CreateTemplateAsync(ChecklistTemplate entity, CancellationToken ct = default);
    Task<bool> UpdateTemplateAsync(ChecklistTemplate entity, long expectedRowVersion, CancellationToken ct = default);

    Task<(IReadOnlyList<ChecklistRun> Items, long TotalCount)> QueryRunsAsync(ChecklistRunListQuery query, CancellationToken ct = default);
    Task<ChecklistRun?> GetRunByIdAsync(Guid runId, CancellationToken ct = default);
    Task<ChecklistRun> CreateRunAsync(ChecklistRun entity, CancellationToken ct = default);
    Task<bool> UpdateRunAsync(ChecklistRun entity, long expectedRowVersion, CancellationToken ct = default);
}
