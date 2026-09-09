using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoPositionAssignmentOverlayRepository : IPositionAssignmentOverlayRepository
{
    public const string CollectionName = "hcm_position_assignment_overlays";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_position_assignments_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_position_assignments_tenant_state_refs";

    private readonly IMongoCollection<EmployeePositionAssignmentOverlay> _collection;
    private bool _indexesEnsured;

    public MongoPositionAssignmentOverlayRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<EmployeePositionAssignmentOverlay>(CollectionName);
    }

    public async Task<IReadOnlyList<EmployeePositionAssignmentOverlay>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<EmployeePositionAssignmentOverlay?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EmployeePositionAssignmentOverlay>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<EmployeePositionAssignmentOverlay>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EmployeePositionAssignmentOverlay>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<EmployeePositionAssignmentOverlay>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<EmployeePositionAssignmentOverlay>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<EmployeePositionAssignmentOverlay>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(EmployeePositionAssignmentOverlay assignment, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(assignment, cancellationToken: ct);
    }

    public async Task UpdateAsync(EmployeePositionAssignmentOverlay assignment, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<EmployeePositionAssignmentOverlay>.Filter.And(
            Builders<EmployeePositionAssignmentOverlay>.Filter.Eq(x => x.TenantId, assignment.TenantId),
            Builders<EmployeePositionAssignmentOverlay>.Filter.Eq(x => x.Id, assignment.Id));
        await _collection.ReplaceOneAsync(filter, assignment, cancellationToken: ct);
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (_indexesEnsured)
        {
            return;
        }

        await _collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<EmployeePositionAssignmentOverlay>(
                Builders<EmployeePositionAssignmentOverlay>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<EmployeePositionAssignmentOverlay>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<EmployeePositionAssignmentOverlay>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<EmployeePositionAssignmentOverlay>(
                Builders<EmployeePositionAssignmentOverlay>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.AssignmentState)
                    .Ascending(x => x.EmployeeProjectionId)
                    .Ascending(x => x.PersonReferenceId)
                    .Ascending(x => x.OrganizationUnitId)
                    .Ascending(x => x.PositionId)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<EmployeePositionAssignmentOverlay> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<EmployeePositionAssignmentOverlay>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<EmployeePositionAssignmentOverlay>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<EmployeePositionAssignmentOverlay> ActiveTenantFilter(Guid tenantId) =>
        Builders<EmployeePositionAssignmentOverlay>.Filter.And(
            Builders<EmployeePositionAssignmentOverlay>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<EmployeePositionAssignmentOverlay>.Filter.Eq(x => x.IsDeleted, false));
}
