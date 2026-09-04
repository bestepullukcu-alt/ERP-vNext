using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>Resolve an active account by its AccountCode (import lookup). Null when missing/soft-deleted.</summary>
    Task<Account?> GetByCodeAsync(Guid tenantId, string accountCode, CancellationToken cancellationToken);

    Task<bool> ExistsByCodeAsync(Guid tenantId, string accountCode, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>Server-side paged list. <paramref name="sortBy"/> accepts "accountName"/"accountCode" (both
    /// backed by a {TenantId, field} index so descending stays an index scan, never a 32MB in-memory sort);
    /// any other value falls back to AccountName ascending. Returns the filtered <c>Total</c> plus the
    /// tenant-wide <c>UnfilteredTotal</c> (search ignored) for DataTables recordsTotal.
    /// <para><paramref name="accountIdScope"/> is the MOD-0151 territory-coverage account-id constraint (the grid's
    /// Territory Node / Country Scope chips, resolved to current-coverage account ids in the handler). Null skips the
    /// predicate entirely; a non-null but EMPTY set means "nothing matched the coverage filter" and yields zero rows
    /// (while UnfilteredTotal stays the tenant-wide count).</para></summary>
    Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(
        Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
        IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes,
        IReadOnlyCollection<Guid>? accountIdScope, CancellationToken cancellationToken);

    Task<IReadOnlyList<Account>> GetChildrenAsync(Guid tenantId, Guid parentId, CancellationToken cancellationToken);

    /// <summary>Walks the parent chain from <paramref name="candidateParentId"/> to detect whether linking it under
    /// <paramref name="accountId"/> would create a cycle (candidate is the account itself or one of its descendants).</summary>
    Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid accountId, Guid candidateParentId, CancellationToken cancellationToken);

    Task InsertAsync(Account account, CancellationToken cancellationToken);

    Task UpdateAsync(Account account, CancellationToken cancellationToken);
}
