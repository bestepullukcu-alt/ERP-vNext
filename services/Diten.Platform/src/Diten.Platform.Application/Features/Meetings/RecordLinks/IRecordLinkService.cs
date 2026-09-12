using Diten.Platform.Domain.Entities.Meetings;

namespace Diten.Platform.Application.Features.Meetings.RecordLinks;

/// <summary>
/// MOD-0357 pack §3 — the ONE place that reads/writes <see cref="RecordLink"/>. MOD-0024's `relatedRecords`
/// read (batched, see <c>TaskWorkItemProvider</c>) and MOD-0357's own "linked tasks" list both call
/// <see cref="ListBySourceAsync"/>/<see cref="ListByTargetAsync"/>, so the query shape cannot drift between the
/// two consumers — the same reasoning <c>ITaskAssignmentGuard</c> already applies to "who may I hand work to".
/// </summary>
public interface IRecordLinkService
{
    /// <summary>
    /// Idempotent by the six values that identify a link (tenant is server-side, so five here): the SAME
    /// (source, target, linkType) triple returns the EXISTING row rather than writing a second one. A
    /// DIFFERENT <paramref name="linkType"/> between the same two records is a different link and writes a
    /// new row — the pack's own example is a task that is both `preparation` for a meeting and later
    /// `bornFromMeeting` from it; those are two facts, not one.
    /// </summary>
    Task<RecordLink> AddLinkAsync(
        RecordLinkEndpoint source, RecordLinkEndpoint target, string linkType,
        string? idempotencyKey = null, bool createdAfterMinutesPublished = false, CancellationToken ct = default);

    /// <summary>Soft-deletes one link by id. Never cascades to either endpoint's own record (K1).</summary>
    Task RemoveLinkAsync(Guid linkId, CancellationToken ct = default);

    /// <summary>MOD-0357 S4 (K11) — the bridge's own idempotency check; see
    /// <see cref="Diten.Platform.Domain.Entities.Meetings.RecordLink.IdempotencyKey"/>.</summary>
    Task<RecordLink?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);

    /// <summary>MOD-0357 S4 — the exact row this (source, target, linkType) triple would produce, if one
    /// already exists. Used where an ALREADY-linked pair must be reported (409), not silently treated as a
    /// no-op success the way <see cref="AddLinkAsync"/>'s own idempotency does for a genuine retry.</summary>
    Task<RecordLink?> FindLinkAsync(
        RecordLinkEndpoint source, RecordLinkEndpoint target, string linkType, CancellationToken ct = default);

    /// <summary>Every live link where any of these ids is the SOURCE, batched — one query for the whole page,
    /// never one per row.</summary>
    Task<IReadOnlyList<RecordLink>> ListBySourceAsync(
        IReadOnlyCollection<Guid> sourceRecordIds, CancellationToken ct = default);

    /// <summary>The TARGET-side twin of <see cref="ListBySourceAsync"/>.</summary>
    Task<IReadOnlyList<RecordLink>> ListByTargetAsync(
        IReadOnlyCollection<Guid> targetRecordIds, CancellationToken ct = default);
}
