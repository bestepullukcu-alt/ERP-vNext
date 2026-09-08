using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IWorkforcePlanningReadinessMetadataRepository
{
    Task<IReadOnlyList<WorkforcePlanningReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<WorkforcePlanningReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(WorkforcePlanningReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(WorkforcePlanningReadinessMetadata metadata, CancellationToken ct);
}
