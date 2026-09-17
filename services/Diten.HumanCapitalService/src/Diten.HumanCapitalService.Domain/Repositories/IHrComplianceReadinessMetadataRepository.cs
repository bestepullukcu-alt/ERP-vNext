using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IHrComplianceReadinessMetadataRepository
{
    Task<IReadOnlyList<HrComplianceReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<HrComplianceReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(HrComplianceReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(HrComplianceReadinessMetadata metadata, CancellationToken ct);
}
