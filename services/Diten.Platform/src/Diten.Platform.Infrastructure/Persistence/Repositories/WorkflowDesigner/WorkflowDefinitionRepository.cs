using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories.WorkflowDesigner;

public sealed class WorkflowDefinitionRepository : TenantRepository<WorkflowDefinition>, IWorkflowDefinitionRepository
{
    public WorkflowDefinitionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "platform_workflow_definitions")
    {
    }

    private FilterDefinition<WorkflowDefinition> Scope =>
        Builders<WorkflowDefinition>.Filter.And(
            Builders<WorkflowDefinition>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<WorkflowDefinition>.Filter.Eq(x => x.IsDeleted, false));

    public async Task<(IReadOnlyList<WorkflowDefinition> Items, long TotalCount)> QueryAsync(WorkflowDefinitionListQuery query, CancellationToken ct = default)
    {
        var filter = Scope;
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filter = Builders<WorkflowDefinition>.Filter.And(filter,
                Builders<WorkflowDefinition>.Filter.Regex(x => x.Name, new MongoDB.Bson.BsonRegularExpression(query.Search.Trim(), "i")));
        }
        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<WorkflowDefinitionStatus>(query.Status.Trim(), true, out var status))
        {
            filter = Builders<WorkflowDefinition>.Filter.And(filter, Builders<WorkflowDefinition>.Filter.Eq(x => x.Status, status));
        }

        var total = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 25 : query.PageSize;
        var items = await Collection.Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<WorkflowDefinition?> GetByIdAsync(Guid definitionId, CancellationToken ct = default)
    {
        var filter = Builders<WorkflowDefinition>.Filter.And(Scope,
            Builders<WorkflowDefinition>.Filter.Eq(x => x.WorkflowDefinitionId, definitionId));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<WorkflowDefinition?> GetByCodeVersionAsync(string code, int versionNumber, CancellationToken ct = default)
    {
        var filter = Builders<WorkflowDefinition>.Filter.And(Scope,
            Builders<WorkflowDefinition>.Filter.Eq(x => x.Code, code),
            Builders<WorkflowDefinition>.Filter.Eq(x => x.VersionNumber, versionNumber));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetLatestVersionNumberAsync(string code, CancellationToken ct = default)
    {
        var filter = Builders<WorkflowDefinition>.Filter.And(Scope,
            Builders<WorkflowDefinition>.Filter.Eq(x => x.Code, code));
        var latest = await Collection.Find(filter).SortByDescending(x => x.VersionNumber).Limit(1).FirstOrDefaultAsync(ct);
        return latest?.VersionNumber ?? 0;
    }

    public async Task<WorkflowDefinition?> GetPublishedByCodeAsync(string code, CancellationToken ct = default)
    {
        var filter = Builders<WorkflowDefinition>.Filter.And(Scope,
            Builders<WorkflowDefinition>.Filter.Eq(x => x.Code, code),
            Builders<WorkflowDefinition>.Filter.Eq(x => x.Status, WorkflowDefinitionStatus.Published));
        return await Collection.Find(filter).SortByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
    }

    public new async Task<WorkflowDefinition> CreateAsync(WorkflowDefinition entity, CancellationToken ct = default)
        => await base.CreateAsync(entity, ct);

    public async Task<bool> UpdateAsync(WorkflowDefinition entity, long expectedRowVersion, CancellationToken ct = default)
    {
        entity.RowVersion = expectedRowVersion + 1;
        var filter = Builders<WorkflowDefinition>.Filter.And(
            Builders<WorkflowDefinition>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<WorkflowDefinition>.Filter.Eq(x => x.IsDeleted, false),
            Builders<WorkflowDefinition>.Filter.Eq(x => x.WorkflowDefinitionId, entity.WorkflowDefinitionId),
            Builders<WorkflowDefinition>.Filter.Eq(x => x.RowVersion, expectedRowVersion));
        var result = await Collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}
