using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories.WorkflowDesigner;

public sealed class WorkflowRuntimeRepository : TenantRepository<WorkflowInstance>, IWorkflowRuntimeRepository
{
    private readonly IMongoCollection<ApprovalTask> _tasks;

    public WorkflowRuntimeRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "platform_workflow_instances")
    {
        _tasks = dbContext.GetCollection<ApprovalTask>("platform_workflow_approval_tasks");
    }

    private FilterDefinition<WorkflowInstance> InstanceScope =>
        Builders<WorkflowInstance>.Filter.And(
            Builders<WorkflowInstance>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<WorkflowInstance>.Filter.Eq(x => x.IsDeleted, false));

    private FilterDefinition<ApprovalTask> TaskScope =>
        Builders<ApprovalTask>.Filter.And(
            Builders<ApprovalTask>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<ApprovalTask>.Filter.Eq(x => x.IsDeleted, false));

    public async Task<(IReadOnlyList<WorkflowInstance> Items, long TotalCount)> QueryInstancesAsync(WorkflowInstanceListQuery query, CancellationToken ct = default)
    {
        var filter = InstanceScope;
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filter = Builders<WorkflowInstance>.Filter.And(filter,
                Builders<WorkflowInstance>.Filter.Regex(x => x.SubjectReference, new MongoDB.Bson.BsonRegularExpression(query.Search.Trim(), "i")));
        }
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<WorkflowInstanceStatus>(query.Status.Trim(), true, out var status))
        {
            filter = Builders<WorkflowInstance>.Filter.And(filter, Builders<WorkflowInstance>.Filter.Eq(x => x.Status, status));
        }

        var total = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 25 : query.PageSize;
        var items = await Collection.Find(filter)
            .SortByDescending(x => x.StartedAt)
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<WorkflowInstance?> GetInstanceByIdAsync(Guid instanceId, CancellationToken ct = default)
    {
        var filter = Builders<WorkflowInstance>.Filter.And(InstanceScope,
            Builders<WorkflowInstance>.Filter.Eq(x => x.WorkflowInstanceId, instanceId));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public new async Task<WorkflowInstance> CreateInstanceAsync(WorkflowInstance entity, CancellationToken ct = default)
        => await base.CreateAsync(entity, ct);

    public async Task<bool> UpdateInstanceAsync(WorkflowInstance entity, long expectedRowVersion, CancellationToken ct = default)
    {
        entity.RowVersion = expectedRowVersion + 1;
        var filter = Builders<WorkflowInstance>.Filter.And(
            Builders<WorkflowInstance>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<WorkflowInstance>.Filter.Eq(x => x.IsDeleted, false),
            Builders<WorkflowInstance>.Filter.Eq(x => x.WorkflowInstanceId, entity.WorkflowInstanceId),
            Builders<WorkflowInstance>.Filter.Eq(x => x.RowVersion, expectedRowVersion));
        var result = await Collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    public async Task<(IReadOnlyList<ApprovalTask> Items, long TotalCount)> QueryTasksAsync(ApprovalTaskListQuery query, CancellationToken ct = default)
    {
        var filter = TaskScope;
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<ApprovalTaskStatus>(query.Status.Trim(), true, out var status))
        {
            filter = Builders<ApprovalTask>.Filter.And(filter, Builders<ApprovalTask>.Filter.Eq(x => x.Status, status));
        }
        if (!string.IsNullOrWhiteSpace(query.AssigneeRole))
        {
            filter = Builders<ApprovalTask>.Filter.And(filter, Builders<ApprovalTask>.Filter.Eq(x => x.AssigneeRole, query.AssigneeRole.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(query.AssigneeId))
        {
            filter = Builders<ApprovalTask>.Filter.And(filter, Builders<ApprovalTask>.Filter.Eq(x => x.AssigneeId, query.AssigneeId.Trim()));
        }
        if (query.InstanceId.HasValue)
        {
            filter = Builders<ApprovalTask>.Filter.And(filter, Builders<ApprovalTask>.Filter.Eq(x => x.WorkflowInstanceId, query.InstanceId.Value));
        }

        var total = await _tasks.CountDocumentsAsync(filter, cancellationToken: ct);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 25 : query.PageSize;
        var items = await _tasks.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<ApprovalTask?> GetTaskByIdAsync(Guid taskId, CancellationToken ct = default)
    {
        var filter = Builders<ApprovalTask>.Filter.And(TaskScope,
            Builders<ApprovalTask>.Filter.Eq(x => x.ApprovalTaskId, taskId));
        return await _tasks.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ApprovalTask>> GetTasksByInstanceAsync(Guid instanceId, CancellationToken ct = default)
    {
        var filter = Builders<ApprovalTask>.Filter.And(TaskScope,
            Builders<ApprovalTask>.Filter.Eq(x => x.WorkflowInstanceId, instanceId));
        return await _tasks.Find(filter).SortBy(x => x.StepSequence).ToListAsync(ct);
    }

    public async Task<ApprovalTask> CreateTaskAsync(ApprovalTask entity, CancellationToken ct = default)
    {
        typeof(ApprovalTask).GetProperty(nameof(ApprovalTask.TenantId))?.SetValue(entity, TenantContext.TenantId);
        await _tasks.InsertOneAsync(entity, cancellationToken: ct);
        return entity;
    }

    public async Task<bool> UpdateTaskAsync(ApprovalTask entity, long expectedRowVersion, CancellationToken ct = default)
    {
        entity.RowVersion = expectedRowVersion + 1;
        var filter = Builders<ApprovalTask>.Filter.And(
            Builders<ApprovalTask>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<ApprovalTask>.Filter.Eq(x => x.IsDeleted, false),
            Builders<ApprovalTask>.Filter.Eq(x => x.ApprovalTaskId, entity.ApprovalTaskId),
            Builders<ApprovalTask>.Filter.Eq(x => x.RowVersion, expectedRowVersion));
        var result = await _tasks.ReplaceOneAsync(filter, entity, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}
