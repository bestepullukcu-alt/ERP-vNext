using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public sealed record WorkflowDefinitionListQuery(string? Search, string? Status, int Page, int PageSize);
public sealed record WorkflowInstanceListQuery(string? Search, string? Status, int Page, int PageSize);
public sealed record ApprovalTaskListQuery(string? Status, string? AssigneeRole, string? AssigneeId, Guid? InstanceId, int Page, int PageSize);

public interface IWorkflowDefinitionRepository
{
    Task<(IReadOnlyList<WorkflowDefinition> Items, long TotalCount)> QueryAsync(WorkflowDefinitionListQuery query, CancellationToken ct = default);
    Task<WorkflowDefinition?> GetByIdAsync(Guid definitionId, CancellationToken ct = default);
    Task<WorkflowDefinition?> GetByCodeVersionAsync(string code, int versionNumber, CancellationToken ct = default);
    Task<int> GetLatestVersionNumberAsync(string code, CancellationToken ct = default);
    Task<WorkflowDefinition?> GetPublishedByCodeAsync(string code, CancellationToken ct = default);
    Task<WorkflowDefinition> CreateAsync(WorkflowDefinition entity, CancellationToken ct = default);
    Task<bool> UpdateAsync(WorkflowDefinition entity, long expectedRowVersion, CancellationToken ct = default);
}

public interface IWorkflowRuntimeRepository
{
    Task<(IReadOnlyList<WorkflowInstance> Items, long TotalCount)> QueryInstancesAsync(WorkflowInstanceListQuery query, CancellationToken ct = default);
    Task<WorkflowInstance?> GetInstanceByIdAsync(Guid instanceId, CancellationToken ct = default);
    Task<WorkflowInstance> CreateInstanceAsync(WorkflowInstance entity, CancellationToken ct = default);
    Task<bool> UpdateInstanceAsync(WorkflowInstance entity, long expectedRowVersion, CancellationToken ct = default);

    Task<(IReadOnlyList<ApprovalTask> Items, long TotalCount)> QueryTasksAsync(ApprovalTaskListQuery query, CancellationToken ct = default);
    Task<ApprovalTask?> GetTaskByIdAsync(Guid taskId, CancellationToken ct = default);
    Task<IReadOnlyList<ApprovalTask>> GetTasksByInstanceAsync(Guid instanceId, CancellationToken ct = default);
    Task<ApprovalTask> CreateTaskAsync(ApprovalTask entity, CancellationToken ct = default);
    Task<bool> UpdateTaskAsync(ApprovalTask entity, long expectedRowVersion, CancellationToken ct = default);
}
