using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoHeadcountBudgetReadinessMetadataRepository : IHeadcountBudgetReadinessMetadataRepository
{
    public const string CollectionName = "hcm_headcount_budget_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_headcount_budget_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_headcount_budget_tenant_state";

    private readonly IMongoCollection<HeadcountBudgetReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoHeadcountBudgetReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<HeadcountBudgetReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<HeadcountBudgetReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<HeadcountBudgetReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HeadcountBudgetReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<HeadcountBudgetReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HeadcountBudgetReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<HeadcountBudgetReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<HeadcountBudgetReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(HeadcountBudgetReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(HeadcountBudgetReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HeadcountBudgetReadinessMetadata>.Filter.And(
            Builders<HeadcountBudgetReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<HeadcountBudgetReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<HeadcountBudgetReadinessMetadata>(
                Builders<HeadcountBudgetReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<HeadcountBudgetReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<HeadcountBudgetReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<HeadcountBudgetReadinessMetadata>(
                Builders<HeadcountBudgetReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.HeadcountBudgetReadinessState)
                    .Ascending(x => x.HeadcountRequisitionBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<HeadcountBudgetReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<HeadcountBudgetReadinessMetadata>.Filter.And(
            Builders<HeadcountBudgetReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<HeadcountBudgetReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
