using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoIndustryTalentPoolReadinessMetadataRepository : IIndustryTalentPoolReadinessMetadataRepository
{
    public const string CollectionName = "tep_industry_talent_pool_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_tep_industry_talent_pool_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_industry_talent_pool_tenant_state";

    private readonly IMongoCollection<IndustryTalentPoolReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoIndustryTalentPoolReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<IndustryTalentPoolReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<IndustryTalentPoolReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<IndustryTalentPoolReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<IndustryTalentPoolReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<IndustryTalentPoolReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<IndustryTalentPoolReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<IndustryTalentPoolReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<IndustryTalentPoolReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<IndustryTalentPoolReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(IndustryTalentPoolReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(IndustryTalentPoolReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<IndustryTalentPoolReadinessMetadata>.Filter.And(
            Builders<IndustryTalentPoolReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<IndustryTalentPoolReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<IndustryTalentPoolReadinessMetadata>(
                Builders<IndustryTalentPoolReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<IndustryTalentPoolReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<IndustryTalentPoolReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<IndustryTalentPoolReadinessMetadata>(
                Builders<IndustryTalentPoolReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IndustryTalentPoolReadinessState)
                    .Ascending(x => x.PoolMembershipCatalogBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<IndustryTalentPoolReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<IndustryTalentPoolReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<IndustryTalentPoolReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<IndustryTalentPoolReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<IndustryTalentPoolReadinessMetadata>.Filter.And(
            Builders<IndustryTalentPoolReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<IndustryTalentPoolReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
