using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IEmployeeProjectionRepository
{
    Task<IReadOnlyList<EmployeeProfileProjection>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<EmployeeProfileProjection?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(EmployeeProfileProjection projection, CancellationToken ct);
    Task UpdateAsync(EmployeeProfileProjection projection, CancellationToken ct);
}
