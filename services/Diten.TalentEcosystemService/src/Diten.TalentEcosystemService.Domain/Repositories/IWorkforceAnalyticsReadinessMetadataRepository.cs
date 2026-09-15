using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IWorkforceAnalyticsReadinessMetadataRepository
{
    Task<IReadOnlyList<WorkforceAnalyticsReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<WorkforceAnalyticsReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(WorkforceAnalyticsReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(WorkforceAnalyticsReadinessMetadata metadata, CancellationToken ct);
}
