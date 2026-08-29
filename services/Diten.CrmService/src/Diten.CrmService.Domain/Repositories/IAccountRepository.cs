using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    /// <summary>Resolve an active account by its AccountCode (import lookup). Null when missing/soft-deleted.</summary>
    Task<Account?> GetByCodeAsync(Guid tenantId, string accountCode, CancellationToken cancellationToken);

    Task<bool> ExistsByCodeAsync(Guid tenantId, string accountCode, Guid? excludeId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<Account> Items, long Total)> ListAsync(
        Guid tenantId, string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<Account>> GetChildrenAsync(Guid tenantId, Guid parentId, CancellationToken cancellationToken);

    /// <summary>Walks the parent chain from <paramref name="candidateParentId"/> to detect whether linking it under
    /// <paramref name="accountId"/> would create a cycle (candidate is the account itself or one of its descendants).</summary>
    Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid accountId, Guid candidateParentId, CancellationToken cancellationToken);

    Task InsertAsync(Account account, CancellationToken cancellationToken);

    Task UpdateAsync(Account account, CancellationToken cancellationToken);
}
