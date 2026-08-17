using Diten.Platform.Domain.Entities.HrisSources;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.HrisSources;

internal sealed class InMemoryHrisSourceRepository : IHrisSourceRepository
{
    private readonly Guid _tenantId;
    private readonly List<HrisSourceProfile> _sourceProfiles = [];
    private readonly List<HrisMappingProfile> _mappingProfiles = [];
    private readonly List<HrisExternalIdentifierMap> _identifierMaps = [];
    private readonly List<HrisSyncCheckpoint> _syncCheckpoints = [];
    private readonly List<HrisSourceHealthSnapshot> _healthSnapshots = [];

    public InMemoryHrisSourceRepository(Guid tenantId) => _tenantId = tenantId;

    public IReadOnlyList<HrisSourceProfile> SourceProfiles => _sourceProfiles;
    public IReadOnlyList<HrisExternalIdentifierMap> IdentifierMaps => _identifierMaps;

    public Task<HrisSourceProfile> CreateSourceProfileAsync(HrisSourceProfile sourceProfile, CancellationToken ct = default)
    {
        _sourceProfiles.Add(sourceProfile);
        return Task.FromResult(sourceProfile);
    }

    public Task<HrisSourceProfile?> GetSourceProfileByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_sourceProfiles.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted));

    public Task<IReadOnlyList<HrisSourceProfile>> GetSourceProfilesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<HrisSourceProfile> result = _sourceProfiles
            .Where(x => x.TenantId == _tenantId && !x.IsDeleted)
            .OrderBy(x => x.Code)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<bool> ExistsActiveSourceCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) =>
        Task.FromResult(_sourceProfiles.Any(x =>
            x.TenantId == _tenantId
            && !x.IsDeleted
            && x.Code == code
            && (!excludeId.HasValue || x.Id != excludeId.Value)));

    public Task UpdateSourceProfileAsync(HrisSourceProfile sourceProfile, CancellationToken ct = default)
    {
        sourceProfile.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<bool> ArchiveSourceProfileAsync(Guid id, CancellationToken ct = default)
    {
        var item = _sourceProfiles.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted);
        if (item == null)
        {
            return Task.FromResult(false);
        }

        item.IsDeleted = true;
        item.DeletedAt = DateTimeOffset.UtcNow;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.FromResult(true);
    }

    public Task<bool> HasActiveIdentifierMapsAsync(Guid sourceProfileId, CancellationToken ct = default) =>
        Task.FromResult(_identifierMaps.Any(x => x.TenantId == _tenantId && x.SourceProfileId == sourceProfileId && !x.IsDeleted));

    public Task<HrisMappingProfile?> GetActiveMappingProfileAsync(Guid sourceProfileId, CancellationToken ct = default) =>
        Task.FromResult(_mappingProfiles.FirstOrDefault(x => x.TenantId == _tenantId && x.SourceProfileId == sourceProfileId && x.IsActive && !x.IsDeleted));

    public Task UpsertMappingProfileAsync(HrisMappingProfile mappingProfile, CancellationToken ct = default)
    {
        var index = _mappingProfiles.FindIndex(x => x.Id == mappingProfile.Id && x.TenantId == _tenantId && !x.IsDeleted);
        if (index >= 0)
        {
            _mappingProfiles[index] = mappingProfile;
        }
        else
        {
            _mappingProfiles.Add(mappingProfile);
        }

        return Task.CompletedTask;
    }

    public Task ReplaceIdentifierMapsAsync(Guid sourceProfileId, IReadOnlyList<HrisExternalIdentifierMap> identifierMaps, CancellationToken ct = default)
    {
        foreach (var item in _identifierMaps.Where(x => x.TenantId == _tenantId && x.SourceProfileId == sourceProfileId && !x.IsDeleted))
        {
            item.DeletedAt = DateTimeOffset.UtcNow;
            item.IsDeleted = true;
            item.UpdatedAt = DateTimeOffset.UtcNow;
        }

        _identifierMaps.AddRange(identifierMaps);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<HrisExternalIdentifierMap>> GetIdentifierMapsAsync(Guid sourceProfileId, CancellationToken ct = default)
    {
        IReadOnlyList<HrisExternalIdentifierMap> result = _identifierMaps
            .Where(x => x.TenantId == _tenantId && x.SourceProfileId == sourceProfileId && !x.IsDeleted)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<HrisSyncCheckpoint> CreateSyncCheckpointAsync(HrisSyncCheckpoint checkpoint, CancellationToken ct = default)
    {
        _syncCheckpoints.Add(checkpoint);
        return Task.FromResult(checkpoint);
    }

    public Task<HrisSyncCheckpoint?> GetLatestSyncCheckpointAsync(Guid sourceProfileId, CancellationToken ct = default) =>
        Task.FromResult(_syncCheckpoints
            .Where(x => x.TenantId == _tenantId && x.SourceProfileId == sourceProfileId && !x.IsDeleted)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault());

    public Task<HrisSourceHealthSnapshot> CreateHealthSnapshotAsync(HrisSourceHealthSnapshot healthSnapshot, CancellationToken ct = default)
    {
        _healthSnapshots.Add(healthSnapshot);
        return Task.FromResult(healthSnapshot);
    }

    public Task<HrisSourceHealthSnapshot?> GetLatestHealthSnapshotAsync(Guid sourceProfileId, CancellationToken ct = default) =>
        Task.FromResult(_healthSnapshots
            .Where(x => x.TenantId == _tenantId && x.SourceProfileId == sourceProfileId && !x.IsDeleted)
            .OrderByDescending(x => x.ObservedAt)
            .FirstOrDefault());

    public void Add(HrisSourceProfile item) => _sourceProfiles.Add(item);

    public void AddIdentifierMap(HrisExternalIdentifierMap item) => _identifierMaps.Add(item);
}
