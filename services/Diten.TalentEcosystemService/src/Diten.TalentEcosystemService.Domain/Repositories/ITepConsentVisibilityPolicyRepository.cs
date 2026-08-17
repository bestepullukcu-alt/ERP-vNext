using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITepConsentVisibilityPolicyRepository
{
    Task<IReadOnlyList<TepConsentVisibilityPolicy>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<TepConsentVisibilityPolicy?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TepConsentVisibilityPolicy policy, CancellationToken ct);
    Task UpdateAsync(TepConsentVisibilityPolicy policy, CancellationToken ct);
}
