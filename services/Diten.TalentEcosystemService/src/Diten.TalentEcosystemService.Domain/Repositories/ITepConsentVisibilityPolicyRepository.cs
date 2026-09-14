using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITepConsentVisibilityPolicyRepository
{
    Task<IReadOnlyList<TepConsentVisibilityPolicy>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<TepConsentVisibilityPolicy?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TepConsentVisibilityPolicy policy, CancellationToken ct);
    Task UpdateAsync(TepConsentVisibilityPolicy policy, CancellationToken ct);
}
