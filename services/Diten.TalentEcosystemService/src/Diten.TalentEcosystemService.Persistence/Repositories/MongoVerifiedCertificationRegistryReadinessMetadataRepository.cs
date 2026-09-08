using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoVerifiedCertificationRegistryReadinessMetadataRepository : IVerifiedCertificationRegistryReadinessMetadataRepository
{
    public const string CollectionName = "tep_verified_certification_registry_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_verified_certification_registry_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_verified_certification_registry_tenant_state";

    private readonly IMongoCollection<VerifiedCertificationRegistryReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoVerifiedCertificationRegistryReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<VerifiedCertificationRegistryReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<VerifiedCertificationRegistryReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<VerifiedCertificationRegistryReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<VerifiedCertificationRegistryReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<VerifiedCertificationRegistryReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<VerifiedCertificationRegistryReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<VerifiedCertificationRegistryReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<VerifiedCertificationRegistryReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(VerifiedCertificationRegistryReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(VerifiedCertificationRegistryReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<VerifiedCertificationRegistryReadinessMetadata>.Filter.And(
            Builders<VerifiedCertificationRegistryReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<VerifiedCertificationRegistryReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<VerifiedCertificationRegistryReadinessMetadata>(
                Builders<VerifiedCertificationRegistryReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<VerifiedCertificationRegistryReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<VerifiedCertificationRegistryReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<VerifiedCertificationRegistryReadinessMetadata>(
                Builders<VerifiedCertificationRegistryReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.VerifiedCertificationRegistryReadinessState)
                    .Ascending(x => x.CertificationCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<VerifiedCertificationRegistryReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<VerifiedCertificationRegistryReadinessMetadata>.Filter.And(
            Builders<VerifiedCertificationRegistryReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<VerifiedCertificationRegistryReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
