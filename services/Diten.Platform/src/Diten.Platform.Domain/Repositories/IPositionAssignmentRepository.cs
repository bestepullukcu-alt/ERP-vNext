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

    // WP-CL-BE-3 — the assignments of these positions only (tenant scoped, non-deleted), instead of the whole table.
    // Default = in-memory filter for test doubles; the Mongo repository runs an indexed $in on PositionId.
    async Task<IReadOnlyList<PositionAssignment>> GetByPositionIdsAsync(
        IReadOnlyCollection<Guid> positionIds, CancellationToken ct = default) =>
        (await GetAllAsync(ct)).Where(x => positionIds.Contains(x.PositionId)).ToList();
}
