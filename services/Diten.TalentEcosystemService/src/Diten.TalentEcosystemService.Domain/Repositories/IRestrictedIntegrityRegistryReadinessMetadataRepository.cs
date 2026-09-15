using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IRestrictedIntegrityRegistryReadinessMetadataRepository
{
    Task<IReadOnlyList<RestrictedIntegrityRegistryReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<RestrictedIntegrityRegistryReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(RestrictedIntegrityRegistryReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(RestrictedIntegrityRegistryReadinessMetadata metadata, CancellationToken ct);
}
