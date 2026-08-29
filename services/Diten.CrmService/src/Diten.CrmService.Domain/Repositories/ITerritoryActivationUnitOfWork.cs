using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// Mongo transaction boundary for model activation. Prevents a model, its nodes and proposed resource assignments
/// from becoming partially operational.
///
/// <para>FU04B: the plan baseline snapshot is committed inside the SAME boundary, so a baseline can never survive an
/// activation that failed or rolled back (pack §22.4 D-FU04B-1).</para>
/// </summary>
public interface ITerritoryActivationUnitOfWork
{
    Task CommitAsync(
        TerritoryModel model,
        IReadOnlyCollection<TerritoryNode> nodes,
        IReadOnlyCollection<TerritoryResourceAssignment> resourceAssignments,
        TerritoryResourceAssignmentPlanSnapshot? planSnapshot,
        CancellationToken cancellationToken);

    /// <summary>Atomically supersedes a source model and carries its operational account coverage into the new version.</summary>
    Task CommitVersionCutoverAsync(
        TerritoryModel targetModel,
        IReadOnlyCollection<TerritoryNode> targetNodes,
        IReadOnlyCollection<TerritoryResourceAssignment> targetResourceAssignments,
        TerritoryResourceAssignmentPlanSnapshot? planSnapshot,
        TerritoryModel sourceModel,
        IReadOnlyCollection<TerritoryNode> sourceNodes,
        IReadOnlyCollection<AccountTerritoryAssignment> endedSourceAssignments,
        IReadOnlyCollection<AccountTerritoryAssignment> createdTargetAssignments,
        CancellationToken cancellationToken)
        => throw new NotSupportedException("Version cutover is not supported by this unit of work.");
}
