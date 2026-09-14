using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITepTrustLevelPolicyMetadataRepository
{
    Task<IReadOnlyList<TepTrustLevelPolicyMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<TepTrustLevelPolicyMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TepTrustLevelPolicyMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TepTrustLevelPolicyMetadata metadata, CancellationToken ct);
}
