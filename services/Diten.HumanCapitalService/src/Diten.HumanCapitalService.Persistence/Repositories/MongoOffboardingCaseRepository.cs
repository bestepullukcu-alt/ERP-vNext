using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoOffboardingCaseRepository : IOffboardingCaseRepository
{
    public const string CollectionName = "hcm_offboarding_cases";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_offboarding_cases_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_offboarding_cases_tenant_state_refs";

    private readonly IMongoCollection<OffboardingCase> _collection;
    private bool _indexesEnsured;

    public MongoOffboardingCaseRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<OffboardingCase>(CollectionName);
    }

    public async Task<IReadOnlyList<OffboardingCase>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<OffboardingCase?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<OffboardingCase>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<OffboardingCase>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<OffboardingCase>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<OffboardingCase>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<OffboardingCase>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<OffboardingCase>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(OffboardingCase offboardingCase, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(offboardingCase, cancellationToken: ct);
    }

    public async Task UpdateAsync(OffboardingCase offboardingCase, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<OffboardingCase>.Filter.And(
            Builders<OffboardingCase>.Filter.Eq(x => x.TenantId, offboardingCase.TenantId),
            Builders<OffboardingCase>.Filter.Eq(x => x.Id, offboardingCase.Id));
        await _collection.ReplaceOneAsync(filter, offboardingCase, cancellationToken: ct);
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (_indexesEnsured)
        {
            return;
        }

        await _collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<OffboardingCase>(
                Builders<OffboardingCase>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<OffboardingCase>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<OffboardingCase>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<OffboardingCase>(
                Builders<OffboardingCase>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.OffboardingState)
                    .Ascending(x => x.EmployeeProjectionId)
                    .Ascending(x => x.AssignmentOverlayId)
                    .Ascending(x => x.PlannedExitDate)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<OffboardingCase> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<OffboardingCase>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<OffboardingCase>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<OffboardingCase> ActiveTenantFilter(Guid tenantId) =>
        Builders<OffboardingCase>.Filter.And(
            Builders<OffboardingCase>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<OffboardingCase>.Filter.Eq(x => x.IsDeleted, false));
}
