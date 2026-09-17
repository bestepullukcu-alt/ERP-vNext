using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IHrCaseManagementReadinessMetadataRepository
{
    Task<IReadOnlyList<HrCaseManagementReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<HrCaseManagementReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(HrCaseManagementReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(HrCaseManagementReadinessMetadata metadata, CancellationToken ct);
}
