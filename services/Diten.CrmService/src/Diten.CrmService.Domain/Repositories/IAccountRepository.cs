using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>Resolve an active account by its AccountCode (import lookup). Null when missing/soft-deleted.</summary>
    Task<Account?> GetByCodeAsync(Guid tenantId, string accountCode, CancellationToken cancellationToken);

    Task<bool> ExistsByCodeAsync(Guid tenantId, string accountCode, Guid? excludeId, CancellationToken cancellationToken);

    /// <summary>WP-VP-2 (B-8) — the non-deleted accounts with these ids in ONE read (display names at read time). The
    /// default goes through the id-scoped <see cref="ListAsync"/>; the Mongo repository answers with a single
    /// <c>$in</c> find.</summary>
    async Task<IReadOnlyList<Account>> ListByIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids is null || ids.Count == 0)
        {
            return Array.Empty<Account>();
        }

        var page = await ListAsync(tenantId, null, 1, ids.Count, null, null, null, null, ids, cancellationToken);
        return page.Items;
    }

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

    /// <summary>WP-VP-2B (R2) — <see cref="ListAsync(Guid,string?,int,int,string?,string?,IReadOnlyCollection{string}?,IReadOnlyCollection{string}?,IReadOnlyCollection{Guid}?,CancellationToken)"/>
    /// plus an EXCLUSION set (<c>Id NIN</c>) — the "accounts without active contacts" filter. Everything else (search,
    /// chips, inclusion scope, paging, sorting, totals) is identical. The default only supports an empty exclusion;
    /// production applies it in the same query.</summary>
    Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(
        Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
        IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes,
        IReadOnlyCollection<Guid>? accountIdScope, IReadOnlyCollection<Guid>? excludedAccountIds,
        CancellationToken cancellationToken)
        => excludedAccountIds is null || excludedAccountIds.Count == 0
            ? ListAsync(tenantId, search, page, pageSize, sortBy, sortDir, statuses, accountTypes, accountIdScope, cancellationToken)
            : throw new NotSupportedException("This repository does not support an account-id exclusion.");

    /// <summary>WP-VP-2B (R3-a) — the distinct account-type codes of the tenant's accounts, optionally only of
    /// <paramref name="accountIdScope"/>. Production: one <c>distinct</c>.</summary>
    async Task<IReadOnlyList<string>> ListDistinctAccountTypesAsync(
        Guid tenantId, IReadOnlyCollection<Guid>? accountIdScope, CancellationToken cancellationToken)
    {
        var page = await ListAsync(tenantId, null, 1, int.MaxValue, null, null, null, null, accountIdScope, cancellationToken);
        return page.Items.Select(a => a.AccountType).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
    }

    Task<IReadOnlyList<Account>> GetChildrenAsync(Guid tenantId, Guid parentId, CancellationToken cancellationToken);

    /// <summary>Walks the parent chain from <paramref name="candidateParentId"/> to detect whether linking it under
    /// <paramref name="accountId"/> would create a cycle (candidate is the account itself or one of its descendants).</summary>
    Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid accountId, Guid candidateParentId, CancellationToken cancellationToken);

    Task InsertAsync(Account account, CancellationToken cancellationToken);

    Task UpdateAsync(Account account, CancellationToken cancellationToken);
}
