using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface ICompensationBenefitsReadinessMetadataRepository
{
    Task<IReadOnlyList<CompensationBenefitsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<CompensationBenefitsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(CompensationBenefitsReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(CompensationBenefitsReadinessMetadata metadata, CancellationToken ct);
}
