using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoSectorTalentTrendsReadinessMetadataRepository : ISectorTalentTrendsReadinessMetadataRepository
{
    public const string CollectionName = "tep_sector_talent_trends_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_sector_talent_trends_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_sector_talent_trends_tenant_state";

    private readonly IMongoCollection<SectorTalentTrendsReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoSectorTalentTrendsReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SectorTalentTrendsReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<SectorTalentTrendsReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<SectorTalentTrendsReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<SectorTalentTrendsReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<SectorTalentTrendsReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<SectorTalentTrendsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<SectorTalentTrendsReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<SectorTalentTrendsReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<SectorTalentTrendsReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(SectorTalentTrendsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(SectorTalentTrendsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<SectorTalentTrendsReadinessMetadata>.Filter.And(
            Builders<SectorTalentTrendsReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<SectorTalentTrendsReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<SectorTalentTrendsReadinessMetadata>(
                Builders<SectorTalentTrendsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<SectorTalentTrendsReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<SectorTalentTrendsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<SectorTalentTrendsReadinessMetadata>(
                Builders<SectorTalentTrendsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SectorTalentTrendsReadinessState)
                    .Ascending(x => x.TrendCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<SectorTalentTrendsReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<SectorTalentTrendsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<SectorTalentTrendsReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<SectorTalentTrendsReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<SectorTalentTrendsReadinessMetadata>.Filter.And(
            Builders<SectorTalentTrendsReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<SectorTalentTrendsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
