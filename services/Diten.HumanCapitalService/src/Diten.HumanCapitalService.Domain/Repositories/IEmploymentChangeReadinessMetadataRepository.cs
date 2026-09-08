using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IEmploymentChangeReadinessMetadataRepository
{
    Task<IReadOnlyList<EmploymentChangeReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<EmploymentChangeReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(EmploymentChangeReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(EmploymentChangeReadinessMetadata metadata, CancellationToken ct);
}
