using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeAttendanceProviders;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class TimeAttendanceProviderRepository : TenantRepository<TimeAttendanceExternalProviderProfile>, ITimeAttendanceProviderRepository
{
    private readonly IMongoCollection<TimeAttendanceContractProfile> _contractProfiles;
    private readonly IMongoCollection<TimeAttendanceEmployeeReferenceMap> _employeeReferenceMaps;
    private readonly IMongoCollection<TimeAttendanceEventReference> _eventReferences;
    private readonly IMongoCollection<AttendanceSummaryReference> _summaryReferences;
    private readonly IMongoCollection<TimeAttendanceSyncCheckpoint> _syncCheckpoints;
    private readonly IMongoCollection<TimeAttendanceProviderHealthSnapshot> _healthSnapshots;

    public TimeAttendanceProviderRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, TimeAttendanceProviderCollectionNames.ProviderProfiles)
    {
        _contractProfiles = dbContext.GetCollection<TimeAttendanceContractProfile>(TimeAttendanceProviderCollectionNames.ContractProfiles);
        _employeeReferenceMaps = dbContext.GetCollection<TimeAttendanceEmployeeReferenceMap>(TimeAttendanceProviderCollectionNames.EmployeeReferenceMaps);
        _eventReferences = dbContext.GetCollection<TimeAttendanceEventReference>(TimeAttendanceProviderCollectionNames.EventReferences);
        _summaryReferences = dbContext.GetCollection<AttendanceSummaryReference>(TimeAttendanceProviderCollectionNames.AttendanceSummaryReferences);
        _syncCheckpoints = dbContext.GetCollection<TimeAttendanceSyncCheckpoint>(TimeAttendanceProviderCollectionNames.SyncCheckpoints);
        _healthSnapshots = dbContext.GetCollection<TimeAttendanceProviderHealthSnapshot>(TimeAttendanceProviderCollectionNames.HealthSnapshots);
    }

    public Task<TimeAttendanceExternalProviderProfile> CreateProviderProfileAsync(TimeAttendanceExternalProviderProfile profile, CancellationToken ct = default) =>
        CreateAsync(profile, ct);

    public Task<TimeAttendanceExternalProviderProfile?> GetProviderProfileByIdAsync(Guid id, CancellationToken ct = default) =>
        GetByIdAsync(id, ct);

    public Task<IReadOnlyList<TimeAttendanceExternalProviderProfile>> GetProviderProfilesAsync(CancellationToken ct = default) =>
        GetAllAsync(ct);

    public Task<bool> ExistsActiveCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<TimeAttendanceExternalProviderProfile>>
        {
            ExecutionFilter,
            Builders<TimeAttendanceExternalProviderProfile>.Filter.Eq(x => x.Code, code)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<TimeAttendanceExternalProviderProfile>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return Collection.Find(Builders<TimeAttendanceExternalProviderProfile>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task UpdateProviderProfileAsync(TimeAttendanceExternalProviderProfile profile, CancellationToken ct = default)
    {
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<TimeAttendanceExternalProviderProfile>.Filter.And(
            ExecutionFilter,
            Builders<TimeAttendanceExternalProviderProfile>.Filter.Eq(x => x.Id, profile.Id));

        await Collection.ReplaceOneAsync(filter, profile, cancellationToken: ct);
    }

    public async Task<bool> ArchiveProviderProfileAsync(Guid id, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var filter = Builders<TimeAttendanceExternalProviderProfile>.Filter.And(
            ExecutionFilter,
            Builders<TimeAttendanceExternalProviderProfile>.Filter.Eq(x => x.Id, id));

        var update = Builders<TimeAttendanceExternalProviderProfile>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, now)
            .Set(x => x.UpdatedAt, now);

        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    public async Task<TimeAttendanceContractProfile?> GetContractProfileAsync(Guid providerProfileId, CancellationToken ct = default)
    {
        return await _contractProfiles.Find(TenantFilter<TimeAttendanceContractProfile>(
                Builders<TimeAttendanceContractProfile>.Filter.Eq(x => x.ProviderProfileId, providerProfileId)))
            .SortByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpsertContractProfileAsync(TimeAttendanceContractProfile contractProfile, CancellationToken ct = default)
    {
        contractProfile.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = TenantFilter<TimeAttendanceContractProfile>(Builders<TimeAttendanceContractProfile>.Filter.Eq(x => x.Id, contractProfile.Id));
        var result = await _contractProfiles.ReplaceOneAsync(filter, contractProfile, cancellationToken: ct);
        if (result.MatchedCount == 0)
        {
            await _contractProfiles.InsertOneAsync(contractProfile, cancellationToken: ct);
        }
    }

    public async Task<TimeAttendanceEmployeeReferenceMap> CreateEmployeeReferenceMapAsync(TimeAttendanceEmployeeReferenceMap referenceMap, CancellationToken ct = default)
    {
        await _employeeReferenceMaps.InsertOneAsync(referenceMap, cancellationToken: ct);
        return referenceMap;
    }

    public async Task<TimeAttendanceEmployeeReferenceMap?> GetEmployeeReferenceMapByIdAsync(Guid providerProfileId, Guid mapId, CancellationToken ct = default)
    {
        return await _employeeReferenceMaps.Find(TenantFilter<TimeAttendanceEmployeeReferenceMap>(
                Builders<TimeAttendanceEmployeeReferenceMap>.Filter.Eq(x => x.ProviderProfileId, providerProfileId),
                Builders<TimeAttendanceEmployeeReferenceMap>.Filter.Eq(x => x.Id, mapId)))
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpdateEmployeeReferenceMapAsync(TimeAttendanceEmployeeReferenceMap referenceMap, CancellationToken ct = default)
    {
        referenceMap.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = TenantFilter<TimeAttendanceEmployeeReferenceMap>(Builders<TimeAttendanceEmployeeReferenceMap>.Filter.Eq(x => x.Id, referenceMap.Id));
        await _employeeReferenceMaps.ReplaceOneAsync(filter, referenceMap, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<TimeAttendanceEmployeeReferenceMap>> GetEmployeeReferenceMapsAsync(Guid providerProfileId, CancellationToken ct = default)
    {
        return await _employeeReferenceMaps.Find(TenantFilter<TimeAttendanceEmployeeReferenceMap>(
                Builders<TimeAttendanceEmployeeReferenceMap>.Filter.Eq(x => x.ProviderProfileId, providerProfileId)))
            .SortBy(x => x.ExternalEmployeeReference)
            .ToListAsync(ct);
    }

    public async Task<TimeAttendanceEventReference> CreateEventReferenceAsync(TimeAttendanceEventReference eventReference, CancellationToken ct = default)
    {
        await _eventReferences.InsertOneAsync(eventReference, cancellationToken: ct);
        return eventReference;
    }

    public async Task<IReadOnlyList<TimeAttendanceEventReference>> GetEventReferencesAsync(Guid providerProfileId, CancellationToken ct = default)
    {
        return await _eventReferences.Find(TenantFilter<TimeAttendanceEventReference>(
                Builders<TimeAttendanceEventReference>.Filter.Eq(x => x.ProviderProfileId, providerProfileId)))
            .SortByDescending(x => x.ProviderTimestamp)
            .ToListAsync(ct);
    }

    public async Task<AttendanceSummaryReference> CreateAttendanceSummaryReferenceAsync(AttendanceSummaryReference summaryReference, CancellationToken ct = default)
    {
        await _summaryReferences.InsertOneAsync(summaryReference, cancellationToken: ct);
        return summaryReference;
    }

    public async Task<IReadOnlyList<AttendanceSummaryReference>> GetAttendanceSummaryReferencesAsync(Guid providerProfileId, CancellationToken ct = default)
    {
        return await _summaryReferences.Find(TenantFilter<AttendanceSummaryReference>(
                Builders<AttendanceSummaryReference>.Filter.Eq(x => x.ProviderProfileId, providerProfileId)))
            .SortByDescending(x => x.SummaryPeriodStart)
            .ToListAsync(ct);
    }

    public async Task<TimeAttendanceSyncCheckpoint> CreateSyncCheckpointAsync(TimeAttendanceSyncCheckpoint checkpoint, CancellationToken ct = default)
    {
        await _syncCheckpoints.InsertOneAsync(checkpoint, cancellationToken: ct);
        return checkpoint;
    }

    public async Task<TimeAttendanceSyncCheckpoint?> GetLatestSyncCheckpointAsync(Guid providerProfileId, CancellationToken ct = default)
    {
        return await _syncCheckpoints.Find(TenantFilter<TimeAttendanceSyncCheckpoint>(
                Builders<TimeAttendanceSyncCheckpoint>.Filter.Eq(x => x.ProviderProfileId, providerProfileId)))
            .SortByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<TimeAttendanceProviderHealthSnapshot> CreateHealthSnapshotAsync(TimeAttendanceProviderHealthSnapshot healthSnapshot, CancellationToken ct = default)
    {
        await _healthSnapshots.InsertOneAsync(healthSnapshot, cancellationToken: ct);
        return healthSnapshot;
    }

    public async Task<TimeAttendanceProviderHealthSnapshot?> GetLatestHealthSnapshotAsync(Guid providerProfileId, CancellationToken ct = default)
    {
        return await _healthSnapshots.Find(TenantFilter<TimeAttendanceProviderHealthSnapshot>(
                Builders<TimeAttendanceProviderHealthSnapshot>.Filter.Eq(x => x.ProviderProfileId, providerProfileId)))
            .SortByDescending(x => x.CheckedAt)
            .FirstOrDefaultAsync(ct);
    }

    private FilterDefinition<TEntity> TenantFilter<TEntity>(params FilterDefinition<TEntity>[] filters)
        where TEntity : TenantScopedEntity
    {
        var allFilters = new List<FilterDefinition<TEntity>>
        {
            Builders<TEntity>.Filter.Eq(x => x.TenantId, TenantContext.TenantId),
            Builders<TEntity>.Filter.Eq(x => x.IsDeleted, false)
        };
        allFilters.AddRange(filters);
        return Builders<TEntity>.Filter.And(allFilters);
    }
}
