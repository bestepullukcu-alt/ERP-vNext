using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>Atomic persistence boundary for a versioned territory draft clone.</summary>
public interface ITerritoryDraftCloneUnitOfWork
{
    Task CommitAsync(
        TerritoryModel model,
        IReadOnlyCollection<TerritoryNode> nodes,
        IReadOnlyCollection<TerritoryAssignmentRule> rules,
        CancellationToken cancellationToken);
}
