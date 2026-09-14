using Diten.TalentEcosystemService.Domain.Entities;

namespace Diten.TalentEcosystemService.Domain.Repositories;

public interface IProfessionalReputationLedgerReadinessMetadataRepository
{
    Task<IReadOnlyList<ProfessionalReputationLedgerReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<ProfessionalReputationLedgerReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(ProfessionalReputationLedgerReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(ProfessionalReputationLedgerReadinessMetadata metadata, CancellationToken ct);
}
