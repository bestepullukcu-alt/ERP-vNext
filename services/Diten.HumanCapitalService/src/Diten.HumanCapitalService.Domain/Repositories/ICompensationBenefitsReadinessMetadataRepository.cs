using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface ICompensationBenefitsReadinessMetadataRepository
{
    Task<IReadOnlyList<CompensationBenefitsReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<CompensationBenefitsReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(CompensationBenefitsReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(CompensationBenefitsReadinessMetadata metadata, CancellationToken ct);
}
