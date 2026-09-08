using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoTalentDataFoundationReadinessMetadataRepository : ITalentDataFoundationReadinessMetadataRepository
{
    public const string CollectionName = "tep_talent_data_foundation_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_talent_data_foundation_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_talent_data_foundation_tenant_state";

    private readonly IMongoCollection<TalentDataFoundationReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoTalentDataFoundationReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TalentDataFoundationReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<TalentDataFoundationReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<TalentDataFoundationReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TalentDataFoundationReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TalentDataFoundationReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TalentDataFoundationReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TalentDataFoundationReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<TalentDataFoundationReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(TalentDataFoundationReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(TalentDataFoundationReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TalentDataFoundationReadinessMetadata>.Filter.And(
            Builders<TalentDataFoundationReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<TalentDataFoundationReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<TalentDataFoundationReadinessMetadata>(
                Builders<TalentDataFoundationReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<TalentDataFoundationReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<TalentDataFoundationReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TalentDataFoundationReadinessMetadata>(
                Builders<TalentDataFoundationReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.TalentDataFoundationReadinessState)
                    .Ascending(x => x.TalentEntityCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<TalentDataFoundationReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<TalentDataFoundationReadinessMetadata>.Filter.And(
            Builders<TalentDataFoundationReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TalentDataFoundationReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
