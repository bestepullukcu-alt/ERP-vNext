using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITalentDevelopmentNetworkReadinessMetadataRepository
{
    Task<IReadOnlyList<TalentDevelopmentNetworkReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<TalentDevelopmentNetworkReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TalentDevelopmentNetworkReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TalentDevelopmentNetworkReadinessMetadata metadata, CancellationToken ct);
}
