using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories.TaskChecklistEngine;

public sealed class ChecklistRepository : TenantRepository<ChecklistTemplate>, IChecklistRepository
{
    private readonly IMongoCollection<ChecklistRun> _runs;

    public ChecklistRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "platform_checklist_templates")
    {
        _runs = dbContext.GetCollection<ChecklistRun>("platform_checklist_runs");
    }

    private FilterDefinition<ChecklistTemplate> TemplateScope =>
        Builders<ChecklistTemplate>.Filter.And(
            Builders<ChecklistTemplate>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<ChecklistTemplate>.Filter.Eq(x => x.IsDeleted, false));

    private FilterDefinition<ChecklistRun> RunScope =>
        Builders<ChecklistRun>.Filter.And(
            Builders<ChecklistRun>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<ChecklistRun>.Filter.Eq(x => x.IsDeleted, false));

    public async Task<(IReadOnlyList<ChecklistTemplate> Items, long TotalCount)> QueryTemplatesAsync(ChecklistTemplateListQuery query, CancellationToken ct = default)
    {
        var filter = TemplateScope;

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filter = Builders<ChecklistTemplate>.Filter.And(filter,
                Builders<ChecklistTemplate>.Filter.Regex(x => x.Name, new MongoDB.Bson.BsonRegularExpression(query.Search.Trim(), "i")));
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<ChecklistTemplateStatus>(query.Status.Trim(), true, out var status))
        {
            filter = Builders<ChecklistTemplate>.Filter.And(filter, Builders<ChecklistTemplate>.Filter.Eq(x => x.Status, status));
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

    public async Task<ChecklistTemplate?> GetTemplateByIdAsync(Guid templateId, CancellationToken ct = default)
    {
        var filter = Builders<ChecklistTemplate>.Filter.And(TemplateScope,
            Builders<ChecklistTemplate>.Filter.Eq(x => x.ChecklistTemplateId, templateId));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<ChecklistTemplate?> GetTemplateByCodeAsync(string code, CancellationToken ct = default)
    {
        var filter = Builders<ChecklistTemplate>.Filter.And(TemplateScope,
            Builders<ChecklistTemplate>.Filter.Eq(x => x.Code, code));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public new async Task<ChecklistTemplate> CreateTemplateAsync(ChecklistTemplate entity, CancellationToken ct = default)
        => await base.CreateAsync(entity, ct);

    public async Task<bool> UpdateTemplateAsync(ChecklistTemplate entity, long expectedRowVersion, CancellationToken ct = default)
    {
        entity.RowVersion = expectedRowVersion + 1;
        var filter = Builders<ChecklistTemplate>.Filter.And(
            Builders<ChecklistTemplate>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<ChecklistTemplate>.Filter.Eq(x => x.IsDeleted, false),
            Builders<ChecklistTemplate>.Filter.Eq(x => x.ChecklistTemplateId, entity.ChecklistTemplateId),
            Builders<ChecklistTemplate>.Filter.Eq(x => x.RowVersion, expectedRowVersion));
        var result = await Collection.ReplaceOneAsync(filter, entity, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    public async Task<(IReadOnlyList<ChecklistRun> Items, long TotalCount)> QueryRunsAsync(ChecklistRunListQuery query, CancellationToken ct = default)
    {
        var filter = RunScope;

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filter = Builders<ChecklistRun>.Filter.And(filter,
                Builders<ChecklistRun>.Filter.Regex(x => x.Name, new MongoDB.Bson.BsonRegularExpression(query.Search.Trim(), "i")));
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && Enum.TryParse<ChecklistRunStatus>(query.Status.Trim(), true, out var status))
        {
            filter = Builders<ChecklistRun>.Filter.And(filter, Builders<ChecklistRun>.Filter.Eq(x => x.Status, status));
        }

        var total = await _runs.CountDocumentsAsync(filter, cancellationToken: ct);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 25 : query.PageSize;

        var items = await _runs.Find(filter)
            .SortByDescending(x => x.StartedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<ChecklistRun?> GetRunByIdAsync(Guid runId, CancellationToken ct = default)
    {
        var filter = Builders<ChecklistRun>.Filter.And(RunScope,
            Builders<ChecklistRun>.Filter.Eq(x => x.ChecklistRunId, runId));
        return await _runs.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<ChecklistRun> CreateRunAsync(ChecklistRun entity, CancellationToken ct = default)
    {
        typeof(ChecklistRun).GetProperty(nameof(ChecklistRun.TenantId))?.SetValue(entity, TenantContext.TenantId);
        await _runs.InsertOneAsync(entity, cancellationToken: ct);
        return entity;
    }

    public async Task<bool> UpdateRunAsync(ChecklistRun entity, long expectedRowVersion, CancellationToken ct = default)
    {
        entity.RowVersion = expectedRowVersion + 1;
        var filter = Builders<ChecklistRun>.Filter.And(
            Builders<ChecklistRun>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<ChecklistRun>.Filter.Eq(x => x.IsDeleted, false),
            Builders<ChecklistRun>.Filter.Eq(x => x.ChecklistRunId, entity.ChecklistRunId),
            Builders<ChecklistRun>.Filter.Eq(x => x.RowVersion, expectedRowVersion));
        var result = await _runs.ReplaceOneAsync(filter, entity, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }
}
