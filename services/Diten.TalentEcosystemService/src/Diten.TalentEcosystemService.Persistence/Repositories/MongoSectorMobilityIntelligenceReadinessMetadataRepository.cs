using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoSectorMobilityIntelligenceReadinessMetadataRepository : ISectorMobilityIntelligenceReadinessMetadataRepository
{
    public const string CollectionName = "tep_sector_mobility_intelligence_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_sector_mobility_intelligence_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_sector_mobility_intelligence_tenant_state";

    private readonly IMongoCollection<SectorMobilityIntelligenceReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoSectorMobilityIntelligenceReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SectorMobilityIntelligenceReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<SectorMobilityIntelligenceReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<SectorMobilityIntelligenceReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<SectorMobilityIntelligenceReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<SectorMobilityIntelligenceReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<SectorMobilityIntelligenceReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<SectorMobilityIntelligenceReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<SectorMobilityIntelligenceReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(SectorMobilityIntelligenceReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(SectorMobilityIntelligenceReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<SectorMobilityIntelligenceReadinessMetadata>.Filter.And(
            Builders<SectorMobilityIntelligenceReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<SectorMobilityIntelligenceReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<SectorMobilityIntelligenceReadinessMetadata>(
                Builders<SectorMobilityIntelligenceReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<SectorMobilityIntelligenceReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<SectorMobilityIntelligenceReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<SectorMobilityIntelligenceReadinessMetadata>(
                Builders<SectorMobilityIntelligenceReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SectorMobilityIntelligenceReadinessState)
                    .Ascending(x => x.MobilityCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<SectorMobilityIntelligenceReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<SectorMobilityIntelligenceReadinessMetadata>.Filter.And(
            Builders<SectorMobilityIntelligenceReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<SectorMobilityIntelligenceReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
