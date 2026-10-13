using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Enums;

namespace Diten.InventoryService.Domain.Expiry.Repositories;

public sealed record ExpiryExceptionQuery(
    ExpiryExceptionType? Type,
    IReadOnlyCollection<ExpiryExceptionStatus>? Statuses,
    bool? Late,
    DateTimeOffset Now,
    string? WarehouseId,
    Guid? AssignedTo,
    string? Search,
    bool SortByDue,
    int Page,
    int PageSize);

public sealed record ExpiryExceptionPage(IReadOnlyList<FefoExceptionCase> Items, long Total);

public sealed record ExpiryPolicyHistoryPage(IReadOnlyList<ExpiryPolicyHistoryEntry> Items, long Total);

/// <summary>A write refused by a unique index (a create key, the active-case key, a once-only event, a reused signature id).</summary>
public sealed class ExpiryDuplicateException(string index) : Exception($"Duplicate on {index}.")
{
    public string Index { get; }  = index;
}

/// <summary>
/// MOD-0176 records (pack §3). Every member is bound to the caller's resolved Tenant + LegalEntity — there is no overload without
/// the scope, so a company's policy or case does not exist for another company (403 / 404 decided by the caller). Changes go
/// through <see cref="InTransactionAsync{T}"/>: one Mongo transaction, all or nothing. Nothing is ever deleted.
/// </summary>
public interface IExpiryRepository
{
    Task<ExpiryPolicy?> FindPolicyAsync(CancellationToken cancellationToken = default);

    Task<ExpiryPolicyHistoryPage> PolicyHistoryAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<FefoExceptionCase?> FindCaseAsync(Guid caseId, CancellationToken cancellationToken = default);

    Task<FefoExceptionCase?> FindCaseByCreateKeyAsync(Guid openedBy, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<FefoExceptionCase?> FindActiveCaseByDedupKeyAsync(string dedupKey, CancellationToken cancellationToken = default);

    Task<ExpiryExceptionPage> ListCasesAsync(ExpiryExceptionQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FefoExceptionEvent>> EventsAsync(Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>The caller's earlier event on this case with this idempotency key, if any.</summary>
    Task<FefoExceptionEvent?> FindEventByKeyAsync(Guid caseId, Guid actor, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>The caller's earlier policy save with this idempotency key (its audit row), if any.</summary>
    Task<ExpiryAuditRecord?> FindPolicySaveAsync(Guid userId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>V23 pattern: the id names a case of ANOTHER company of the caller's tenant (→ 403, not 404).</summary>
    Task<bool> ExistsInAnotherCompanyAsync(Guid caseId, CancellationToken cancellationToken = default);

    /// <summary>F2 (KF-D): the caller's earlier prepared override with this idempotency key, if any.</summary>
    Task<LotDateOverride?> FindDateOverrideByKeyAsync(Guid createdBy, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>F3 (KF-D subject): one override of the caller's company by id (the provider and the gate read it).</summary>
    Task<LotDateOverride?> FindDateOverrideAsync(Guid overrideId, CancellationToken cancellationToken = default);

    Task<T> InTransactionAsync<T>(Func<IExpiryWriteSession, CancellationToken, Task<T>> work, CancellationToken cancellationToken = default);
}

/// <summary>
/// The writes of one MOD-0176 transaction. History rows, case events and audit rows are INSERT-ONLY by shape: this session has an
/// insert for each and nothing that updates, replaces or deletes them (architecture-tested).
/// </summary>
public interface IExpiryWriteSession
{
    /// <summary>Next <c>FX-{yyyy}-{nnnn}</c> of the tenant (atomic counter).</summary>
    Task<string> NextNumberAsync(int year, CancellationToken cancellationToken = default);

    /// <summary>version 0 → insert the company's first row; otherwise replace the row of <paramref name="expectedVersion"/>.</summary>
    Task<bool> SavePolicyAsync(ExpiryPolicy policy, int expectedVersion, CancellationToken cancellationToken = default);

    Task InsertPolicyHistoryAsync(IReadOnlyList<ExpiryPolicyHistoryEntry> entries, CancellationToken cancellationToken = default);

    Task InsertCaseAsync(FefoExceptionCase exceptionCase, CancellationToken cancellationToken = default);

    Task<bool> ReplaceCaseAsync(FefoExceptionCase exceptionCase, int expectedVersion, CancellationToken cancellationToken = default);

    Task InsertEventAsync(FefoExceptionEvent exceptionEvent, CancellationToken cancellationToken = default);

    Task InsertAuditAsync(ExpiryAuditRecord record, CancellationToken cancellationToken = default);

    /// <summary>F2 (KF-D): a prepared lot-date override.</summary>
    Task InsertDateOverrideAsync(LotDateOverride dateOverride, CancellationToken cancellationToken = default);
}
