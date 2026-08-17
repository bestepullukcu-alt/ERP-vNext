using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITepAssociationMembershipRegistryRepository
{
    Task<IReadOnlyList<TepAssociationMembershipRegistry>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<TepAssociationMembershipRegistry?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TepAssociationMembershipRegistry registry, CancellationToken ct);
    Task UpdateAsync(TepAssociationMembershipRegistry registry, CancellationToken ct);
}
