using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ISkillsGapHeatmapReadinessMetadataRepository
{
    Task<IReadOnlyList<SkillsGapHeatmapReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<SkillsGapHeatmapReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(SkillsGapHeatmapReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(SkillsGapHeatmapReadinessMetadata metadata, CancellationToken ct);
}
