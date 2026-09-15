using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;

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

    /// <summary>MOD-0357 S4 (K11) — the bridge's own idempotency check, the twin of
    /// <see cref="IMeetingRepository.FindByIdempotencyKeyAsync"/>. `null` means this exact bridge request has
    /// never been processed for this tenant.</summary>
    Task<RecordLink?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);

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

// MOD-0357 S2 — the meeting aggregate's own repositories. Same live TenantRepository<T> execution filter as
// every repository above; UpdateAsync's ExpectedVersion pattern mirrors ITaskItemRepository.UpdateAsync exactly
// (FindOneAndReplaceAsync gated on both tenant+id+Version — a mismatch returns null, the handler turns that into
// a 409, and no compare-and-set logic is duplicated here).

/// <summary>Raw storage for <see cref="Meeting"/>.</summary>
public interface IMeetingRepository
{
    Task<Meeting> CreateAsync(Meeting meeting, CancellationToken ct = default);

    Task<Meeting?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>The idempotency check (K11) — a meeting already created for this exact key, if any.</summary>
    Task<Meeting?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);

    /// <summary>K11, race-safe — the same <c>FindOrCreateAsync</c> pattern <c>RecordLinkRepository</c> already
    /// established in S1: find by <see cref="Meeting.IdempotencyKey"/> first; on a genuine insert race the
    /// unique index (<c>PlatformSchemaManifest.Meetings.cs</c>) refuses the loser's write, which this method
    /// catches and turns into "return the winner" — never a 500.</summary>
    Task<Meeting> FindOrCreateAsync(Meeting candidate, CancellationToken ct = default);

    /// <summary>Optimistic-concurrency replace. Returns <c>false</c> (never throws) when <paramref name="expectedVersion"/>
    /// no longer matches the stored row — the caller turns that into a 409.</summary>
    Task<bool> UpdateAsync(Meeting meeting, int expectedVersion, CancellationToken ct = default);

    /// <summary>Every meeting in the tenant, unfiltered by visibility — the handler applies D3 (organizer ∨
    /// attendee ∨ read-all) afterward, once, in one place, rather than duplicating it per repository method.</summary>
    Task<IReadOnlyList<Meeting>> ListAsync(CancellationToken ct = default);

    /// <summary>Batched read for <c>MeetingRelatedRecordResolver</c> — one query for every id the
    /// registry asks for, never one query per id (the same discipline <c>ITaskItemRepository.ListByIdsAsync</c>
    /// already follows for the "tasks" side of the same registry).</summary>
    Task<IReadOnlyList<Meeting>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>Whether ANY live meeting still references this type — the "in use" check
    /// <c>DeleteMeetingTypeCommand</c> refuses on (pack §13, <c>MEETING_TYPE_IN_USE</c>), without loading every
    /// meeting in the tenant just to answer one boolean.</summary>
    Task<bool> AnyByMeetingTypeIdAsync(Guid meetingTypeId, CancellationToken ct = default);

    /// <summary>
    /// MOD-0357 S7 (K6) — the REVERSE of <see cref="Meeting.FollowUpOfMeetingId"/>: which meeting (if any)
    /// continues this one. Derived, never stored — a meeting does not know its own successor, the same
    /// "read-only convenience over an existing field" shape <c>GetLinkedTasksHandler</c> already takes for
    /// `relatedRecords`. The EARLIEST match by <c>CreatedAt</c> when more than one exists (nothing in the pack
    /// forbids scheduling a second continuation by mistake; the first one scheduled is "the" follow-up for
    /// display purposes).
    /// </summary>
    Task<Meeting?> FindByFollowUpOfMeetingIdAsync(Guid meetingId, CancellationToken ct = default);
}

/// <summary>Raw storage for <see cref="MeetingAttendee"/>.</summary>
public interface IMeetingAttendeeRepository
{
    Task<MeetingAttendee> CreateAsync(MeetingAttendee attendee, CancellationToken ct = default);

    Task<IReadOnlyList<MeetingAttendee>> ListByMeetingIdAsync(Guid meetingId, CancellationToken ct = default);

    /// <summary>Every attendee row across several meetings, in ONE call — the batched read a list projection
    /// (visibility check, "am I an attendee of any of these") needs, never one query per meeting.</summary>
    Task<IReadOnlyList<MeetingAttendee>> ListByMeetingIdsAsync(IReadOnlyCollection<Guid> meetingIds, CancellationToken ct = default);

    Task<MeetingAttendee?> FindAsync(Guid meetingId, Guid userId, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>S5 — the respond endpoint's own write. No <c>expectedVersion</c>: K5's "same response twice is
    /// idempotent" needs no optimistic-concurrency token, since setting the SAME value twice is a no-op either
    /// way and there is no other writer of this one field to race against.</summary>
    Task UpdateInvitationResponseAsync(Guid id, InvitationResponse response, CancellationToken ct = default);

    /// <summary>S6 — the one-way sync <c>PublishMinutesCommand</c>/<c>CorrectPublishedMinutesCommand</c> write
    /// after a minutes version publishes: tutanak → katılımcı, never the other direction. No
    /// <c>expectedVersion</c>, same reasoning as <see cref="UpdateInvitationResponseAsync"/> — a re-publish of
    /// the SAME attendance value is a no-op, and nothing else writes this field.</summary>
    Task UpdateAttendanceStatusAsync(Guid meetingId, Guid userId, AttendanceStatus status, CancellationToken ct = default);

    /// <summary>S5c — <c>MeetingWorkItemProvider</c>'s own source query: every invitation still awaiting THIS
    /// user's Accept/Decline, across every meeting in the tenant. Filtered at the query, not in memory — an
    /// actor's own Pending set stays small regardless of how large the tenant's meeting history grows.</summary>
    Task<IReadOnlyList<MeetingAttendee>> ListPendingByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>BL-406 — the one write for <see cref="MeetingAttendee.MailUndeliveredAt"/>. No <c>expectedVersion</c>,
    /// same reasoning as <see cref="UpdateAttendanceStatusAsync"/>: nothing else writes this field, and re-marking
    /// an already-undelivered row with the same fact is a no-op either way. A no-op (false) return means no row
    /// matched this (meetingId, userId) — the caller logs and moves on, never throws.</summary>
    Task<bool> MarkMailUndeliveredAsync(Guid meetingId, Guid userId, DateTimeOffset failedAt, CancellationToken ct = default);
}

/// <summary>Raw storage for <see cref="AgendaItem"/>.</summary>
public interface IAgendaItemRepository
{
    Task<AgendaItem> CreateAsync(AgendaItem item, CancellationToken ct = default);

    Task<AgendaItem?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<AgendaItem>> ListByMeetingIdAsync(Guid meetingId, CancellationToken ct = default);

    Task<bool> UpdateAsync(AgendaItem item, int expectedVersion, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Raw storage for <see cref="MeetingType"/>.</summary>
public interface IMeetingTypeRepository
{
    Task<MeetingType> CreateAsync(MeetingType type, CancellationToken ct = default);

    Task<MeetingType?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<MeetingType>> ListAsync(CancellationToken ct = default);

    Task<MeetingType?> FindByNameAsync(string name, CancellationToken ct = default);

    Task<bool> UpdateAsync(MeetingType type, int expectedVersion, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Raw storage for <see cref="MeetingMinutesVersion"/> — MOD-0357 S6 (pack K4). APPEND-ONLY by convention of
/// its own callers, not by anything this interface enforces mechanically: <see cref="UpdateAsync"/> exists
/// because a DRAFT row is edited in place, and every command handler that calls it first confirms the row it is
/// about to replace is still <c>MinutesStatus.Draft</c> — this repository trusts the caller on that, the same
/// way <c>IAgendaItemRepository.UpdateAsync</c> trusts its own callers about which rows are still editable.
/// </summary>
public interface IMeetingMinutesVersionRepository
{
    /// <summary>
    /// Null when a concurrent request already created a row for the same (tenant, meeting, version number) —
    /// the unique index's own guarantee at the storage level (schema manifest), translated here into "no"
    /// rather than a 500 to the loser of a genuine race. Mirrors <c>IRecordLinkRepository.FindOrCreateAsync</c>'s
    /// own posture on WHERE a <c>MongoWriteException</c> may be caught (architecture rule: MongoDB Driver types
    /// stay in Persistence, never surface to a command handler).
    /// </summary>
    Task<MeetingMinutesVersion?> TryCreateAsync(MeetingMinutesVersion version, CancellationToken ct = default);

    /// <summary>The single highest <see cref="MeetingMinutesVersion.VersionNumber"/> for this meeting, or null
    /// if none exists yet — every command handler's own "what is the current state" read.</summary>
    Task<MeetingMinutesVersion?> GetLatestByMeetingIdAsync(Guid meetingId, CancellationToken ct = default);

    /// <summary>Every version for this meeting, <see cref="MeetingMinutesVersion.VersionNumber"/> descending —
    /// the editor's own history view (<c>GetMeetingMinutesQuery</c>).</summary>
    Task<IReadOnlyList<MeetingMinutesVersion>> ListByMeetingIdAsync(Guid meetingId, CancellationToken ct = default);

    /// <summary>
    /// S12 — every PUBLISHED version across a SET of meetings, batched — one query for the whole report, never
    /// one per meeting. A meeting may carry more than one published version (a correction after publish adds a
    /// new one, K4); the caller reduces to the highest <see cref="MeetingMinutesVersion.VersionNumber"/> per
    /// meeting, the same "group in memory over an already-narrow, already-indexed read" style
    /// <c>GetMeetingListHandler</c> already uses for attendees and linked tasks — a Meetings-scale read, not a
    /// hundred-thousand-row aggregation MOD-0024's own report needed.
    /// </summary>
    Task<IReadOnlyList<MeetingMinutesVersion>> ListPublishedByMeetingIdsAsync(
        IReadOnlyCollection<Guid> meetingIds, CancellationToken ct = default);

    /// <summary>Optimistic-concurrency replace, same shape as every other MOD-0024-adjacent
    /// <c>UpdateAsync(entity, expectedVersion)</c>. Callers use this ONLY on a row still
    /// <see cref="MeetingMinutesVersion.Status"/> <c>Draft</c> — publishing and correcting never call it (see
    /// the type's own doc comment on why a Published row is never replaced).</summary>
    Task<bool> UpdateAsync(MeetingMinutesVersion version, int expectedVersion, CancellationToken ct = default);
}

/// <summary>Raw storage for <see cref="MeetingSeries"/> (MOD-0357 S11).</summary>
public interface IMeetingSeriesRepository
{
    Task<MeetingSeries> CreateAsync(MeetingSeries series, CancellationToken ct = default);

    Task<MeetingSeries?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Every series the tenant can see, active or not — the management surface has to show a
    /// deactivated series (a series that vanishes when switched off could never be switched back on).</summary>
    Task<IReadOnlyList<MeetingSeries>> ListAllAsync(CancellationToken ct = default);

    /// <summary>Only <see cref="MeetingSeries.IsActive"/> rows, not-deleted — the sweep's own read (KS5).</summary>
    Task<IReadOnlyList<MeetingSeries>> ListActiveAsync(CancellationToken ct = default);

    Task<MeetingSeries?> FindByNameAsync(string name, CancellationToken ct = default);

    Task<bool> UpdateAsync(MeetingSeries series, int expectedVersion, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
