using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoSkillsGapHeatmapReadinessMetadataRepository : ISkillsGapHeatmapReadinessMetadataRepository
{
    public const string CollectionName = "tep_skills_gap_heatmap_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_skills_gap_heatmap_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_skills_gap_heatmap_tenant_state";

    private readonly IMongoCollection<SkillsGapHeatmapReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoSkillsGapHeatmapReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SkillsGapHeatmapReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<SkillsGapHeatmapReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<SkillsGapHeatmapReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<SkillsGapHeatmapReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<SkillsGapHeatmapReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<SkillsGapHeatmapReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<SkillsGapHeatmapReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<SkillsGapHeatmapReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<SkillsGapHeatmapReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(SkillsGapHeatmapReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(SkillsGapHeatmapReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<SkillsGapHeatmapReadinessMetadata>.Filter.And(
            Builders<SkillsGapHeatmapReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<SkillsGapHeatmapReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<SkillsGapHeatmapReadinessMetadata>(
                Builders<SkillsGapHeatmapReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<SkillsGapHeatmapReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<SkillsGapHeatmapReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<SkillsGapHeatmapReadinessMetadata>(
                Builders<SkillsGapHeatmapReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SkillsGapHeatmapReadinessState)
                    .Ascending(x => x.GapCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<SkillsGapHeatmapReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<SkillsGapHeatmapReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<SkillsGapHeatmapReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<SkillsGapHeatmapReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<SkillsGapHeatmapReadinessMetadata>.Filter.And(
            Builders<SkillsGapHeatmapReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<SkillsGapHeatmapReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
