using Diten.Platform.Domain.Entities.Organization;

namespace Diten.Platform.Domain.Repositories;

public interface IPositionAssignmentRepository
{
    Task<PositionAssignment> CreateAsync(PositionAssignment assignment, CancellationToken ct = default);
    Task<PositionAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PositionAssignment>> GetAllAsync(CancellationToken ct = default);
    Task<bool> HasOverlapAsync(Guid positionId, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateAsync(PositionAssignment assignment, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
