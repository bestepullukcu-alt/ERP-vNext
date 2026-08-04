using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

// MOD-0023 Batch 01 — tenant-scoped repository implementations over the live TenantRepository<T> base.
// CreateAsync / GetByIdAsync are inherited (the base sets TenantId from context on create and applies
// the tenant + IsDeleted execution filter on read). Collections use snake_case, area-prefixed names.

public sealed class WorkflowTemplateRepository : TenantRepository<WorkflowTemplate>, IWorkflowTemplateRepository
{
    public WorkflowTemplateRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "workflow_templates")
    {
    }

    public Task<WorkflowTemplate?> GetByTemplateCodeAsync(string templateCode, CancellationToken ct = default)
    {
        var filter = Builders<WorkflowTemplate>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplate>.Filter.Eq(x => x.TemplateCode, templateCode));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public async Task<IReadOnlyList<WorkflowTemplate>> GetAllForTenantAsync(CancellationToken ct = default)
    {
        return await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);
    }

    public async Task<bool> UpdateAsync(WorkflowTemplate template, int expectedVersion, CancellationToken ct = default)
    {
        template.Version = expectedVersion + 1;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<WorkflowTemplate>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplate>.Filter.Eq(x => x.Id, template.Id),
            Builders<WorkflowTemplate>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, template, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }
}

public sealed class WorkflowTemplateVersionRepository
    : TenantRepository<WorkflowTemplateVersion>, IWorkflowTemplateVersionRepository
{
    public WorkflowTemplateVersionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "workflow_template_versions")
    {
    }

    public Task<WorkflowTemplateVersion?> GetByIdForTemplateAsync(
        Guid templateId,
        Guid id,
        CancellationToken ct = default)
    {
        var filter = Builders<WorkflowTemplateVersion>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.TemplateId, templateId),
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.Id, id));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public Task<WorkflowTemplateVersion?> GetLatestVersionAsync(Guid templateId, CancellationToken ct = default)
    {
        var filter = Builders<WorkflowTemplateVersion>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.TemplateId, templateId));
        return Collection.Find(filter).SortByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct)!;
    }

    public async Task<int> GetLatestVersionNumberAsync(Guid templateId, CancellationToken ct = default)
    {
        var latest = await GetLatestVersionAsync(templateId, ct);
        return latest?.VersionNumber ?? 0;
    }

    public Task<WorkflowTemplateVersion?> GetActivePublishedVersionAsync(Guid templateId, CancellationToken ct = default)
    {
        var filter = Builders<WorkflowTemplateVersion>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.TemplateId, templateId),
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.Status, Domain.Enums.Workflow.WorkflowTemplateVersionStatus.Published),
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.IsImmutable, true));
        return Collection.Find(filter).SortByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct)!;
    }

    public async Task<bool> ExistsVersionNumberAsync(Guid templateId, int versionNumber, CancellationToken ct = default)
    {
        var filter = Builders<WorkflowTemplateVersion>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.TemplateId, templateId),
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.VersionNumber, versionNumber));
        return await Collection.Find(filter).AnyAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowTemplateVersion>> ListByTemplateIdAsync(
        Guid templateId,
        CancellationToken ct = default)
    {
        var filter = Builders<WorkflowTemplateVersion>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.TemplateId, templateId));
        return await Collection.Find(filter).SortByDescending(x => x.VersionNumber).ToListAsync(ct);
    }

    public async Task<WorkflowTemplateVersionUpdateResult> UpdateAsync(
        WorkflowTemplateVersion version,
        int expectedVersion,
        CancellationToken ct = default)
    {
        var immutableFilter = Builders<WorkflowTemplateVersion>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.Id, version.Id),
            Builders<WorkflowTemplateVersion>.Filter.Or(
                Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.IsImmutable, true),
                Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.Status, Domain.Enums.Workflow.WorkflowTemplateVersionStatus.Published)));
        if (await Collection.Find(immutableFilter).AnyAsync(ct))
        {
            return WorkflowTemplateVersionUpdateResult.Immutable;
        }

        version.Version = expectedVersion + 1;
        version.UpdatedAt = DateTimeOffset.UtcNow;
        version.ConcurrencyToken = Guid.NewGuid().ToString("N");
        var filter = Builders<WorkflowTemplateVersion>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.Id, version.Id),
            Builders<WorkflowTemplateVersion>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, version, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1
            ? WorkflowTemplateVersionUpdateResult.Updated
            : WorkflowTemplateVersionUpdateResult.NotFoundOrConcurrencyConflict;
    }
}

public sealed class WorkflowInstanceRepository : TenantRepository<WorkflowInstance>, IWorkflowInstanceRepository
{
    public WorkflowInstanceRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "workflow_instances")
    {
    }

    public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        var filter = Builders<WorkflowInstance>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowInstance>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public async Task<WorkflowInstance?> GetLatestByObjectRefAsync(
        string objectRef,
        string objectType,
        string objectId,
        CancellationToken ct = default)
    {
        var filter = Builders<WorkflowInstance>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowInstance>.Filter.Eq(x => x.ObjectRef, objectRef),
            Builders<WorkflowInstance>.Filter.Eq(x => x.ObjectType, objectType),
            Builders<WorkflowInstance>.Filter.Eq(x => x.ObjectId, objectId));
        var candidates = await Collection
            .Find(filter)
            .ToListAsync(ct);

        return candidates
            .OrderByDescending(x => x.StartedAt)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default)
    {
        return await Collection.Find(ExecutionFilter).SortByDescending(x => x.StartedAt).ToListAsync(ct);
    }

    public async Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default)
    {
        instance.Version = expectedVersion + 1;
        instance.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<WorkflowInstance>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowInstance>.Filter.Eq(x => x.Id, instance.Id),
            Builders<WorkflowInstance>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, instance, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    public Task<bool> UpdateEscalationOrTimeoutAsync(
        WorkflowInstance instance,
        int expectedVersion,
        CancellationToken ct = default) =>
        UpdateAsync(instance, expectedVersion, ct);
}

public sealed class ApprovalTaskRepository : TenantRepository<ApprovalTask>, IApprovalTaskRepository
{
    public ApprovalTaskRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "approval_tasks")
    {
    }

    public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default)
    {
        var filter = Builders<ApprovalTask>.Filter.And(
            ExecutionFilter,
            Builders<ApprovalTask>.Filter.Eq(x => x.WorkflowInstanceId, workflowInstanceId));
        return Collection.Find(filter).SortBy(x => x.CreatedAt).FirstOrDefaultAsync(ct)!;
    }

    public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default)
    {
        var filter = Builders<ApprovalTask>.Filter.And(
            ExecutionFilter,
            Builders<ApprovalTask>.Filter.Eq(x => x.WorkflowInstanceId, workflowInstanceId),
            Builders<ApprovalTask>.Filter.In(
                x => x.Status,
                [ApprovalTaskStatus.WaitingApproval, ApprovalTaskStatus.WaitingEvidence]));
        return Collection.Find(filter).SortByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct)!;
    }

    public async Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default)
    {
        var filter = Builders<ApprovalTask>.Filter.And(
            ExecutionFilter,
            Builders<ApprovalTask>.Filter.Eq(x => x.WorkflowInstanceId, workflowInstanceId));
        return await Collection.Find(filter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default)
    {
        return await Collection.Find(ExecutionFilter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ApprovalTask>> ListOverdueTasksAsync(
        DateTimeOffset nowUtc,
        int maxItems,
        CancellationToken ct = default)
    {
        var filter = Builders<ApprovalTask>.Filter.And(
            ExecutionFilter,
            Builders<ApprovalTask>.Filter.In(
                x => x.Status,
                [
                    ApprovalTaskStatus.WaitingApproval,
                    ApprovalTaskStatus.WaitingEvidence,
                    ApprovalTaskStatus.Escalated,
                    ApprovalTaskStatus.TimedOut
                ]),
            Builders<ApprovalTask>.Filter.Ne(x => x.DueAt, null),
            Builders<ApprovalTask>.Filter.Lte(x => x.DueAt, nowUtc));
        return await Collection.Find(filter)
            .SortBy(x => x.DueAt)
            .Limit(Math.Max(1, maxItems))
            .ToListAsync(ct);
    }

    public async Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default)
    {
        task.Version = expectedVersion + 1;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<ApprovalTask>.Filter.And(
            ExecutionFilter,
            Builders<ApprovalTask>.Filter.Eq(x => x.Id, task.Id),
            Builders<ApprovalTask>.Filter.Eq(x => x.Version, expectedVersion));
        var result = await Collection.ReplaceOneAsync(filter, task, new ReplaceOptions(), ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    public Task<bool> UpdateEscalationAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) =>
        UpdateAsync(task, expectedVersion, ct);
}

public sealed class RuntimeAssignmentSnapshotRepository
    : TenantRepository<RuntimeAssignmentSnapshot>, IRuntimeAssignmentSnapshotRepository
{
    public RuntimeAssignmentSnapshotRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "workflow_runtime_assignment_snapshots")
    {
    }

    public async Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(
        Guid workflowInstanceId,
        CancellationToken ct = default)
    {
        var filter = Builders<RuntimeAssignmentSnapshot>.Filter.And(
            ExecutionFilter,
            Builders<RuntimeAssignmentSnapshot>.Filter.Eq(x => x.WorkflowInstanceId, workflowInstanceId));
        return await Collection.Find(filter).SortByDescending(x => x.ResolvedAt).ToListAsync(ct);
    }
}

public sealed class WorkflowTransitionLogRepository : TenantRepository<WorkflowTransitionLog>, IWorkflowTransitionLogRepository
{
    public WorkflowTransitionLogRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "workflow_transition_logs")
    {
    }

    public async Task<IReadOnlyList<WorkflowTransitionLog>> ListByInstanceIdAsync(
        Guid workflowInstanceId,
        CancellationToken ct = default)
    {
        var filter = Builders<WorkflowTransitionLog>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTransitionLog>.Filter.Eq(x => x.WorkflowInstanceId, workflowInstanceId));
        return await Collection.Find(filter).SortBy(x => x.SequenceNo).ToListAsync(ct);
    }

    public Task<WorkflowTransitionLog?> GetByTaskActionIdempotencyKeyAsync(
        Guid approvalTaskId,
        Domain.Enums.Workflow.WorkflowTransitionAction action,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var filter = Builders<WorkflowTransitionLog>.Filter.And(
            ExecutionFilter,
            Builders<WorkflowTransitionLog>.Filter.Eq(x => x.ApprovalTaskId, approvalTaskId),
            Builders<WorkflowTransitionLog>.Filter.Eq(x => x.Action, action),
            Builders<WorkflowTransitionLog>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public Task<WorkflowTransitionLog?> FindByIdempotencyAsync(
        Guid approvalTaskId,
        Domain.Enums.Workflow.WorkflowTransitionAction action,
        string idempotencyKey,
        CancellationToken ct = default) =>
        GetByTaskActionIdempotencyKeyAsync(approvalTaskId, action, idempotencyKey, ct);

    public Task<WorkflowTransitionLog> AppendAsync(WorkflowTransitionLog log, CancellationToken ct = default) =>
        CreateAsync(log, ct);

    public async Task<long> GetLatestSequenceNoAsync(Guid workflowInstanceId, CancellationToken ct = default)
    {
        var latest = await Collection
            .Find(Builders<WorkflowTransitionLog>.Filter.And(
                ExecutionFilter,
                Builders<WorkflowTransitionLog>.Filter.Eq(x => x.WorkflowInstanceId, workflowInstanceId)))
            .SortByDescending(x => x.SequenceNo)
            .FirstOrDefaultAsync(ct);
        return latest?.SequenceNo ?? 0;
    }
}

public sealed class SlaEscalationRuleRepository : TenantRepository<SlaEscalationRule>, ISlaEscalationRuleRepository
{
    public SlaEscalationRuleRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "workflow_sla_rules")
    {
    }

    public async Task<IReadOnlyList<SlaEscalationRule>> ListActiveByTemplateIdAsync(
        Guid templateId,
        CancellationToken ct = default)
    {
        var filter = Builders<SlaEscalationRule>.Filter.And(
            ExecutionFilter,
            Builders<SlaEscalationRule>.Filter.Eq(x => x.TemplateId, templateId),
            Builders<SlaEscalationRule>.Filter.Eq(x => x.IsActive, true));
        return await Collection.Find(filter).SortBy(x => x.StageCode).ThenBy(x => x.StepCode).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SlaEscalationRule>> ListActiveAsync(CancellationToken ct = default)
    {
        var filter = Builders<SlaEscalationRule>.Filter.And(
            ExecutionFilter,
            Builders<SlaEscalationRule>.Filter.Eq(x => x.IsActive, true));
        return await Collection.Find(filter).SortBy(x => x.TemplateId).ThenBy(x => x.StageCode).ToListAsync(ct);
    }

    public Task<SlaEscalationRule?> FindForStepAsync(
        Guid templateId,
        string stageCode,
        string stepCode,
        CancellationToken ct = default)
    {
        var filter = Builders<SlaEscalationRule>.Filter.And(
            ExecutionFilter,
            Builders<SlaEscalationRule>.Filter.Eq(x => x.TemplateId, templateId),
            Builders<SlaEscalationRule>.Filter.Eq(x => x.StageCode, stageCode),
            Builders<SlaEscalationRule>.Filter.Eq(x => x.StepCode, stepCode),
            Builders<SlaEscalationRule>.Filter.Eq(x => x.IsActive, true));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public async Task DeactivateRulesForTemplateAsync(Guid templateId, CancellationToken ct = default)
    {
        var filter = Builders<SlaEscalationRule>.Filter.And(
            ExecutionFilter,
            Builders<SlaEscalationRule>.Filter.Eq(x => x.TemplateId, templateId),
            Builders<SlaEscalationRule>.Filter.Eq(x => x.IsActive, true));

        var update = Builders<SlaEscalationRule>.Update
            .Set(x => x.IsActive, false)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
    }
}
