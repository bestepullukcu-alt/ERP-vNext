using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoEmployeeOnboardingReadinessMetadataRepository : IEmployeeOnboardingReadinessMetadataRepository
{
    public const string CollectionName = "hcm_employee_onboarding_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_employee_onboarding_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_employee_onboarding_tenant_state";

    private readonly IMongoCollection<EmployeeOnboardingReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoEmployeeOnboardingReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<EmployeeOnboardingReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<EmployeeOnboardingReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<EmployeeOnboardingReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EmployeeOnboardingReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<EmployeeOnboardingReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EmployeeOnboardingReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<EmployeeOnboardingReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<EmployeeOnboardingReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<EmployeeOnboardingReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(EmployeeOnboardingReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(EmployeeOnboardingReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EmployeeOnboardingReadinessMetadata>.Filter.And(
            Builders<EmployeeOnboardingReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<EmployeeOnboardingReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<EmployeeOnboardingReadinessMetadata>(
                Builders<EmployeeOnboardingReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<EmployeeOnboardingReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<EmployeeOnboardingReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<EmployeeOnboardingReadinessMetadata>(
                Builders<EmployeeOnboardingReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.OnboardingReadinessState)
                    .Ascending(x => x.LifecycleBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<EmployeeOnboardingReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<EmployeeOnboardingReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<EmployeeOnboardingReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<EmployeeOnboardingReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<EmployeeOnboardingReadinessMetadata>.Filter.And(
            Builders<EmployeeOnboardingReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<EmployeeOnboardingReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
