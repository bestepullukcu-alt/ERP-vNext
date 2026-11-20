using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

/// <summary>
/// MOD-0289 DL-1 — what a bank or branch write did. Every write is ONE conditional write of the business document and
/// its audit intent together: a refused or failed write leaves neither.
/// </summary>
public enum BankStoreOutcome
{
    /// <summary>The document and its audit intent were written together.</summary>
    Written,

    /// <summary>
    /// The conditional write matched nothing: the document is gone, belongs to another tenant, is at another version, or is
    /// no longer in the state the write needs. The caller re-reads (tenant-scoped) to say which.
    /// </summary>
    Stale,

    /// <summary>ux_mdm_bank_bic: another bank of the tenant (not deleted) has this BIC.</summary>
    BicConflict,

    /// <summary>ux_mdm_bank_national_code: another bank of the tenant (not deleted) has this country + national code.</summary>
    NationalCodeConflict,

    /// <summary>ux_mdm_bank_branch_code: the bank already has a branch (not deleted) with this code.</summary>
    BranchCodeConflict,

    /// <summary>ux_mdm_bank_code: the tenant already holds this code — deleted banks included (RCS-001 §6).</summary>
    CodeConflict,

    /// <summary>
    /// The audit intent was missing / malformed, or the store could not write it: nothing was written (fail closed →
    /// 503 MDM_BANK_AUDIT_UNAVAILABLE).
    /// </summary>
    AuditIntentUnavailable,

    /// <summary>The collection's unique indexes are not in place (yet): nothing was written (503).</summary>
    IndexesNotReady
}

public enum BankListOrder
{
    Code,
    Name,
    CountryCode,
    Status,
    CreatedAt
}

/// <summary>A bank list request: every filter is applied together with the tenant and not-deleted filters.</summary>
public sealed record BankListCriteria(
    string? Search,
    BankLifecycleStatus? Status,
    string? CountryCode,
    BankListOrder Order,
    bool Descending,
    int Page,
    int PageSize);

public sealed record BankPage(IReadOnlyList<Bank> Items, long Total);

/// <summary>
/// The bank store (<c>mdm_banks</c>). Every method is tenant-scoped by the repository (the trusted tenant, never a
/// parameter). The write methods are the audited writers: each takes the audit intent that must land in the SAME write.
/// </summary>
public interface IBankRepository
{
    Task<Bank?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Bank>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task<BankPage> ListAsync(BankListCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>Inserts the bank; its <see cref="Bank.AuditIntents"/> holds exactly the creation intent.</summary>
    Task<BankStoreOutcome> CreateAsync(Bank bank, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update, Activate and Deactivate: writes the bank's mutable fields and appends <paramref name="intent"/>, only while the
    /// stored version is <paramref name="expectedVersion"/>; the version moves by one.
    /// </summary>
    Task<BankStoreOutcome> UpdateAsync(
        Bank bank, int expectedVersion, LocalAuditIntent intent, CancellationToken cancellationToken = default);

    /// <summary>Soft delete (IsDeleted + DeletedAt) of a Draft that was never activated, with its intent.</summary>
    Task<BankStoreOutcome> SoftDeleteAsync(
        Bank bank, int expectedVersion, LocalAuditIntent intent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft delete of several Drafts, all or none (one transaction): one conflicting document and nothing is deleted. Each
    /// item carries its own intent.
    /// </summary>
    Task<BankStoreOutcome> SoftDeleteManyAsync(
        IReadOnlyList<BankSoftDelete> items, CancellationToken cancellationToken = default);
}

public sealed record BankSoftDelete(Bank Bank, int ExpectedVersion, LocalAuditIntent Intent);
