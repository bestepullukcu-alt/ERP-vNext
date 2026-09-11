using Diten.Platform.Domain.Entities.Meetings;

namespace Diten.Platform.Domain.Repositories;

// MOD-0357 S1 — the one bridge's repository seam. All reads go through the live TenantRepository<T> execution
// filter (tenant + IsDeleted), so a cross-tenant read returns empty with no metadata leak, exactly as every
// other Platform repository already does.

/// <summary>
/// Raw storage for <see cref="RecordLink"/>. Callers that need the "same six values twice does not duplicate"
/// rule (K11-adjacent idempotency) or the "read both directions in one call" rule (batched `relatedRecords`)
/// go through <c>IRecordLinkService</c>, not this interface directly — this is the persistence seam, not the
/// business rule.
/// </summary>
public interface IRecordLinkRepository
{
    Task<RecordLink> CreateAsync(RecordLink link, CancellationToken ct = default);

    Task<RecordLink?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The exact row this six-value combination would produce, if one already exists — the idempotency check.
    /// `null` means no such row exists yet.
    /// </summary>
    Task<RecordLink?> FindAsync(
        string sourceModuleCode, Guid sourceRecordId,
        string targetModuleCode, Guid targetRecordId,
        string linkType, CancellationToken ct = default);

    /// <summary>
    /// K11, race-safe: finds the existing row for <paramref name="candidate"/>'s six values, or inserts it.
    /// TWO concurrent callers finding nothing must not both insert — the unique compound index
    /// (<c>PlatformSchemaManifest.Meetings.cs</c>) is the guarantee, and the DUPLICATE-KEY WRITE this method
    /// catches is what turns that guarantee into "return the winner" instead of a 500 to the loser. This is
    /// the one place Mongo's own exception type is allowed to be caught — the Application-layer service never
    /// sees it (architecture rule: MongoDB Driver types stay in Persistence).
    /// </summary>
    Task<RecordLink> FindOrCreateAsync(RecordLink candidate, CancellationToken ct = default);

    /// <summary>
    /// Every live link where any of these ids is the SOURCE, in one read — never one query per id. Used by both
    /// MOD-0357's own "linked tasks" list and MOD-0024's batched `relatedRecords` read, so the two consumers
    /// cannot drift on the query shape (pack §3, `IRecordLinkService`'s own doc-comment).
    /// </summary>
    Task<IReadOnlyList<RecordLink>> ListBySourceAsync(IReadOnlyCollection<Guid> sourceRecordIds, CancellationToken ct = default);

    /// <summary>The TARGET-side twin of <see cref="ListBySourceAsync"/> — same batching rule, opposite direction.</summary>
    Task<IReadOnlyList<RecordLink>> ListByTargetAsync(IReadOnlyCollection<Guid> targetRecordIds, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
