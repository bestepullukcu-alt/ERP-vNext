using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoTepAssociationMembershipRegistryRepository : ITepAssociationMembershipRegistryRepository
{
    public const string CollectionName = "tep_association_membership_registry";
    public const string ActiveCodeUniqueIndexName = "ux_tep_association_memberships_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_association_memberships_tenant_state";

    private readonly IMongoCollection<TepAssociationMembershipRegistry> _collection;
    private bool _indexesEnsured;

    public MongoTepAssociationMembershipRegistryRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TepAssociationMembershipRegistry>(CollectionName);
    }

    public async Task<IReadOnlyList<TepAssociationMembershipRegistry>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<TepAssociationMembershipRegistry?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepAssociationMembershipRegistry>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<TepAssociationMembershipRegistry>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepAssociationMembershipRegistry>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepAssociationMembershipRegistry>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<TepAssociationMembershipRegistry>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<TepAssociationMembershipRegistry>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(TepAssociationMembershipRegistry registry, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(registry, cancellationToken: ct);
    }

    public async Task UpdateAsync(TepAssociationMembershipRegistry registry, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepAssociationMembershipRegistry>.Filter.And(
            Builders<TepAssociationMembershipRegistry>.Filter.Eq(x => x.TenantId, registry.TenantId),
            Builders<TepAssociationMembershipRegistry>.Filter.Eq(x => x.Id, registry.Id));
        await _collection.ReplaceOneAsync(filter, registry, cancellationToken: ct);
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (_indexesEnsured)
        {
            return;
        }

        await _collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TepAssociationMembershipRegistry>(
                Builders<TepAssociationMembershipRegistry>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<TepAssociationMembershipRegistry>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<TepAssociationMembershipRegistry>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TepAssociationMembershipRegistry>(
                Builders<TepAssociationMembershipRegistry>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.AssociationMembershipState)
                    .Ascending(x => x.MemberCompanyState)
                    .Ascending(x => x.PolicyEvaluationState)
                    .Ascending(x => x.AssociationActivationState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<TepAssociationMembershipRegistry> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<TepAssociationMembershipRegistry>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepAssociationMembershipRegistry>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<TepAssociationMembershipRegistry> ActiveTenantFilter(Guid tenantId) =>
        Builders<TepAssociationMembershipRegistry>.Filter.And(
            Builders<TepAssociationMembershipRegistry>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TepAssociationMembershipRegistry>.Filter.Eq(x => x.IsDeleted, false));
}
