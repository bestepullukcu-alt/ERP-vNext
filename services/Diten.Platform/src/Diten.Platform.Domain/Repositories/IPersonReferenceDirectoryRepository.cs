using Diten.Platform.Domain.Entities.PersonReferenceDirectory;

namespace Diten.Platform.Domain.Repositories;

public interface IPersonReferenceDirectoryRepository
{
    Task<PersonReferenceProjection> CreateProjectionAsync(PersonReferenceProjection projection, CancellationToken ct = default);
    Task<PersonReferenceProjection?> GetProjectionByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PersonReferenceProjection>> GetProjectionsAsync(CancellationToken ct = default);
    Task<bool> ExistsActiveCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> ExistsActiveCorrelationKeyAsync(string correlationKey, Guid hrisSourceProfileId, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateProjectionAsync(PersonReferenceProjection projection, CancellationToken ct = default);
    Task<bool> ArchiveProjectionAsync(Guid id, CancellationToken ct = default);
    Task<PersonReferenceExternalCorrelation> UpsertCorrelationAsync(PersonReferenceExternalCorrelation correlation, CancellationToken ct = default);
    Task<PersonReferenceExternalCorrelation?> GetCorrelationByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PersonReferenceExternalCorrelation>> GetCorrelationsAsync(Guid projectionId, CancellationToken ct = default);
    Task<PersonReferenceDirectoryHealthSnapshot> CreateHealthSnapshotAsync(PersonReferenceDirectoryHealthSnapshot healthSnapshot, CancellationToken ct = default);
    Task<PersonReferenceDirectoryHealthSnapshot?> GetLatestHealthSnapshotAsync(CancellationToken ct = default);
}
