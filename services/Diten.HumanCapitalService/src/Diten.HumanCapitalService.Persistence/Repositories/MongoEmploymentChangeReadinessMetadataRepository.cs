using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoEmploymentChangeReadinessMetadataRepository : IEmploymentChangeReadinessMetadataRepository
{
    public const string CollectionName = "hcm_employment_change_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_employment_change_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_employment_change_tenant_state";

    private readonly IMongoCollection<EmploymentChangeReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoEmploymentChangeReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<EmploymentChangeReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<EmploymentChangeReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<EmploymentChangeReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EmploymentChangeReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<EmploymentChangeReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EmploymentChangeReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<EmploymentChangeReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<EmploymentChangeReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<EmploymentChangeReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(EmploymentChangeReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(EmploymentChangeReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EmploymentChangeReadinessMetadata>.Filter.And(
            Builders<EmploymentChangeReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<EmploymentChangeReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<EmploymentChangeReadinessMetadata>(
                Builders<EmploymentChangeReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<EmploymentChangeReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<EmploymentChangeReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<EmploymentChangeReadinessMetadata>(
                Builders<EmploymentChangeReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.EmploymentChangeReadinessState)
                    .Ascending(x => x.ChangeLifecycleBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<EmploymentChangeReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<EmploymentChangeReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<EmploymentChangeReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<EmploymentChangeReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<EmploymentChangeReadinessMetadata>.Filter.And(
            Builders<EmploymentChangeReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<EmploymentChangeReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
