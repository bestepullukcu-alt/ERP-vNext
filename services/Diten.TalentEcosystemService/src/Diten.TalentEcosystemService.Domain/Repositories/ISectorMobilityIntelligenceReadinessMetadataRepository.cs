using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ISectorMobilityIntelligenceReadinessMetadataRepository
{
    Task<IReadOnlyList<SectorMobilityIntelligenceReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<SectorMobilityIntelligenceReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(SectorMobilityIntelligenceReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(SectorMobilityIntelligenceReadinessMetadata metadata, CancellationToken ct);
}
