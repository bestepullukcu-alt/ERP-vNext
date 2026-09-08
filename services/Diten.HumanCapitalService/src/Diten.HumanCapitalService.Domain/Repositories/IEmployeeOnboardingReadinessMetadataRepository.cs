using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IEmployeeOnboardingReadinessMetadataRepository
{
    Task<IReadOnlyList<EmployeeOnboardingReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<EmployeeOnboardingReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(EmployeeOnboardingReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(EmployeeOnboardingReadinessMetadata metadata, CancellationToken ct);
}
