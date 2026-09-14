using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface ITepAssociationMembershipRegistryRepository
{
    Task<IReadOnlyList<TepAssociationMembershipRegistry>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<TepAssociationMembershipRegistry?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TepAssociationMembershipRegistry registry, CancellationToken ct);
    Task UpdateAsync(TepAssociationMembershipRegistry registry, CancellationToken ct);
}
