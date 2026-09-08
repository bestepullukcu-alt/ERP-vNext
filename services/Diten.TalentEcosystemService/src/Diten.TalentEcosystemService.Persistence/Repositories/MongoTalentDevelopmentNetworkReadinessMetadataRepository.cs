using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoTalentDevelopmentNetworkReadinessMetadataRepository : ITalentDevelopmentNetworkReadinessMetadataRepository
{
    public const string CollectionName = "tep_talent_development_network_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_talent_development_network_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_talent_development_network_tenant_state";

    private readonly IMongoCollection<TalentDevelopmentNetworkReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoTalentDevelopmentNetworkReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TalentDevelopmentNetworkReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<TalentDevelopmentNetworkReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<TalentDevelopmentNetworkReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TalentDevelopmentNetworkReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TalentDevelopmentNetworkReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TalentDevelopmentNetworkReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TalentDevelopmentNetworkReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<TalentDevelopmentNetworkReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(TalentDevelopmentNetworkReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(TalentDevelopmentNetworkReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TalentDevelopmentNetworkReadinessMetadata>.Filter.And(
            Builders<TalentDevelopmentNetworkReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<TalentDevelopmentNetworkReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<TalentDevelopmentNetworkReadinessMetadata>(
                Builders<TalentDevelopmentNetworkReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<TalentDevelopmentNetworkReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<TalentDevelopmentNetworkReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TalentDevelopmentNetworkReadinessMetadata>(
                Builders<TalentDevelopmentNetworkReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.TalentDevelopmentNetworkReadinessState)
                    .Ascending(x => x.PathwayCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<TalentDevelopmentNetworkReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<TalentDevelopmentNetworkReadinessMetadata>.Filter.And(
            Builders<TalentDevelopmentNetworkReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TalentDevelopmentNetworkReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
