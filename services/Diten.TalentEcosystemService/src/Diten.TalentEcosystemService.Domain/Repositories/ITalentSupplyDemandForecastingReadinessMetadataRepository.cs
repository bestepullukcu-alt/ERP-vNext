using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITalentSupplyDemandForecastingReadinessMetadataRepository
{
    Task<IReadOnlyList<TalentSupplyDemandForecastingReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<TalentSupplyDemandForecastingReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TalentSupplyDemandForecastingReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TalentSupplyDemandForecastingReadinessMetadata metadata, CancellationToken ct);
}
