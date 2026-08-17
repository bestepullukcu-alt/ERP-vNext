using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface IPositionAssignmentOverlayRepository
{
    Task<IReadOnlyList<EmployeePositionAssignmentOverlay>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<EmployeePositionAssignmentOverlay?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(EmployeePositionAssignmentOverlay assignment, CancellationToken ct);
    Task UpdateAsync(EmployeePositionAssignmentOverlay assignment, CancellationToken ct);
}
