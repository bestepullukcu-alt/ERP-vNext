using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IVerifiedCertificationRegistryReadinessMetadataRepository
{
    Task<IReadOnlyList<VerifiedCertificationRegistryReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<VerifiedCertificationRegistryReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(VerifiedCertificationRegistryReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(VerifiedCertificationRegistryReadinessMetadata metadata, CancellationToken ct);
}
