using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IRestrictedIntegrityRegistryReadinessMetadataRepository
{
    Task<IReadOnlyList<RestrictedIntegrityRegistryReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<RestrictedIntegrityRegistryReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(RestrictedIntegrityRegistryReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(RestrictedIntegrityRegistryReadinessMetadata metadata, CancellationToken ct);
}
