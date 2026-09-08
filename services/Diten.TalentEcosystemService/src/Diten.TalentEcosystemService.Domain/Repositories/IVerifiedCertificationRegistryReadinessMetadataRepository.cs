using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IVerifiedCertificationRegistryReadinessMetadataRepository
{
    Task<IReadOnlyList<VerifiedCertificationRegistryReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<VerifiedCertificationRegistryReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(VerifiedCertificationRegistryReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(VerifiedCertificationRegistryReadinessMetadata metadata, CancellationToken ct);
}
