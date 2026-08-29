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
    Task InsertManyAsync(IReadOnlyCollection<AccountTerritoryAssignment> assignments, CancellationToken cancellationToken);
    Task UpdateManyAsync(IReadOnlyCollection<AccountTerritoryAssignment> assignments, CancellationToken cancellationToken);
    Task UpdateAsync(AccountTerritoryAssignment assignment, CancellationToken cancellationToken);
    Task CommitApplyAsync(
        IReadOnlyCollection<AccountTerritoryAssignment> ended,
        IReadOnlyCollection<AccountTerritoryAssignment> created,
        CancellationToken cancellationToken);
}
