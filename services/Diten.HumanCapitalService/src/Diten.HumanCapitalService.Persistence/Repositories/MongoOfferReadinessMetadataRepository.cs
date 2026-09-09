using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoOfferReadinessMetadataRepository : IOfferReadinessMetadataRepository
{
    public const string CollectionName = "hcm_offer_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_offer_management_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_offer_management_tenant_state";

    private readonly IMongoCollection<OfferReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoOfferReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<OfferReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<OfferReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<OfferReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<OfferReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<OfferReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<OfferReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<OfferReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<OfferReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<OfferReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(OfferReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(OfferReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<OfferReadinessMetadata>.Filter.And(
            Builders<OfferReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<OfferReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<OfferReadinessMetadata>(
                Builders<OfferReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<OfferReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<OfferReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<OfferReadinessMetadata>(
                Builders<OfferReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.OfferReadinessState)
                    .Ascending(x => x.OfferWorkflowBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<OfferReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<OfferReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<OfferReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<OfferReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<OfferReadinessMetadata>.Filter.And(
            Builders<OfferReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<OfferReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
