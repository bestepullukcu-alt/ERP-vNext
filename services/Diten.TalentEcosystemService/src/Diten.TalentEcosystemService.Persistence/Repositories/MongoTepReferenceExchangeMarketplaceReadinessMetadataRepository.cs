using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoTepReferenceExchangeMarketplaceReadinessMetadataRepository
    : ITepReferenceExchangeMarketplaceReadinessMetadataRepository
{
    public const string CollectionName = "tep_reference_exchange_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_reference_exchange_readiness_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_reference_exchange_readiness_tenant_state";

    private readonly IMongoCollection<TepReferenceExchangeMarketplaceReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoTepReferenceExchangeMarketplaceReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TepReferenceExchangeMarketplaceReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<TepReferenceExchangeMarketplaceReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<TepReferenceExchangeMarketplaceReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepReferenceExchangeMarketplaceReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepReferenceExchangeMarketplaceReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepReferenceExchangeMarketplaceReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepReferenceExchangeMarketplaceReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<TepReferenceExchangeMarketplaceReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(TepReferenceExchangeMarketplaceReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(TepReferenceExchangeMarketplaceReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepReferenceExchangeMarketplaceReadinessMetadata>.Filter.And(
            Builders<TepReferenceExchangeMarketplaceReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<TepReferenceExchangeMarketplaceReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<TepReferenceExchangeMarketplaceReadinessMetadata>(
                Builders<TepReferenceExchangeMarketplaceReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<TepReferenceExchangeMarketplaceReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<TepReferenceExchangeMarketplaceReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TepReferenceExchangeMarketplaceReadinessMetadata>(
                Builders<TepReferenceExchangeMarketplaceReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ExchangeReadinessState)
                    .Ascending(x => x.ExchangeAvailabilityState)
                    .Ascending(x => x.ParticipantEligibilityState)
                    .Ascending(x => x.ConsentPreconditionState)
                    .Ascending(x => x.VisibilityApprovalState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<TepReferenceExchangeMarketplaceReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<TepReferenceExchangeMarketplaceReadinessMetadata>.Filter.And(
            Builders<TepReferenceExchangeMarketplaceReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TepReferenceExchangeMarketplaceReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
