using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ISectorMobilityIntelligenceReadinessMetadataRepository
{
    Task<IReadOnlyList<SectorMobilityIntelligenceReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<SectorMobilityIntelligenceReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(SectorMobilityIntelligenceReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(SectorMobilityIntelligenceReadinessMetadata metadata, CancellationToken ct);
}
