using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoTepConsentVisibilityPolicyRepository : ITepConsentVisibilityPolicyRepository
{
    public const string CollectionName = "tep_consent_visibility_policies";
    public const string ActiveCodeUniqueIndexName = "ux_tep_consent_visibility_policies_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_consent_visibility_policies_tenant_state";

    private readonly IMongoCollection<TepConsentVisibilityPolicy> _collection;
    private bool _indexesEnsured;

    public MongoTepConsentVisibilityPolicyRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TepConsentVisibilityPolicy>(CollectionName);
    }

    public async Task<IReadOnlyList<TepConsentVisibilityPolicy>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<TepConsentVisibilityPolicy?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepConsentVisibilityPolicy>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepConsentVisibilityPolicy>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepConsentVisibilityPolicy>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepConsentVisibilityPolicy>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<TepConsentVisibilityPolicy>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(TepConsentVisibilityPolicy policy, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(policy, cancellationToken: ct);
    }

    public async Task UpdateAsync(TepConsentVisibilityPolicy policy, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepConsentVisibilityPolicy>.Filter.And(
            Builders<TepConsentVisibilityPolicy>.Filter.Eq(x => x.TenantId, policy.TenantId),
            Builders<TepConsentVisibilityPolicy>.Filter.Eq(x => x.Id, policy.Id));
        await _collection.ReplaceOneAsync(filter, policy, cancellationToken: ct);
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (_indexesEnsured)
        {
            return;
        }

        await _collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TepConsentVisibilityPolicy>(
                Builders<TepConsentVisibilityPolicy>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<TepConsentVisibilityPolicy>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<TepConsentVisibilityPolicy>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TepConsentVisibilityPolicy>(
                Builders<TepConsentVisibilityPolicy>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PolicyState)
                    .Ascending(x => x.ConsentRequirementState)
                    .Ascending(x => x.VisibilityScope)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<TepConsentVisibilityPolicy> ActiveTenantFilter(Guid tenantId) =>
        Builders<TepConsentVisibilityPolicy>.Filter.And(
            Builders<TepConsentVisibilityPolicy>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TepConsentVisibilityPolicy>.Filter.Eq(x => x.IsDeleted, false));
}
