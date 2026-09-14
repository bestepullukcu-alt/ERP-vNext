using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoHiringRiskIndicatorsReadinessMetadataRepository : IHiringRiskIndicatorsReadinessMetadataRepository
{
    public const string CollectionName = "tep_hiring_risk_indicators_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_hiring_risk_indicators_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_hiring_risk_indicators_tenant_state";

    private readonly IMongoCollection<HiringRiskIndicatorsReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoHiringRiskIndicatorsReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<HiringRiskIndicatorsReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<HiringRiskIndicatorsReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<HiringRiskIndicatorsReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HiringRiskIndicatorsReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<HiringRiskIndicatorsReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HiringRiskIndicatorsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<HiringRiskIndicatorsReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<HiringRiskIndicatorsReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<HiringRiskIndicatorsReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(HiringRiskIndicatorsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(HiringRiskIndicatorsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<HiringRiskIndicatorsReadinessMetadata>.Filter.And(
            Builders<HiringRiskIndicatorsReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<HiringRiskIndicatorsReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<HiringRiskIndicatorsReadinessMetadata>(
                Builders<HiringRiskIndicatorsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<HiringRiskIndicatorsReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<HiringRiskIndicatorsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<HiringRiskIndicatorsReadinessMetadata>(
                Builders<HiringRiskIndicatorsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.HiringRiskIndicatorsReadinessState)
                    .Ascending(x => x.RiskIndicatorCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<HiringRiskIndicatorsReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<HiringRiskIndicatorsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<HiringRiskIndicatorsReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<HiringRiskIndicatorsReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<HiringRiskIndicatorsReadinessMetadata>.Filter.And(
            Builders<HiringRiskIndicatorsReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<HiringRiskIndicatorsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
