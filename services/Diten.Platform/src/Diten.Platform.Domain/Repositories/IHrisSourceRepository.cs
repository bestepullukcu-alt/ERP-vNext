using Diten.Platform.Domain.Entities.HrisSources;

namespace Diten.Platform.Domain.Repositories;

public interface IHrisSourceRepository
{
    Task<HrisSourceProfile> CreateSourceProfileAsync(HrisSourceProfile sourceProfile, CancellationToken ct = default);
    Task<HrisSourceProfile?> GetSourceProfileByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<HrisSourceProfile>> GetSourceProfilesAsync(CancellationToken ct = default);
    Task<bool> ExistsActiveSourceCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateSourceProfileAsync(HrisSourceProfile sourceProfile, CancellationToken ct = default);
    Task<bool> ArchiveSourceProfileAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasActiveIdentifierMapsAsync(Guid sourceProfileId, CancellationToken ct = default);
    Task<HrisMappingProfile?> GetActiveMappingProfileAsync(Guid sourceProfileId, CancellationToken ct = default);
    Task UpsertMappingProfileAsync(HrisMappingProfile mappingProfile, CancellationToken ct = default);
    Task ReplaceIdentifierMapsAsync(Guid sourceProfileId, IReadOnlyList<HrisExternalIdentifierMap> identifierMaps, CancellationToken ct = default);
    Task<IReadOnlyList<HrisExternalIdentifierMap>> GetIdentifierMapsAsync(Guid sourceProfileId, CancellationToken ct = default);
    Task<HrisSyncCheckpoint> CreateSyncCheckpointAsync(HrisSyncCheckpoint checkpoint, CancellationToken ct = default);
    Task<HrisSyncCheckpoint?> GetLatestSyncCheckpointAsync(Guid sourceProfileId, CancellationToken ct = default);
    Task<HrisSourceHealthSnapshot> CreateHealthSnapshotAsync(HrisSourceHealthSnapshot healthSnapshot, CancellationToken ct = default);
    Task<HrisSourceHealthSnapshot?> GetLatestHealthSnapshotAsync(Guid sourceProfileId, CancellationToken ct = default);
}
