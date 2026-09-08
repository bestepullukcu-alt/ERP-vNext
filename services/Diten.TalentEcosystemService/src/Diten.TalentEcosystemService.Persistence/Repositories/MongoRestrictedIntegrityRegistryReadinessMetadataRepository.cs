using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoRestrictedIntegrityRegistryReadinessMetadataRepository : IRestrictedIntegrityRegistryReadinessMetadataRepository
{
    public const string CollectionName = "tep_restricted_integrity_registry_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_restricted_integrity_registry_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_restricted_integrity_registry_tenant_state";

    private readonly IMongoCollection<RestrictedIntegrityRegistryReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoRestrictedIntegrityRegistryReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<RestrictedIntegrityRegistryReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<RestrictedIntegrityRegistryReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<RestrictedIntegrityRegistryReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<RestrictedIntegrityRegistryReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<RestrictedIntegrityRegistryReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<RestrictedIntegrityRegistryReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<RestrictedIntegrityRegistryReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<RestrictedIntegrityRegistryReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(RestrictedIntegrityRegistryReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(RestrictedIntegrityRegistryReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<RestrictedIntegrityRegistryReadinessMetadata>.Filter.And(
            Builders<RestrictedIntegrityRegistryReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<RestrictedIntegrityRegistryReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<RestrictedIntegrityRegistryReadinessMetadata>(
                Builders<RestrictedIntegrityRegistryReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<RestrictedIntegrityRegistryReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<RestrictedIntegrityRegistryReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<RestrictedIntegrityRegistryReadinessMetadata>(
                Builders<RestrictedIntegrityRegistryReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.RestrictedIntegrityRegistryReadinessState)
                    .Ascending(x => x.IntegrityCaseCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<RestrictedIntegrityRegistryReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<RestrictedIntegrityRegistryReadinessMetadata>.Filter.And(
            Builders<RestrictedIntegrityRegistryReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<RestrictedIntegrityRegistryReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
