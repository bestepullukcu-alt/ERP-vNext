using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

public interface ITerritoryResourceAssignmentRepository
{
    Task<TerritoryResourceAssignment?> GetByIdAsync(Guid tenantId, Guid modelId, Guid id, CancellationToken cancellationToken);

    /// <summary>All non-deleted assignments of a model, ordered by Position then valid-from.</summary>
    Task<IReadOnlyList<TerritoryResourceAssignment>> ListByModelAsync(Guid tenantId, Guid modelId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TerritoryResourceAssignment>> ListByResourceAsync(
        Guid tenantId, string resourceId, CancellationToken cancellationToken);

    Task InsertAsync(TerritoryResourceAssignment assignment, CancellationToken cancellationToken);

    Task UpdateAsync(TerritoryResourceAssignment assignment, CancellationToken cancellationToken);

    /// <summary>Atomically ends a source assignment and creates its replacement/transfer target.</summary>
    Task CommitLifecycleTransitionAsync(
        TerritoryResourceAssignment ended,
        TerritoryResourceAssignment created,
        CancellationToken cancellationToken);
}
