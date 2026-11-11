using Diten.Platform.Domain.Entities.RecordCodes;

namespace Diten.Platform.Domain.Repositories;

// MOD-0040-FU02 §4 — the three record-code stores. Every read and write takes the tenant as an explicit parameter and
// filters on TenantId == tenantId EXACTLY; a call with Guid.Empty, from the platform context, or for a tenant other than
// the context's throws RecordCodeTenantPredicateViolationException and touches nothing. No update, replace or delete of
// an issued code exists anywhere.

public interface IRecordCodeCounterRepository
{
    /// <summary>Atomic <c>$inc</c> by one (upsert), outside any transaction; returns the number now taken.</summary>
    Task<long> NextAsync(Guid tenantId, string prefix, string recordType, CancellationToken cancellationToken = default);
}

public interface IRecordCodeAllocationRepository
{
    /// <summary>
    /// Inside the allocation's transaction. An E11000 on the idempotency index becomes
    /// <c>RecordCodeIdempotencyConflictException</c> (the E11000 as inner exception); any other error is left as it is.
    /// </summary>
    Task InsertAsync(
        IPlatformTransactionSession session,
        Guid tenantId,
        RecordCodeAllocation allocation,
        CancellationToken cancellationToken = default);

    Task<RecordCodeAllocation?> FindByIdempotencyKeyAsync(
        Guid tenantId,
        string callerService,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<RecordCodeAllocation?> FindByIdempotencyKeyAsync(
        IPlatformTransactionSession session,
        Guid tenantId,
        string callerService,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>The prefix of this company's latest LegalEntity allocation, or null when it has none.</summary>
    Task<string?> FindLatestPrefixForLegalEntityAsync(
        Guid tenantId,
        Guid legalEntityId,
        CancellationToken cancellationToken = default);

    /// <summary>Whether another company's LegalEntity allocation in this tenant already carries <paramref name="prefix"/>.</summary>
    Task<bool> ExistsPrefixForOtherLegalEntityAsync(
        Guid tenantId,
        string prefix,
        Guid legalEntityId,
        CancellationToken cancellationToken = default);
}

public interface IRecordCodeGroupPrefixRepository
{
    Task<RecordCodeGroupPrefix?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<RecordCodeGroupPrefix?> GetAsync(
        IPlatformTransactionSession session,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inside the alignment's transaction: writes <paramref name="groupPrefix"/> only while the setting is unlocked (or
    /// absent). Returns what happened; a locked row is never changed.
    /// </summary>
    Task<RecordCodeGroupPrefixAlignment> AlignIfUnlockedAsync(
        IPlatformTransactionSession session,
        Guid tenantId,
        string groupPrefix,
        string alignedBy,
        DateTime alignedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inside the allocation's transaction: <c>UpdateOne</c> filtered on (TenantId, GroupPrefix == <paramref name="prefix"/>)
    /// that writes <c>LockedAtUtc</c> only when it is empty (<c>$ifNull</c>). <see cref="RecordCodeGroupPrefixLockResult.Matched"/>
    /// = MatchedCount == 1; <see cref="RecordCodeGroupPrefixLockResult.LockedNow"/> = ModifiedCount == 1.
    /// </summary>
    Task<RecordCodeGroupPrefixLockResult> LockAsync(
        IPlatformTransactionSession session,
        Guid tenantId,
        string prefix,
        DateTime lockedAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed record RecordCodeGroupPrefixLockResult(bool Matched, bool LockedNow);

public enum RecordCodeGroupPrefixAlignment
{
    Inserted = 1,
    Updated = 2,
    Unchanged = 3,
    Locked = 4
}
