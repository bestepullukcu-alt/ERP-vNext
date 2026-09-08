using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories.TaskChecklistEngine;

public sealed class WorkTaskRepository : TenantRepository<WorkTask>, IWorkTaskRepository
{
    private readonly IMongoCollection<TaskAssignment> _assignments;

    public WorkTaskRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "platform_tasks")
    {
        _assignments = dbContext.GetCollection<TaskAssignment>("platform_task_assignments");
    }

    private FilterDefinition<WorkTask> TenantScope =>
        Builders<WorkTask>.Filter.And(
            Builders<WorkTask>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<WorkTask>.Filter.Eq(x => x.IsDeleted, false));

    public async Task<(IReadOnlyList<WorkTask> Items, long TotalCount)> QueryAsync(WorkTaskListQuery query, CancellationToken ct = default)
    {
        var filter = TenantScope;

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filter = Builders<WorkTask>.Filter.And(filter,
                Builders<WorkTask>.Filter.Regex(x => x.Title, new MongoDB.Bson.BsonRegularExpression(query.Search.Trim(), "i")));
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<WorkTaskStatus>(query.Status.Trim(), true, out var status))
        {
            filter = Builders<WorkTask>.Filter.And(filter, Builders<WorkTask>.Filter.Eq(x => x.Status, status));
        }

        if (!string.IsNullOrWhiteSpace(query.AssigneeId))
        {
            filter = Builders<WorkTask>.Filter.And(filter, Builders<WorkTask>.Filter.Eq(x => x.AssigneeId, query.AssigneeId.Trim()));
        }

        var total = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 25 : query.PageSize;

        var items = await Collection.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<WorkTask?> GetByIdAsync(Guid workTaskId, CancellationToken ct = default)
    {
        var filter = Builders<WorkTask>.Filter.And(TenantScope,
            Builders<WorkTask>.Filter.Eq(x => x.WorkTaskId, workTaskId));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public new async Task<WorkTask> CreateAsync(WorkTask entity, CancellationToken ct = default)
        => await base.CreateAsync(entity, ct);

    public async Task<bool> UpdateAsync(WorkTask entity, long expectedRowVersion, CancellationToken ct = default)
    {
        entity.RowVersion = expectedRowVersion + 1;
        var filter = Builders<WorkTask>.Filter.And(
            Builders<WorkTask>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<WorkTask>.Filter.Eq(x => x.IsDeleted, false),
            Builders<WorkTask>.Filter.Eq(x => x.WorkTaskId, entity.WorkTaskId),
            Builders<WorkTask>.Filter.Eq(x => x.RowVersion, expectedRowVersion));

        var result = await Collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    public async Task CreateAssignmentAsync(TaskAssignment assignment, CancellationToken ct = default)
    {
        // TenantId is an init-only required member on TenantScopedEntity; stamp it from context
        // via reflection, mirroring TenantRepository&lt;T&gt;.CreateAsync.
        typeof(TaskAssignment).GetProperty(nameof(TaskAssignment.TenantId))?.SetValue(assignment, TenantContext.TenantId);
        await _assignments.InsertOneAsync(assignment, cancellationToken: ct);
    }

    public async Task DeactivateAssignmentsAsync(Guid workTaskId, CancellationToken ct = default)
    {
        var filter = Builders<TaskAssignment>.Filter.And(
            Builders<TaskAssignment>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<TaskAssignment>.Filter.Eq(x => x.WorkTaskId, workTaskId),
            Builders<TaskAssignment>.Filter.Eq(x => x.IsActive, true));
        var update = Builders<TaskAssignment>.Update
            .Set(x => x.IsActive, false)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);
        await _assignments.UpdateManyAsync(filter, update, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<TaskAssignment>> GetAssignmentsByTaskAsync(Guid workTaskId, CancellationToken ct = default)
    {
        var filter = Builders<TaskAssignment>.Filter.And(
            Builders<TaskAssignment>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<TaskAssignment>.Filter.Eq(x => x.IsDeleted, false),
            Builders<TaskAssignment>.Filter.Eq(x => x.WorkTaskId, workTaskId));
        return await _assignments.Find(filter).SortByDescending(x => x.AssignedAt).ToListAsync(ct);
    }
}
