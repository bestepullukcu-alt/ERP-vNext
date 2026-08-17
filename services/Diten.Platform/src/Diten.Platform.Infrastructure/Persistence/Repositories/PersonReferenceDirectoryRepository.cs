using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.PersonReferenceDirectory;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class PersonReferenceDirectoryRepository : TenantRepository<PersonReferenceProjection>, IPersonReferenceDirectoryRepository
{
    private readonly IMongoCollection<PersonReferenceExternalCorrelation> _correlations;
    private readonly IMongoCollection<PersonReferenceDirectoryHealthSnapshot> _healthSnapshots;

    public PersonReferenceDirectoryRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PersonReferenceDirectoryCollectionNames.Projections)
    {
        _correlations = dbContext.GetCollection<PersonReferenceExternalCorrelation>(PersonReferenceDirectoryCollectionNames.ExternalCorrelations);
        _healthSnapshots = dbContext.GetCollection<PersonReferenceDirectoryHealthSnapshot>(PersonReferenceDirectoryCollectionNames.HealthSnapshots);
    }

    public Task<PersonReferenceProjection> CreateProjectionAsync(PersonReferenceProjection projection, CancellationToken ct = default) =>
        CreateAsync(projection, ct);

    public Task<PersonReferenceProjection?> GetProjectionByIdAsync(Guid id, CancellationToken ct = default) =>
        GetByIdAsync(id, ct);

    public Task<IReadOnlyList<PersonReferenceProjection>> GetProjectionsAsync(CancellationToken ct = default) =>
        GetAllAsync(ct);

    public Task<bool> ExistsActiveCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<PersonReferenceProjection>>
        {
            ExecutionFilter,
            Builders<PersonReferenceProjection>.Filter.Eq(x => x.Code, code)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<PersonReferenceProjection>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return Collection.Find(Builders<PersonReferenceProjection>.Filter.And(filters)).AnyAsync(ct);
    }

    public Task<bool> ExistsActiveCorrelationKeyAsync(string correlationKey, Guid hrisSourceProfileId, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<PersonReferenceExternalCorrelation>>
        {
            TenantFilter<PersonReferenceExternalCorrelation>(),
            Builders<PersonReferenceExternalCorrelation>.Filter.Eq(x => x.HrisSourceProfileId, hrisSourceProfileId),
            Builders<PersonReferenceExternalCorrelation>.Filter.Eq(x => x.CorrelationKey, correlationKey)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<PersonReferenceExternalCorrelation>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return _correlations.Find(Builders<PersonReferenceExternalCorrelation>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task UpdateProjectionAsync(PersonReferenceProjection projection, CancellationToken ct = default)
    {
        projection.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<PersonReferenceProjection>.Filter.And(
            ExecutionFilter,
            Builders<PersonReferenceProjection>.Filter.Eq(x => x.Id, projection.Id));

        await Collection.ReplaceOneAsync(filter, projection, cancellationToken: ct);
    }

    public async Task<bool> ArchiveProjectionAsync(Guid id, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var filter = Builders<PersonReferenceProjection>.Filter.And(
            ExecutionFilter,
            Builders<PersonReferenceProjection>.Filter.Eq(x => x.Id, id));

        var update = Builders<PersonReferenceProjection>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, now)
            .Set(x => x.ReferenceState, PersonReferenceState.Archived)
            .Set(x => x.UpdatedAt, now);

        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    public async Task<PersonReferenceExternalCorrelation> UpsertCorrelationAsync(PersonReferenceExternalCorrelation correlation, CancellationToken ct = default)
    {
        correlation.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = TenantFilter<PersonReferenceExternalCorrelation>(Builders<PersonReferenceExternalCorrelation>.Filter.Eq(x => x.Id, correlation.Id));
        var result = await _correlations.ReplaceOneAsync(filter, correlation, cancellationToken: ct);
        if (result.MatchedCount == 0)
        {
            await _correlations.InsertOneAsync(correlation, cancellationToken: ct);
        }

        return correlation;
    }

    public async Task<PersonReferenceExternalCorrelation?> GetCorrelationByIdAsync(Guid id, CancellationToken ct = default) =>
        await _correlations.Find(TenantFilter<PersonReferenceExternalCorrelation>(Builders<PersonReferenceExternalCorrelation>.Filter.Eq(x => x.Id, id)))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<PersonReferenceExternalCorrelation>> GetCorrelationsAsync(Guid projectionId, CancellationToken ct = default)
    {
        return await _correlations.Find(TenantFilter<PersonReferenceExternalCorrelation>(
                Builders<PersonReferenceExternalCorrelation>.Filter.Eq(x => x.PersonReferenceProjectionId, projectionId)))
            .SortBy(x => x.ExternalObjectReference)
            .ToListAsync(ct);
    }

    public async Task<PersonReferenceDirectoryHealthSnapshot> CreateHealthSnapshotAsync(PersonReferenceDirectoryHealthSnapshot healthSnapshot, CancellationToken ct = default)
    {
        await _healthSnapshots.InsertOneAsync(healthSnapshot, cancellationToken: ct);
        return healthSnapshot;
    }

    public async Task<PersonReferenceDirectoryHealthSnapshot?> GetLatestHealthSnapshotAsync(CancellationToken ct = default) =>
        await _healthSnapshots.Find(TenantFilter<PersonReferenceDirectoryHealthSnapshot>())
            .SortByDescending(x => x.LastCheckedAt)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

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
