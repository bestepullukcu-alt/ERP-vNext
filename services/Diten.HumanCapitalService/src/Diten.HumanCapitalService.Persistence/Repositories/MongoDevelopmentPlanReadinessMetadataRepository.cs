using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoDevelopmentPlanReadinessMetadataRepository : IDevelopmentPlanReadinessMetadataRepository
{
    public const string CollectionName = "hcm_development_plans_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_development_plans_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_development_plans_tenant_state";

    private readonly IMongoCollection<DevelopmentPlanReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoDevelopmentPlanReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<DevelopmentPlanReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<DevelopmentPlanReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<DevelopmentPlanReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<DevelopmentPlanReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<DevelopmentPlanReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<DevelopmentPlanReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<DevelopmentPlanReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<DevelopmentPlanReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<DevelopmentPlanReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(DevelopmentPlanReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(DevelopmentPlanReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<DevelopmentPlanReadinessMetadata>.Filter.And(
            Builders<DevelopmentPlanReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<DevelopmentPlanReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<DevelopmentPlanReadinessMetadata>(
                Builders<DevelopmentPlanReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<DevelopmentPlanReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<DevelopmentPlanReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DevelopmentPlanReadinessMetadata>(
                Builders<DevelopmentPlanReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.DevelopmentPlanReadinessState)
                    .Ascending(x => x.DevelopmentPlanWorkflowBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<DevelopmentPlanReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<DevelopmentPlanReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<DevelopmentPlanReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<DevelopmentPlanReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<DevelopmentPlanReadinessMetadata>.Filter.And(
            Builders<DevelopmentPlanReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<DevelopmentPlanReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
