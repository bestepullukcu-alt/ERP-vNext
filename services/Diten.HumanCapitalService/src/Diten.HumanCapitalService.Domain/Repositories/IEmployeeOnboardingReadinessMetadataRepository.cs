using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IEmployeeOnboardingReadinessMetadataRepository
{
    Task<IReadOnlyList<EmployeeOnboardingReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<EmployeeOnboardingReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(EmployeeOnboardingReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(EmployeeOnboardingReadinessMetadata metadata, CancellationToken ct);
}
