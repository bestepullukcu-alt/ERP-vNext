using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoTepRehireRecommendationReadinessMetadataRepository
    : ITepRehireRecommendationReadinessMetadataRepository
{
    public const string CollectionName = "tep_rehire_recommendation_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_rehire_recommendation_readiness_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_rehire_recommendation_readiness_tenant_state";

    private readonly IMongoCollection<TepRehireRecommendationReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoTepRehireRecommendationReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TepRehireRecommendationReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<TepRehireRecommendationReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<TepRehireRecommendationReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepRehireRecommendationReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<TepRehireRecommendationReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepRehireRecommendationReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepRehireRecommendationReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<TepRehireRecommendationReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<TepRehireRecommendationReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(TepRehireRecommendationReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(TepRehireRecommendationReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepRehireRecommendationReadinessMetadata>.Filter.And(
            Builders<TepRehireRecommendationReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<TepRehireRecommendationReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<TepRehireRecommendationReadinessMetadata>(
                Builders<TepRehireRecommendationReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<TepRehireRecommendationReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<TepRehireRecommendationReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TepRehireRecommendationReadinessMetadata>(
                Builders<TepRehireRecommendationReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.RecommendationReadinessState)
                    .Ascending(x => x.RecommendationPolicyState)
                    .Ascending(x => x.RecommendationEvaluationState)
                    .Ascending(x => x.ConsentPreconditionState)
                    .Ascending(x => x.VisibilityApprovalState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<TepRehireRecommendationReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<TepRehireRecommendationReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepRehireRecommendationReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<TepRehireRecommendationReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<TepRehireRecommendationReadinessMetadata>.Filter.And(
            Builders<TepRehireRecommendationReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TepRehireRecommendationReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
