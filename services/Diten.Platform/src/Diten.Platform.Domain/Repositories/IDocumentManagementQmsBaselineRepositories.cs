using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

public interface IBaselineReleaseRepository
{
    Task<BaselineRelease> CreateAsync(BaselineRelease baseline, CancellationToken ct = default);
    Task<BaselineRelease?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BaselineRelease>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Version-guarded replace; returns false when the stored technical version no longer matches.</summary>
    Task<bool> UpdateAsync(BaselineRelease baseline, int expectedVersion, CancellationToken ct = default);
}

public interface ICollectionDefinitionRepository
{
    Task<CollectionDefinition> CreateAsync(CollectionDefinition definition, CancellationToken ct = default);
    Task CreateManyAsync(IReadOnlyList<CollectionDefinition> definitions, CancellationToken ct = default);
    Task<IReadOnlyList<CollectionDefinition>> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default);
    Task<CollectionDefinition?> GetByCanonicalIdAsync(Guid baselineReleaseId, string canonicalId, CancellationToken ct = default);
    Task<bool> UpdateAsync(CollectionDefinition definition, int expectedVersion, CancellationToken ct = default);
    Task UpdateManyAsync(IReadOnlyList<CollectionDefinition> definitions, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(CollectionDefinition definition, int expectedVersion, CancellationToken ct = default);
}

public interface IBaselineSnapshotManifestRepository
{
    Task<BaselineSnapshotManifest> CreateAsync(BaselineSnapshotManifest manifest, CancellationToken ct = default);
    Task<BaselineSnapshotManifest?> GetByBaselineAsync(Guid baselineReleaseId, CancellationToken ct = default);
}
