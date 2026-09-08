using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IHiringRiskIndicatorsReadinessMetadataRepository
{
    Task<IReadOnlyList<HiringRiskIndicatorsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<HiringRiskIndicatorsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(HiringRiskIndicatorsReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(HiringRiskIndicatorsReadinessMetadata metadata, CancellationToken ct);
}
