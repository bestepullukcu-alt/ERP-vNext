using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoTepTrustLevelPolicyMetadataRepository : ITepTrustLevelPolicyMetadataRepository
{
    public const string CollectionName = "tep_trust_level_policies";
    public const string ActiveCodeUniqueIndexName = "ux_tep_trust_level_policies_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_trust_level_policies_tenant_state";

    private readonly IMongoCollection<TepTrustLevelPolicyMetadata> _collection;
    private bool _indexesEnsured;

    public MongoTepTrustLevelPolicyMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TepTrustLevelPolicyMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<TepTrustLevelPolicyMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<TepTrustLevelPolicyMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepTrustLevelPolicyMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<TepTrustLevelPolicyMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepTrustLevelPolicyMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepTrustLevelPolicyMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<TepTrustLevelPolicyMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<TepTrustLevelPolicyMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(TepTrustLevelPolicyMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(TepTrustLevelPolicyMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepTrustLevelPolicyMetadata>.Filter.And(
            Builders<TepTrustLevelPolicyMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<TepTrustLevelPolicyMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<TepTrustLevelPolicyMetadata>(
                Builders<TepTrustLevelPolicyMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<TepTrustLevelPolicyMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<TepTrustLevelPolicyMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TepTrustLevelPolicyMetadata>(
                Builders<TepTrustLevelPolicyMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.TrustLevelPolicyState)
                    .Ascending(x => x.TrustValidationState)
                    .Ascending(x => x.MultiSignatureRequirementState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<TepTrustLevelPolicyMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<TepTrustLevelPolicyMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepTrustLevelPolicyMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<TepTrustLevelPolicyMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<TepTrustLevelPolicyMetadata>.Filter.And(
            Builders<TepTrustLevelPolicyMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TepTrustLevelPolicyMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
