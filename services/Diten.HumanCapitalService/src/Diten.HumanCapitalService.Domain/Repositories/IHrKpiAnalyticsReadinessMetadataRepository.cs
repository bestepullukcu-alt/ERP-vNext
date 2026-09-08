using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IHrKpiAnalyticsReadinessMetadataRepository
{
    Task<IReadOnlyList<HrKpiAnalyticsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<HrKpiAnalyticsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(HrKpiAnalyticsReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(HrKpiAnalyticsReadinessMetadata metadata, CancellationToken ct);
}
