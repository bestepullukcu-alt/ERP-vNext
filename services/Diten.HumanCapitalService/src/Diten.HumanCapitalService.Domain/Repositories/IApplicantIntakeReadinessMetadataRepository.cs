using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IApplicantIntakeReadinessMetadataRepository
{
    Task<IReadOnlyList<ApplicantIntakeReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<ApplicantIntakeReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(ApplicantIntakeReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(ApplicantIntakeReadinessMetadata metadata, CancellationToken ct);
}
