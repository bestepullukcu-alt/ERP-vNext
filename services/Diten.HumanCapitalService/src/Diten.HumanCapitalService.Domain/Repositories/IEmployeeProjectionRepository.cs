using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IEmployeeProjectionRepository
{
    Task<IReadOnlyList<EmployeeProfileProjection>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<EmployeeProfileProjection?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(EmployeeProfileProjection projection, CancellationToken ct);
    Task UpdateAsync(EmployeeProfileProjection projection, CancellationToken ct);
}
