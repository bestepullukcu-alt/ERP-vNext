using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ISectorTalentTrendsReadinessMetadataRepository
{
    Task<IReadOnlyList<SectorTalentTrendsReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<SectorTalentTrendsReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(SectorTalentTrendsReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(SectorTalentTrendsReadinessMetadata metadata, CancellationToken ct);
}
