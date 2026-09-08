using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoWorkforcePlanningReadinessMetadataRepository : IWorkforcePlanningReadinessMetadataRepository
{
    public const string CollectionName = "hcm_workforce_planning_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_workforce_planning_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_workforce_planning_tenant_state";

    private readonly IMongoCollection<WorkforcePlanningReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoWorkforcePlanningReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<WorkforcePlanningReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<WorkforcePlanningReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<WorkforcePlanningReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<WorkforcePlanningReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<WorkforcePlanningReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<WorkforcePlanningReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<WorkforcePlanningReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<WorkforcePlanningReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(WorkforcePlanningReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(WorkforcePlanningReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<WorkforcePlanningReadinessMetadata>.Filter.And(
            Builders<WorkforcePlanningReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<WorkforcePlanningReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
        await _collection.ReplaceOneAsync(filter, metadata, cancellationToken: ct);
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (_indexesEnsured)
        {
            return;
        }

        await _collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<WorkforcePlanningReadinessMetadata>(
                Builders<WorkforcePlanningReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<WorkforcePlanningReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<WorkforcePlanningReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<WorkforcePlanningReadinessMetadata>(
                Builders<WorkforcePlanningReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.WorkforcePlanningReadinessState)
                    .Ascending(x => x.HeadcountPlanBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<WorkforcePlanningReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<WorkforcePlanningReadinessMetadata>.Filter.And(
            Builders<WorkforcePlanningReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<WorkforcePlanningReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
