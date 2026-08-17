using Diten.Platform.Domain.Entities.PersonReferenceDirectory;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.PersonReferenceDirectory;

internal sealed class InMemoryPersonReferenceDirectoryRepository : IPersonReferenceDirectoryRepository
{
    private readonly Guid _tenantId;
    private readonly List<PersonReferenceProjection> _projections = [];
    private readonly List<PersonReferenceExternalCorrelation> _correlations = [];
    private readonly List<PersonReferenceDirectoryHealthSnapshot> _healthSnapshots = [];

    public InMemoryPersonReferenceDirectoryRepository(Guid tenantId) => _tenantId = tenantId;

    public IReadOnlyList<PersonReferenceProjection> Projections => _projections;
    public IReadOnlyList<PersonReferenceExternalCorrelation> Correlations => _correlations;

    public Task<PersonReferenceProjection> CreateProjectionAsync(PersonReferenceProjection projection, CancellationToken ct = default)
    {
        _projections.Add(projection);
        return Task.FromResult(projection);
    }

    public Task<PersonReferenceProjection?> GetProjectionByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_projections.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted));

    public Task<IReadOnlyList<PersonReferenceProjection>> GetProjectionsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<PersonReferenceProjection> result = _projections
            .Where(x => x.TenantId == _tenantId && !x.IsDeleted)
            .OrderBy(x => x.Code)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<bool> ExistsActiveCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) =>
        Task.FromResult(_projections.Any(x =>
            x.TenantId == _tenantId
            && !x.IsDeleted
            && x.Code == code
            && (!excludeId.HasValue || x.Id != excludeId.Value)));

    public Task<bool> ExistsActiveCorrelationKeyAsync(string correlationKey, Guid hrisSourceProfileId, Guid? excludeId = null, CancellationToken ct = default) =>
        Task.FromResult(_correlations.Any(x =>
            x.TenantId == _tenantId
            && !x.IsDeleted
            && x.HrisSourceProfileId == hrisSourceProfileId
            && x.CorrelationKey == correlationKey
            && (!excludeId.HasValue || x.Id != excludeId.Value)));

    public Task UpdateProjectionAsync(PersonReferenceProjection projection, CancellationToken ct = default)
    {
        projection.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<bool> ArchiveProjectionAsync(Guid id, CancellationToken ct = default)
    {
        var item = _projections.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted);
        if (item == null)
        {
            return Task.FromResult(false);
        }

        item.IsDeleted = true;
        item.DeletedAt = DateTimeOffset.UtcNow;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.FromResult(true);
    }

    public Task<PersonReferenceExternalCorrelation> UpsertCorrelationAsync(PersonReferenceExternalCorrelation correlation, CancellationToken ct = default)
    {
        var index = _correlations.FindIndex(x => x.Id == correlation.Id && x.TenantId == _tenantId && !x.IsDeleted);
        if (index >= 0)
        {
            _correlations[index] = correlation;
        }
        else
        {
            _correlations.Add(correlation);
        }

        return Task.FromResult(correlation);
    }

    public Task<PersonReferenceExternalCorrelation?> GetCorrelationByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_correlations.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted));

    public Task<IReadOnlyList<PersonReferenceExternalCorrelation>> GetCorrelationsAsync(Guid projectionId, CancellationToken ct = default)
    {
        IReadOnlyList<PersonReferenceExternalCorrelation> result = _correlations
            .Where(x => x.TenantId == _tenantId && x.PersonReferenceProjectionId == projectionId && !x.IsDeleted)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<PersonReferenceDirectoryHealthSnapshot> CreateHealthSnapshotAsync(PersonReferenceDirectoryHealthSnapshot healthSnapshot, CancellationToken ct = default)
    {
        _healthSnapshots.Add(healthSnapshot);
        return Task.FromResult(healthSnapshot);
    }

    public Task<PersonReferenceDirectoryHealthSnapshot?> GetLatestHealthSnapshotAsync(CancellationToken ct = default) =>
        Task.FromResult(_healthSnapshots
            .Where(x => x.TenantId == _tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.LastCheckedAt)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefault());

    public void Add(PersonReferenceProjection projection) => _projections.Add(projection);

    public void AddCorrelation(PersonReferenceExternalCorrelation correlation) => _correlations.Add(correlation);
}
