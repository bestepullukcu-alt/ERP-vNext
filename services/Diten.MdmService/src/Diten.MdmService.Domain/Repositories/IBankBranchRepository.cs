using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

/// <summary>A branch list request of one bank (tenant and not-deleted filters always apply).</summary>
public sealed record BankBranchListCriteria(
    Guid BankId,
    string? Search,
    BankLifecycleStatus? Status,
    int Page,
    int PageSize);

public sealed record BankBranchPage(IReadOnlyList<BankBranch> Items, long Total);

/// <summary>The branch store (<c>mdm_bank_branches</c>); same rules as <see cref="IBankRepository"/>.</summary>
public interface IBankBranchRepository
{
    Task<BankBranch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BankBranchPage> ListAsync(BankBranchListCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>Inserts the branch; its <see cref="BankBranch.AuditIntents"/> holds exactly the creation intent.</summary>
    Task<BankStoreOutcome> CreateAsync(BankBranch branch, CancellationToken cancellationToken = default);

    Task<BankStoreOutcome> UpdateAsync(
        BankBranch branch, int expectedVersion, LocalAuditIntent intent, CancellationToken cancellationToken = default);

    Task<BankStoreOutcome> SoftDeleteAsync(
        BankBranch branch, int expectedVersion, LocalAuditIntent intent, CancellationToken cancellationToken = default);
}
