using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.PayrollSources;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class PayrollSourceRepository : TenantRepository<PayrollExternalSystemProfile>, IPayrollSourceRepository
{
    private readonly IMongoCollection<PayrollContractProfile> _contractProfiles;
    private readonly IMongoCollection<PayrollEmployeeReferenceMap> _employeeReferenceMaps;
    private readonly IMongoCollection<PayrollCycleReference> _cycleReferences;
    private readonly IMongoCollection<PayrollResultReference> _resultReferences;
    private readonly IMongoCollection<PayrollSourceHealthSnapshot> _healthSnapshots;

    public PayrollSourceRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PayrollSourceCollectionNames.ExternalSystemProfiles)
    {
        _contractProfiles = dbContext.GetCollection<PayrollContractProfile>(PayrollSourceCollectionNames.ContractProfiles);
        _employeeReferenceMaps = dbContext.GetCollection<PayrollEmployeeReferenceMap>(PayrollSourceCollectionNames.EmployeeReferenceMaps);
        _cycleReferences = dbContext.GetCollection<PayrollCycleReference>(PayrollSourceCollectionNames.CycleReferences);
        _resultReferences = dbContext.GetCollection<PayrollResultReference>(PayrollSourceCollectionNames.ResultReferences);
        _healthSnapshots = dbContext.GetCollection<PayrollSourceHealthSnapshot>(PayrollSourceCollectionNames.HealthSnapshots);
    }

    public Task<PayrollExternalSystemProfile> CreateExternalSystemProfileAsync(PayrollExternalSystemProfile profile, CancellationToken ct = default) =>
        CreateAsync(profile, ct);

    public Task<PayrollExternalSystemProfile?> GetExternalSystemProfileByIdAsync(Guid id, CancellationToken ct = default) =>
        GetByIdAsync(id, ct);

    public Task<IReadOnlyList<PayrollExternalSystemProfile>> GetExternalSystemProfilesAsync(CancellationToken ct = default) =>
        GetAllAsync(ct);

    public Task<bool> ExistsActiveCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<PayrollExternalSystemProfile>>
        {
            ExecutionFilter,
            Builders<PayrollExternalSystemProfile>.Filter.Eq(x => x.Code, code)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<PayrollExternalSystemProfile>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return Collection.Find(Builders<PayrollExternalSystemProfile>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task UpdateExternalSystemProfileAsync(PayrollExternalSystemProfile profile, CancellationToken ct = default)
    {
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<PayrollExternalSystemProfile>.Filter.And(
            ExecutionFilter,
            Builders<PayrollExternalSystemProfile>.Filter.Eq(x => x.Id, profile.Id));

        await Collection.ReplaceOneAsync(filter, profile, cancellationToken: ct);
    }

    public async Task<bool> ArchiveExternalSystemProfileAsync(Guid id, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var filter = Builders<PayrollExternalSystemProfile>.Filter.And(
            ExecutionFilter,
            Builders<PayrollExternalSystemProfile>.Filter.Eq(x => x.Id, id));

        var update = Builders<PayrollExternalSystemProfile>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, now)
            .Set(x => x.UpdatedAt, now);

        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    public async Task<PayrollContractProfile?> GetContractProfileAsync(Guid sourceProfileId, CancellationToken ct = default)
    {
        return await _contractProfiles.Find(TenantFilter<PayrollContractProfile>(
                Builders<PayrollContractProfile>.Filter.Eq(x => x.PayrollExternalSystemProfileId, sourceProfileId)))
            .SortByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpsertContractProfileAsync(PayrollContractProfile contractProfile, CancellationToken ct = default)
    {
        contractProfile.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = TenantFilter<PayrollContractProfile>(Builders<PayrollContractProfile>.Filter.Eq(x => x.Id, contractProfile.Id));
        var result = await _contractProfiles.ReplaceOneAsync(filter, contractProfile, cancellationToken: ct);
        if (result.MatchedCount == 0)
        {
            await _contractProfiles.InsertOneAsync(contractProfile, cancellationToken: ct);
        }
    }

    public async Task<PayrollEmployeeReferenceMap> CreateEmployeeReferenceMapAsync(PayrollEmployeeReferenceMap referenceMap, CancellationToken ct = default)
    {
        await _employeeReferenceMaps.InsertOneAsync(referenceMap, cancellationToken: ct);
        return referenceMap;
    }

    public async Task<PayrollEmployeeReferenceMap?> GetEmployeeReferenceMapByIdAsync(Guid sourceProfileId, Guid mapId, CancellationToken ct = default)
    {
        return await _employeeReferenceMaps.Find(TenantFilter<PayrollEmployeeReferenceMap>(
                Builders<PayrollEmployeeReferenceMap>.Filter.Eq(x => x.PayrollExternalSystemProfileId, sourceProfileId),
                Builders<PayrollEmployeeReferenceMap>.Filter.Eq(x => x.Id, mapId)))
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpdateEmployeeReferenceMapAsync(PayrollEmployeeReferenceMap referenceMap, CancellationToken ct = default)
    {
        referenceMap.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = TenantFilter<PayrollEmployeeReferenceMap>(Builders<PayrollEmployeeReferenceMap>.Filter.Eq(x => x.Id, referenceMap.Id));
        await _employeeReferenceMaps.ReplaceOneAsync(filter, referenceMap, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<PayrollEmployeeReferenceMap>> GetEmployeeReferenceMapsAsync(Guid sourceProfileId, CancellationToken ct = default)
    {
        return await _employeeReferenceMaps.Find(TenantFilter<PayrollEmployeeReferenceMap>(
                Builders<PayrollEmployeeReferenceMap>.Filter.Eq(x => x.PayrollExternalSystemProfileId, sourceProfileId)))
            .SortBy(x => x.ExternalEmployeeReference)
            .ToListAsync(ct);
    }

    public async Task<PayrollCycleReference> CreateCycleReferenceAsync(PayrollCycleReference cycleReference, CancellationToken ct = default)
    {
        await _cycleReferences.InsertOneAsync(cycleReference, cancellationToken: ct);
        return cycleReference;
    }

    public async Task<PayrollCycleReference?> GetCycleReferenceByIdAsync(Guid sourceProfileId, Guid cycleReferenceId, CancellationToken ct = default)
    {
        return await _cycleReferences.Find(TenantFilter<PayrollCycleReference>(
                Builders<PayrollCycleReference>.Filter.Eq(x => x.PayrollExternalSystemProfileId, sourceProfileId),
                Builders<PayrollCycleReference>.Filter.Eq(x => x.Id, cycleReferenceId)))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<PayrollCycleReference>> GetCycleReferencesAsync(Guid sourceProfileId, CancellationToken ct = default)
    {
        return await _cycleReferences.Find(TenantFilter<PayrollCycleReference>(
                Builders<PayrollCycleReference>.Filter.Eq(x => x.PayrollExternalSystemProfileId, sourceProfileId)))
            .SortByDescending(x => x.PeriodStart)
            .ToListAsync(ct);
    }

    public async Task<PayrollResultReference> CreateResultReferenceAsync(PayrollResultReference resultReference, CancellationToken ct = default)
    {
        await _resultReferences.InsertOneAsync(resultReference, cancellationToken: ct);
        return resultReference;
    }

    public async Task<IReadOnlyList<PayrollResultReference>> GetResultReferencesAsync(Guid sourceProfileId, CancellationToken ct = default)
    {
        return await _resultReferences.Find(TenantFilter<PayrollResultReference>(
                Builders<PayrollResultReference>.Filter.Eq(x => x.PayrollExternalSystemProfileId, sourceProfileId)))
            .SortByDescending(x => x.PublishedAt)
            .ToListAsync(ct);
    }

    public async Task<PayrollSourceHealthSnapshot> CreateHealthSnapshotAsync(PayrollSourceHealthSnapshot healthSnapshot, CancellationToken ct = default)
    {
        await _healthSnapshots.InsertOneAsync(healthSnapshot, cancellationToken: ct);
        return healthSnapshot;
    }

    public async Task<PayrollSourceHealthSnapshot?> GetLatestHealthSnapshotAsync(Guid sourceProfileId, CancellationToken ct = default)
    {
        return await _healthSnapshots.Find(TenantFilter<PayrollSourceHealthSnapshot>(
                Builders<PayrollSourceHealthSnapshot>.Filter.Eq(x => x.PayrollExternalSystemProfileId, sourceProfileId)))
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
