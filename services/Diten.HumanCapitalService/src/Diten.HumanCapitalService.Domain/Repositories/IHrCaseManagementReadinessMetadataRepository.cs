using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IHrCaseManagementReadinessMetadataRepository
{
    Task<IReadOnlyList<HrCaseManagementReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<HrCaseManagementReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(HrCaseManagementReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(HrCaseManagementReadinessMetadata metadata, CancellationToken ct);
}
