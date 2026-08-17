using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IOffboardingCaseRepository
{
    Task<IReadOnlyList<OffboardingCase>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<OffboardingCase?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(OffboardingCase offboardingCase, CancellationToken ct);
    Task UpdateAsync(OffboardingCase offboardingCase, CancellationToken ct);
}
