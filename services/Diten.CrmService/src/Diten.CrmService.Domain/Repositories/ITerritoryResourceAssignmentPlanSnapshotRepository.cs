using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// MOD-0151 FU04B plan baseline reads. Deliberately has NO update/delete member: the snapshot is immutable, and the
/// only write happens inside the activation unit of work (<see cref="ITerritoryActivationUnitOfWork"/>) so a baseline
/// can never exist for an activation that failed closed.
/// </summary>
public interface ITerritoryResourceAssignmentPlanSnapshotRepository
{
    /// <summary>Latest snapshot version for a model, or null when the model was never activated.</summary>
    Task<TerritoryResourceAssignmentPlanSnapshot?> GetLatestAsync(
        Guid tenantId, Guid modelId, CancellationToken cancellationToken);

    /// <summary>All snapshot versions for a model, newest first.</summary>
    Task<IReadOnlyList<TerritoryResourceAssignmentPlanSnapshot>> ListByModelAsync(
        Guid tenantId, Guid modelId, CancellationToken cancellationToken);

    /// <summary>Latest snapshot of every model that planned the given resource — feeds the resource-level view.</summary>
    Task<IReadOnlyList<TerritoryResourceAssignmentPlanSnapshot>> ListByResourceAsync(
        Guid tenantId, string resourceId, CancellationToken cancellationToken);
}
