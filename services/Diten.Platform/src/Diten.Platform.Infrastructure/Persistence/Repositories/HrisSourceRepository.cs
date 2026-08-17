using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.HrisSources;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class HrisSourceRepository : TenantRepository<HrisSourceProfile>, IHrisSourceRepository
{
    private readonly IMongoCollection<HrisMappingProfile> _mappingProfiles;
    private readonly IMongoCollection<HrisExternalIdentifierMap> _identifierMaps;
    private readonly IMongoCollection<HrisSyncCheckpoint> _syncCheckpoints;
    private readonly IMongoCollection<HrisSourceHealthSnapshot> _healthSnapshots;

    public HrisSourceRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, HrisSourceCollectionNames.SourceProfiles)
    {
        _mappingProfiles = dbContext.GetCollection<HrisMappingProfile>(HrisSourceCollectionNames.MappingProfiles);
        _identifierMaps = dbContext.GetCollection<HrisExternalIdentifierMap>(HrisSourceCollectionNames.IdentifierMaps);
        _syncCheckpoints = dbContext.GetCollection<HrisSyncCheckpoint>(HrisSourceCollectionNames.SyncCheckpoints);
        _healthSnapshots = dbContext.GetCollection<HrisSourceHealthSnapshot>(HrisSourceCollectionNames.HealthSnapshots);
    }

    public Task<HrisSourceProfile> CreateSourceProfileAsync(HrisSourceProfile sourceProfile, CancellationToken ct = default) =>
        CreateAsync(sourceProfile, ct);

    public Task<HrisSourceProfile?> GetSourceProfileByIdAsync(Guid id, CancellationToken ct = default) =>
        GetByIdAsync(id, ct);

    public Task<IReadOnlyList<HrisSourceProfile>> GetSourceProfilesAsync(CancellationToken ct = default) =>
        GetAllAsync(ct);

    public Task<bool> ExistsActiveSourceCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<HrisSourceProfile>>
        {
            ExecutionFilter,
            Builders<HrisSourceProfile>.Filter.Eq(x => x.Code, code)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<HrisSourceProfile>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return Collection.Find(Builders<HrisSourceProfile>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task UpdateSourceProfileAsync(HrisSourceProfile sourceProfile, CancellationToken ct = default)
    {
        sourceProfile.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<HrisSourceProfile>.Filter.And(
            ExecutionFilter,
            Builders<HrisSourceProfile>.Filter.Eq(x => x.Id, sourceProfile.Id));

        await Collection.ReplaceOneAsync(filter, sourceProfile, cancellationToken: ct);
    }

    public async Task<bool> ArchiveSourceProfileAsync(Guid id, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var filter = Builders<HrisSourceProfile>.Filter.And(
            ExecutionFilter,
            Builders<HrisSourceProfile>.Filter.Eq(x => x.Id, id));

        var update = Builders<HrisSourceProfile>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, now)
            .Set(x => x.UpdatedAt, now);

        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    public Task<bool> HasActiveIdentifierMapsAsync(Guid sourceProfileId, CancellationToken ct = default) =>
        _identifierMaps.Find(TenantFilter<HrisExternalIdentifierMap>(
                Builders<HrisExternalIdentifierMap>.Filter.Eq(x => x.SourceProfileId, sourceProfileId)))
            .AnyAsync(ct);

    public async Task<HrisMappingProfile?> GetActiveMappingProfileAsync(Guid sourceProfileId, CancellationToken ct = default)
    {
        return await _mappingProfiles.Find(TenantFilter<HrisMappingProfile>(
                Builders<HrisMappingProfile>.Filter.Eq(x => x.SourceProfileId, sourceProfileId),
                Builders<HrisMappingProfile>.Filter.Eq(x => x.IsActive, true)))
            .SortByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpsertMappingProfileAsync(HrisMappingProfile mappingProfile, CancellationToken ct = default)
    {
        mappingProfile.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = TenantFilter<HrisMappingProfile>(Builders<HrisMappingProfile>.Filter.Eq(x => x.Id, mappingProfile.Id));
        var result = await _mappingProfiles.ReplaceOneAsync(filter, mappingProfile, cancellationToken: ct);
        if (result.MatchedCount == 0)
        {
            await _mappingProfiles.InsertOneAsync(mappingProfile, cancellationToken: ct);
        }
    }

    public async Task ReplaceIdentifierMapsAsync(Guid sourceProfileId, IReadOnlyList<HrisExternalIdentifierMap> identifierMaps, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var filter = TenantFilter<HrisExternalIdentifierMap>(Builders<HrisExternalIdentifierMap>.Filter.Eq(x => x.SourceProfileId, sourceProfileId));
        var update = Builders<HrisExternalIdentifierMap>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, now)
            .Set(x => x.UpdatedAt, now);

        await _identifierMaps.UpdateManyAsync(filter, update, cancellationToken: ct);
        if (identifierMaps.Count > 0)
        {
            await _identifierMaps.InsertManyAsync(identifierMaps, cancellationToken: ct);
        }
    }

    public async Task<IReadOnlyList<HrisExternalIdentifierMap>> GetIdentifierMapsAsync(Guid sourceProfileId, CancellationToken ct = default)
    {
        return await _identifierMaps.Find(TenantFilter<HrisExternalIdentifierMap>(
                Builders<HrisExternalIdentifierMap>.Filter.Eq(x => x.SourceProfileId, sourceProfileId)))
            .SortBy(x => x.ExternalObjectType)
            .ThenBy(x => x.ExternalObjectId)
            .ToListAsync(ct);
    }

    public async Task<HrisSyncCheckpoint> CreateSyncCheckpointAsync(HrisSyncCheckpoint checkpoint, CancellationToken ct = default)
    {
        await _syncCheckpoints.InsertOneAsync(checkpoint, cancellationToken: ct);
        return checkpoint;
    }

    public async Task<HrisSyncCheckpoint?> GetLatestSyncCheckpointAsync(Guid sourceProfileId, CancellationToken ct = default)
    {
        return await _syncCheckpoints.Find(TenantFilter<HrisSyncCheckpoint>(Builders<HrisSyncCheckpoint>.Filter.Eq(x => x.SourceProfileId, sourceProfileId)))
            .SortByDescending(x => x.StartedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<HrisSourceHealthSnapshot> CreateHealthSnapshotAsync(HrisSourceHealthSnapshot healthSnapshot, CancellationToken ct = default)
    {
        await _healthSnapshots.InsertOneAsync(healthSnapshot, cancellationToken: ct);
        return healthSnapshot;
    }

    public async Task<HrisSourceHealthSnapshot?> GetLatestHealthSnapshotAsync(Guid sourceProfileId, CancellationToken ct = default)
    {
        return await _healthSnapshots.Find(TenantFilter<HrisSourceHealthSnapshot>(Builders<HrisSourceHealthSnapshot>.Filter.Eq(x => x.SourceProfileId, sourceProfileId)))
            .SortByDescending(x => x.ObservedAt)
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
