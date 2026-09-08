using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITalentDevelopmentNetworkReadinessMetadataRepository
{
    Task<IReadOnlyList<TalentDevelopmentNetworkReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<TalentDevelopmentNetworkReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TalentDevelopmentNetworkReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TalentDevelopmentNetworkReadinessMetadata metadata, CancellationToken ct);
}
