using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoTimeAttendanceLeaveReadinessMetadataRepository : ITimeAttendanceLeaveReadinessMetadataRepository
{
    public const string CollectionName = "hcm_time_attendance_leave_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_time_attendance_leave_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_time_attendance_leave_tenant_state";

    private readonly IMongoCollection<TimeAttendanceLeaveReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoTimeAttendanceLeaveReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TimeAttendanceLeaveReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<TimeAttendanceLeaveReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveScopeFilter(tenantId, legalEntityIds))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<TimeAttendanceLeaveReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TimeAttendanceLeaveReadinessMetadata>.Filter.And(
            ActiveScopeFilter(tenantId, legalEntityIds),
            Builders<TimeAttendanceLeaveReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TimeAttendanceLeaveReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TimeAttendanceLeaveReadinessMetadata>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<TimeAttendanceLeaveReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<TimeAttendanceLeaveReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(TimeAttendanceLeaveReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(TimeAttendanceLeaveReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TimeAttendanceLeaveReadinessMetadata>.Filter.And(
            Builders<TimeAttendanceLeaveReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<TimeAttendanceLeaveReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<TimeAttendanceLeaveReadinessMetadata>(
                Builders<TimeAttendanceLeaveReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<TimeAttendanceLeaveReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<TimeAttendanceLeaveReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TimeAttendanceLeaveReadinessMetadata>(
                Builders<TimeAttendanceLeaveReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.TimeAttendanceLeaveReadinessState)
                    .Ascending(x => x.TimesheetIntakeBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    // Tenant scoping stays authoritative; legal-entity scoping narrows to the effective
    // roll-up set. An empty set matches nothing (In []), so a caller with no scope sees none.
    private static FilterDefinition<TimeAttendanceLeaveReadinessMetadata> ActiveScopeFilter(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds) =>
        Builders<TimeAttendanceLeaveReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TimeAttendanceLeaveReadinessMetadata>.Filter.In(x => x.LegalEntityId, legalEntityIds));

    private static FilterDefinition<TimeAttendanceLeaveReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<TimeAttendanceLeaveReadinessMetadata>.Filter.And(
            Builders<TimeAttendanceLeaveReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TimeAttendanceLeaveReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
