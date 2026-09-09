using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoEmployeeProjectionRepository : IEmployeeProjectionRepository
{
    public const string CollectionName = "hcm_employee_profile_projections";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_employee_projections_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_employee_projections_tenant_state_visibility";

    private readonly IMongoCollection<EmployeeProfileProjection> _collection;
    private bool _indexesEnsured;

    public MongoEmployeeProjectionRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<EmployeeProfileProjection>(CollectionName);
    }

    public async Task<IReadOnlyList<EmployeeProfileProjection>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<EmployeeProfileProjection?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EmployeeProfileProjection>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<EmployeeProfileProjection>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EmployeeProfileProjection>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<EmployeeProfileProjection>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<EmployeeProfileProjection>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<EmployeeProfileProjection>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(EmployeeProfileProjection projection, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(projection, cancellationToken: ct);
    }

    public async Task UpdateAsync(EmployeeProfileProjection projection, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EmployeeProfileProjection>.Filter.And(
            Builders<EmployeeProfileProjection>.Filter.Eq(x => x.TenantId, projection.TenantId),
            Builders<EmployeeProfileProjection>.Filter.Eq(x => x.Id, projection.Id),
            Builders<EmployeeProfileProjection>.Filter.Eq(x => x.IsDeleted, false));
        await _collection.ReplaceOneAsync(filter, projection, cancellationToken: ct);
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (_indexesEnsured)
        {
            return;
        }

        await _collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<EmployeeProfileProjection>(
                Builders<EmployeeProfileProjection>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<EmployeeProfileProjection>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<EmployeeProfileProjection>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<EmployeeProfileProjection>(
                Builders<EmployeeProfileProjection>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ProjectionState)
                    .Ascending(x => x.VisibilityClassification)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<EmployeeProfileProjection> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<EmployeeProfileProjection>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<EmployeeProfileProjection>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<EmployeeProfileProjection> ActiveTenantFilter(Guid tenantId) =>
        Builders<EmployeeProfileProjection>.Filter.And(
            Builders<EmployeeProfileProjection>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<EmployeeProfileProjection>.Filter.Eq(x => x.IsDeleted, false));
}
