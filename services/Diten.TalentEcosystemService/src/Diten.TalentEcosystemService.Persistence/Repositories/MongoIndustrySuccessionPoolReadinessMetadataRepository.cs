using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoIndustrySuccessionPoolReadinessMetadataRepository : IIndustrySuccessionPoolReadinessMetadataRepository
{
    public const string CollectionName = "tep_industry_succession_pool_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_industry_succession_pool_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_industry_succession_pool_tenant_state";

    private readonly IMongoCollection<IndustrySuccessionPoolReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoIndustrySuccessionPoolReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<IndustrySuccessionPoolReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<IndustrySuccessionPoolReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<IndustrySuccessionPoolReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<IndustrySuccessionPoolReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<IndustrySuccessionPoolReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<IndustrySuccessionPoolReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<IndustrySuccessionPoolReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<IndustrySuccessionPoolReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(IndustrySuccessionPoolReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(IndustrySuccessionPoolReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<IndustrySuccessionPoolReadinessMetadata>.Filter.And(
            Builders<IndustrySuccessionPoolReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<IndustrySuccessionPoolReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<IndustrySuccessionPoolReadinessMetadata>(
                Builders<IndustrySuccessionPoolReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<IndustrySuccessionPoolReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<IndustrySuccessionPoolReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<IndustrySuccessionPoolReadinessMetadata>(
                Builders<IndustrySuccessionPoolReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IndustrySuccessionPoolReadinessState)
                    .Ascending(x => x.PoolCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<IndustrySuccessionPoolReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<IndustrySuccessionPoolReadinessMetadata>.Filter.And(
            Builders<IndustrySuccessionPoolReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<IndustrySuccessionPoolReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
