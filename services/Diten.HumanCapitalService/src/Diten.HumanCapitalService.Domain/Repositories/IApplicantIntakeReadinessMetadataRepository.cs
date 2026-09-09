using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IApplicantIntakeReadinessMetadataRepository
{
    Task<IReadOnlyList<ApplicantIntakeReadinessMetadata>> ListAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds,
        CancellationToken ct);

    Task<ApplicantIntakeReadinessMetadata?> GetByIdAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> legalEntityIds,
        Guid id,
        CancellationToken ct);

    Task<bool> ExistsActiveCodeAsync(
        Guid tenantId,
        Guid legalEntityId,
        string code,
        Guid? excludingId,
        CancellationToken ct);

    Task CreateAsync(ApplicantIntakeReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(ApplicantIntakeReadinessMetadata metadata, CancellationToken ct);
}
