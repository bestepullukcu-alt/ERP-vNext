using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoSuccessionReadinessMetadataRepository : ISuccessionReadinessMetadataRepository
{
    public const string CollectionName = "hcm_succession_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_succession_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_succession_tenant_state";

    private readonly IMongoCollection<SuccessionReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoSuccessionReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<SuccessionReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<SuccessionReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<SuccessionReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<SuccessionReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<SuccessionReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<SuccessionReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<SuccessionReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<SuccessionReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<SuccessionReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(SuccessionReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(SuccessionReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<SuccessionReadinessMetadata>.Filter.And(
            Builders<SuccessionReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<SuccessionReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<SuccessionReadinessMetadata>(
                Builders<SuccessionReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<SuccessionReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<SuccessionReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<SuccessionReadinessMetadata>(
                Builders<SuccessionReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.SuccessionReadinessState)
                    .Ascending(x => x.SuccessionPoolBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<SuccessionReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<SuccessionReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<SuccessionReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<SuccessionReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<SuccessionReadinessMetadata>.Filter.And(
            Builders<SuccessionReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<SuccessionReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
