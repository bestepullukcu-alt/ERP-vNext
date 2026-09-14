using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoPerformanceReviewReadinessMetadataRepository : IPerformanceReviewReadinessMetadataRepository
{
    public const string CollectionName = "hcm_performance_review_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_performance_review_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_performance_review_tenant_state";

    private readonly IMongoCollection<PerformanceReviewReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoPerformanceReviewReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<PerformanceReviewReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<PerformanceReviewReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<PerformanceReviewReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<PerformanceReviewReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<PerformanceReviewReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<PerformanceReviewReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<PerformanceReviewReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<PerformanceReviewReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<PerformanceReviewReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(PerformanceReviewReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(PerformanceReviewReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<PerformanceReviewReadinessMetadata>.Filter.And(
            Builders<PerformanceReviewReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<PerformanceReviewReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<PerformanceReviewReadinessMetadata>(
                Builders<PerformanceReviewReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<PerformanceReviewReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<PerformanceReviewReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<PerformanceReviewReadinessMetadata>(
                Builders<PerformanceReviewReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PerformanceReviewReadinessState)
                    .Ascending(x => x.ReviewCycleBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<PerformanceReviewReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<PerformanceReviewReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<PerformanceReviewReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<PerformanceReviewReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<PerformanceReviewReadinessMetadata>.Filter.And(
            Builders<PerformanceReviewReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<PerformanceReviewReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
