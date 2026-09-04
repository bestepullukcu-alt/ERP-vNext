using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

public interface IAccountTerritoryAssignmentRepository
{
    Task<AccountTerritoryAssignment?> GetByIdAsync(Guid tenantId, Guid modelId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccountTerritoryAssignment>> ListByModelAsync(Guid tenantId, Guid modelId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccountTerritoryAssignment>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken cancellationToken);

    /// <summary>Active assignments for a set of accounts (list-grid enrichment). Filters on status only — the
    /// effective-window check is done in memory by the caller so DateTimeOffset (BSON array) never enters a Mongo
    /// range filter (avoids the parallel-array / instant-vs-date pitfalls).</summary>
    Task<IReadOnlyList<AccountTerritoryAssignment>> ListActiveByAccountIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken);

    /// <summary>Active assignments (status only) whose <c>TerritoryNodeId</c> is one of <paramref name="nodeIds"/> —
    /// the coverage-filter candidate set for the Accounts-grid Territory Node chip. Same rule as
    /// <see cref="ListActiveByAccountIdsAsync"/>: the status equality is pushed to Mongo, the effective-window and the
    /// owning-model gate stay in memory (via <c>TerritoryCoverageLifecyclePolicy</c>) so DateTimeOffset (BSON array)
    /// never enters a Mongo range filter.</summary>
    Task<IReadOnlyList<AccountTerritoryAssignment>> ListActiveByNodesAsync(Guid tenantId, IReadOnlyCollection<Guid> nodeIds, CancellationToken cancellationToken);

    /// <summary>Active assignments (status only) whose <c>TerritoryModelId</c> is one of <paramref name="modelIds"/> —
    /// the coverage-filter candidate set for the Accounts-grid Country Scope chip (the models are pre-resolved from the
    /// scope). Same in-memory-gate rule as <see cref="ListActiveByNodesAsync"/>.</summary>
    Task<IReadOnlyList<AccountTerritoryAssignment>> ListActiveByModelIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> modelIds, CancellationToken cancellationToken);

    Task InsertManyAsync(IReadOnlyCollection<AccountTerritoryAssignment> assignments, CancellationToken cancellationToken);
    Task UpdateManyAsync(IReadOnlyCollection<AccountTerritoryAssignment> assignments, CancellationToken cancellationToken);
    Task UpdateAsync(AccountTerritoryAssignment assignment, CancellationToken cancellationToken);
    Task CommitApplyAsync(
        IReadOnlyCollection<AccountTerritoryAssignment> ended,
        IReadOnlyCollection<AccountTerritoryAssignment> created,
        CancellationToken cancellationToken);
}
