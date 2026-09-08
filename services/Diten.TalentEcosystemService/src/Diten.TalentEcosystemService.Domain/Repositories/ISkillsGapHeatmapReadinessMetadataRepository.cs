using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ISkillsGapHeatmapReadinessMetadataRepository
{
    Task<IReadOnlyList<SkillsGapHeatmapReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<SkillsGapHeatmapReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(SkillsGapHeatmapReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(SkillsGapHeatmapReadinessMetadata metadata, CancellationToken ct);
}
