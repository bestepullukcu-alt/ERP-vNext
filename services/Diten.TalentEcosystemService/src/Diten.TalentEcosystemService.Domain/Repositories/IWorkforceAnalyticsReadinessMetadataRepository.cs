using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IWorkforceAnalyticsReadinessMetadataRepository
{
    Task<IReadOnlyList<WorkforceAnalyticsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<WorkforceAnalyticsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(WorkforceAnalyticsReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(WorkforceAnalyticsReadinessMetadata metadata, CancellationToken ct);
}
