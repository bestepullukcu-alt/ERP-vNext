using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ISectorTalentTrendsReadinessMetadataRepository
{
    Task<IReadOnlyList<SectorTalentTrendsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<SectorTalentTrendsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(SectorTalentTrendsReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(SectorTalentTrendsReadinessMetadata metadata, CancellationToken ct);
}
