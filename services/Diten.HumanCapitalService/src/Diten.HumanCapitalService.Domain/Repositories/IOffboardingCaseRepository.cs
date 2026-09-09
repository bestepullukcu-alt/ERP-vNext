using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IOffboardingCaseRepository
{
    Task<IReadOnlyList<OffboardingCase>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<OffboardingCase?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(OffboardingCase offboardingCase, CancellationToken ct);
    Task UpdateAsync(OffboardingCase offboardingCase, CancellationToken ct);
}
