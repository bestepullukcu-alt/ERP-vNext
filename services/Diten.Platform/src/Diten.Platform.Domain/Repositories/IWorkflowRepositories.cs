using Diten.Platform.Domain.Entities.Workflow;

namespace Diten.Platform.Domain.Repositories;

// MOD-0023 Batch 01 — create/read repository seams for the workflow aggregates. All reads are tenant
// scoped through the live TenantRepository<T> execution filter; cross-tenant reads return null/empty
// with no metadata leak. Mutation seams beyond create (publish, transitions) land in later batches.

public interface IWorkflowTemplateRepository
{
    Task<WorkflowTemplate> CreateAsync(WorkflowTemplate template, CancellationToken ct = default);
    Task<WorkflowTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowTemplate?> GetByTemplateCodeAsync(string templateCode, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTemplate>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(WorkflowTemplate template, int expectedVersion, CancellationToken ct = default);
}

public interface IWorkflowTemplateVersionRepository
{
    Task<WorkflowTemplateVersion> CreateAsync(WorkflowTemplateVersion version, CancellationToken ct = default);
    Task<WorkflowTemplateVersion?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowTemplateVersion?> GetByIdForTemplateAsync(Guid templateId, Guid id, CancellationToken ct = default);
    Task<WorkflowTemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken ct = default);
    Task<int> GetLatestVersionNumberAsync(Guid templateId, CancellationToken ct = default);
    Task<WorkflowTemplateVersion?> GetActivePublishedVersionAsync(Guid templateId, CancellationToken ct = default);
    Task<bool> ExistsVersionNumberAsync(Guid templateId, int versionNumber, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTemplateVersion>> ListByTemplateIdAsync(Guid templateId, CancellationToken ct = default);
    Task<WorkflowTemplateVersionUpdateResult> UpdateAsync(
        WorkflowTemplateVersion version,
        int expectedVersion,
        CancellationToken ct = default);
}

public enum WorkflowTemplateVersionUpdateResult
{
    Updated = 0,
    NotFoundOrConcurrencyConflict = 1,
    Immutable = 2
}

public interface IWorkflowInstanceRepository
{
    Task<WorkflowInstance> CreateAsync(WorkflowInstance instance, CancellationToken ct = default);
    Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<WorkflowInstance?> GetLatestByObjectRefAsync(
        string objectRef,
        string objectType,
        string objectId,
        CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default);
    Task<bool> UpdateEscalationOrTimeoutAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default) =>
        UpdateAsync(instance, expectedVersion, ct);

    // WP-CL-BE-3 — the terminal transition writes inside the Platform transaction that also carries the completion
    // outbox event. The Mongo repository overrides this with a session write; the default only serves in-memory
    // test doubles, which have no session to join.
    Task<bool> UpdateAsync(IPlatformTransactionSession session, WorkflowInstance instance, int expectedVersion,
        CancellationToken ct = default) =>
        UpdateAsync(instance, expectedVersion, ct);

    // WP-CL-BE-3 — batch status read for cross-service reconciliation (tenant scoped). Default = in-memory filter
    // for test doubles; the Mongo repository uses the {TenantId, ObjectType, ObjectId} index.
    async Task<IReadOnlyList<WorkflowInstance>> ListByObjectIdsAsync(
        string objectType,
        IReadOnlyCollection<string> objectIds,
        CancellationToken ct = default) =>
        (await GetAllForTenantAsync(ct))
            .Where(x => string.Equals(x.ObjectType, objectType, StringComparison.Ordinal) && objectIds.Contains(x.ObjectId))
            .ToList();
}

public interface IApprovalTaskRepository
{
    Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default);
    Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default);
    Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default);
    Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default);
    Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ApprovalTask>> ListOverdueTasksAsync(
        DateTimeOffset nowUtc,
        int maxItems,
        CancellationToken ct = default) =>
        GetAllForTenantAsync(ct);
    Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default);
    Task<bool> UpdateEscalationAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) =>
        UpdateAsync(task, expectedVersion, ct);

    // WP-CL-BE-3 — terminal transition write inside the Platform transaction (see IWorkflowInstanceRepository).
    Task<bool> UpdateAsync(IPlatformTransactionSession session, ApprovalTask task, int expectedVersion,
        CancellationToken ct = default) =>
        UpdateAsync(task, expectedVersion, ct);
}

public interface IRuntimeAssignmentSnapshotRepository
{
    Task<RuntimeAssignmentSnapshot> CreateAsync(RuntimeAssignmentSnapshot snapshot, CancellationToken ct = default);
    Task<RuntimeAssignmentSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default);
}

public interface IWorkflowTransitionLogRepository
{
    Task<WorkflowTransitionLog> CreateAsync(WorkflowTransitionLog log, CancellationToken ct = default);
    Task<WorkflowTransitionLog> AppendAsync(WorkflowTransitionLog log, CancellationToken ct = default) =>
        CreateAsync(log, ct);

    // WP-CL-BE-3 — terminal transition log inside the Platform transaction (see IWorkflowInstanceRepository).
    Task<WorkflowTransitionLog> CreateAsync(IPlatformTransactionSession session, WorkflowTransitionLog log,
        CancellationToken ct = default) =>
        CreateAsync(log, ct);
    Task<WorkflowTransitionLog?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowTransitionLog?> FindByIdempotencyAsync(
        Guid approvalTaskId,
        Domain.Enums.Workflow.WorkflowTransitionAction action,
        string idempotencyKey,
        CancellationToken ct = default) =>
        GetByTaskActionIdempotencyKeyAsync(approvalTaskId, action, idempotencyKey, ct);
    Task<WorkflowTransitionLog?> GetByTaskActionIdempotencyKeyAsync(
        Guid approvalTaskId,
        Domain.Enums.Workflow.WorkflowTransitionAction action,
        string idempotencyKey,
        CancellationToken ct = default);
    Task<long> GetLatestSequenceNoAsync(Guid workflowInstanceId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTransitionLog>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default);
}

public interface ISlaEscalationRuleRepository
{
    Task<SlaEscalationRule> CreateAsync(SlaEscalationRule rule, CancellationToken ct = default);
    Task<SlaEscalationRule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SlaEscalationRule>> ListActiveByTemplateIdAsync(Guid templateId, CancellationToken ct = default);
    Task<IReadOnlyList<SlaEscalationRule>> ListActiveAsync(CancellationToken ct = default);
    Task<SlaEscalationRule?> FindForStepAsync(
        Guid templateId,
        string stageCode,
        string stepCode,
        CancellationToken ct = default);
    Task DeactivateRulesForTemplateAsync(Guid templateId, CancellationToken ct = default);
}
