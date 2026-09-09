using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoSelfServiceReadinessMetadataRepository : ISelfServiceReadinessMetadataRepository
{
    public const string CollectionName = "hcm_self_service_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_self_service_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_self_service_tenant_state";

    private readonly IMongoCollection<SelfServiceReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoSelfServiceReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SelfServiceReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<SelfServiceReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<SelfServiceReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<SelfServiceReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<SelfServiceReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<SelfServiceReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<SelfServiceReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<SelfServiceReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<SelfServiceReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(SelfServiceReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(SelfServiceReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<SelfServiceReadinessMetadata>.Filter.And(
            Builders<SelfServiceReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<SelfServiceReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<SelfServiceReadinessMetadata>(
                Builders<SelfServiceReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<SelfServiceReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<SelfServiceReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<SelfServiceReadinessMetadata>(
                Builders<SelfServiceReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SelfServiceReadinessState)
                    .Ascending(x => x.RequestIntakeBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<SelfServiceReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<SelfServiceReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<SelfServiceReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<SelfServiceReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<SelfServiceReadinessMetadata>.Filter.And(
            Builders<SelfServiceReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<SelfServiceReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
