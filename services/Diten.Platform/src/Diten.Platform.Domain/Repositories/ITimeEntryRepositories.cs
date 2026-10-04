using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;

namespace Diten.Platform.Domain.Repositories;

// MOD-0280-FU01 (pack §3.1, §4) — the time-entry module's own storage seams. Every implementation sits on the live
// TenantRepository<T> base, so every read ANDs TenantId (from the server context) + IsDeleted=false and every insert
// stamps TenantId from the context: a cross-tenant id is simply "not found", with no metadata to leak.
//
// Duplicate-key races are turned into return values HERE (the MongoDB driver's exception type stays in Persistence,
// as the architecture rule requires) — a TryCreate that lost a race answers false, never a 500.

/// <summary>Raw storage for <see cref="TimesheetWeek"/> revisions.</summary>
public interface ITimesheetWeekRepository
{
    /// <summary>Inserts the revision. <c>false</c> when a unique index refused it (another request created the same
    /// revision number, or a second open revision, first).</summary>
    Task<bool> TryCreateAsync(TimesheetWeek week, CancellationToken ct = default);

    Task<TimesheetWeek?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<TimesheetWeek>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>Every live revision of one person's week, lowest revision first.</summary>
    Task<IReadOnlyList<TimesheetWeek>> ListRevisionsAsync(Guid userId, string weekKey, CancellationToken ct = default);

    /// <summary>Optimistic-concurrency replace. <c>false</c> (never throws) when the stored version moved, or when a
    /// unique index refused the new state — the caller turns either into a 409.</summary>
    Task<bool> UpdateAsync(TimesheetWeek week, int expectedVersion, CancellationToken ct = default);

    /// <summary>Submitted revisions MOD-0023 assigned to <paramref name="approverUserId"/>, oldest submission first.</summary>
    Task<IReadOnlyList<TimesheetWeek>> ListSubmittedForApproverAsync(Guid approverUserId, CancellationToken ct = default);

    /// <summary>BL-484 — the same rule as <see cref="ListSubmittedForApproverAsync"/>, narrowed to <paramref name="weekIds"/>:
    /// an id that is not a submitted revision assigned to <paramref name="approverUserId"/> is simply absent. One read,
    /// however long the approver's queue is.</summary>
    Task<IReadOnlyList<TimesheetWeek>> ListSubmittedForApproverByIdsAsync(
        Guid approverUserId, IReadOnlyCollection<Guid> weekIds, CancellationToken ct = default);

    /// <summary>The sweep's work list (BL-479): FIRST the approved revisions whose task totals were never applied (F12),
    /// then — with whatever is left of <paramref name="limit"/> — the submitted revisions that carry a MOD-0023 instance,
    /// oldest submission first. Two queries, in that order, so a backlog of undecided weeks can never starve an approved
    /// week of its totals.</summary>
    Task<IReadOnlyList<TimesheetWeek>> ListNeedingFinalizationAsync(int limit, CancellationToken ct = default);

    /// <summary>T3 (pack §21.3 N1) — the people with any revision, in any state, of any of these ISO weeks.</summary>
    Task<IReadOnlyList<Guid>> ListUserIdsWithWeeksAsync(IReadOnlyCollection<string> weekKeys, CancellationToken ct = default);
}

/// <summary>Raw storage for <see cref="TimeEntry"/> rows.</summary>
public interface ITimeEntryRepository
{
    Task<TimeEntry> CreateAsync(TimeEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<TimeEntry>> ListByWeekAsync(Guid timesheetWeekId, CancellationToken ct = default);

    /// <summary>BL-484 — <see cref="ListByWeekAsync"/> for several revisions in ONE read (the approvals page).</summary>
    Task<IReadOnlyList<TimeEntry>> ListByWeekIdsAsync(IReadOnlyCollection<Guid> timesheetWeekIds, CancellationToken ct = default);

    /// <summary>One live row, or null when it does not exist in this tenant or was removed.</summary>
    Task<TimeEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Every live row that points at one of these tasks, across all people and revisions — the finalizer's
    /// input for recomputing <see cref="TaskTimeTotal"/>.</summary>
    Task<IReadOnlyList<TimeEntry>> ListByTaskIdsAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default);

    /// <summary>Replaces a draft row's mutable fields (minutes, note). Tenant + id scoped; written only when the row in
    /// hand was read at <paramref name="expectedVersion"/> and is still stored at it (BL-533) — false otherwise.</summary>
    Task<bool> UpdateAsync(TimeEntry entry, int expectedVersion, CancellationToken ct = default);

    /// <summary>Soft-deletes rows (IsDeleted, UpdatedAt). Nothing in this module is hard-deleted.</summary>
    Task SoftDeleteAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}

/// <summary>Raw storage for <see cref="WorkCategory"/>.</summary>
public interface IWorkCategoryRepository
{
    /// <summary><c>false</c> when the tenant-unique code index refused the insert.</summary>
    Task<bool> TryCreateAsync(WorkCategory category, CancellationToken ct = default);

    Task<WorkCategory?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<WorkCategory?> GetByCodeAsync(string code, CancellationToken ct = default);

    Task<IReadOnlyList<WorkCategory>> ListAsync(CancellationToken ct = default);

    Task<bool> UpdateAsync(WorkCategory category, int expectedVersion, CancellationToken ct = default);
}

/// <summary>Raw storage for the one-per-tenant <see cref="TimeEntrySettings"/> row.</summary>
public interface ITimeEntrySettingsRepository
{
    Task<TimeEntrySettings?> GetAsync(CancellationToken ct = default);

    Task<bool> TryCreateAsync(TimeEntrySettings settings, CancellationToken ct = default);

    Task<bool> UpdateAsync(TimeEntrySettings settings, int expectedVersion, CancellationToken ct = default);
}

/// <summary>Raw storage for <see cref="LegalEntityTimeSetting"/> — one row per legal entity, no row = timer off.</summary>
public interface ILegalEntityTimeSettingRepository
{
    Task<IReadOnlyList<LegalEntityTimeSetting>> ListAsync(CancellationToken ct = default);

    Task<LegalEntityTimeSetting?> GetByLegalEntityIdAsync(Guid legalEntityId, CancellationToken ct = default);

    /// <summary>Is the timer switched on for ANY legal entity of the tenant? One cheap read — the transition hook's first
    /// question, so a tenant with the timer off everywhere pays nothing on a task write (v2 F6).</summary>
    Task<bool> AnyTimerEnabledAsync(CancellationToken ct = default);

    Task<bool> TryCreateAsync(LegalEntityTimeSetting setting, CancellationToken ct = default);

    Task<bool> UpdateAsync(LegalEntityTimeSetting setting, int expectedVersion, CancellationToken ct = default);
}

/// <summary>Raw storage for <see cref="TaskTimeTotal"/>. Written ONLY by the approval finalizer (D7).</summary>
public interface ITaskTimeTotalRepository
{
    Task<IReadOnlyList<TaskTimeTotal>> ListByTaskIdsAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default);

    /// <summary>SETS the task's approved minutes (never an increment — replaying it writes the same value), guarded by
    /// the row's version: <paramref name="expectedVersion"/> null means "no row yet" (an insert). <c>false</c> when
    /// another finalizer wrote in between — the caller recomputes from the current approvals and tries again.</summary>
    Task<bool> TrySetApprovedMinutesAsync(
        Guid taskItemId, int approvedMinutes, Guid lastFinalizedWeekId, DateTimeOffset recomputedAtUtc, int? expectedVersion,
        CancellationToken ct = default);
}

/// <summary>Raw storage for <see cref="TimerSegment"/> (MOD-0280-FU01 T1b, D2–D4).</summary>
public interface ITimerSegmentRepository
{
    /// <summary>Inserts a RUNNING segment. <c>false</c> when a unique index refused it: the person already has a running
    /// segment (a concurrent start won), or this transition already started one (a replay).</summary>
    Task<bool> TryStartAsync(TimerSegment segment, CancellationToken ct = default);

    /// <summary>The person's running segment, if any (at most one — the partial unique index says so).</summary>
    Task<TimerSegment?> GetRunningAsync(Guid userId, CancellationToken ct = default);

    Task<TimerSegment?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Every running segment on one task, whoever holds it.</summary>
    Task<IReadOnlyList<TimerSegment>> ListRunningByTaskAsync(Guid taskItemId, CancellationToken ct = default);

    /// <summary>Every running segment of the tenant — the midnight job's and the legal-entity switch's work list.</summary>
    Task<IReadOnlyList<TimerSegment>> ListRunningAsync(int limit, CancellationToken ct = default);

    /// <summary>Was a segment already started by this MOD-0024 transition?</summary>
    Task<bool> ExistsForStartTransitionAsync(Guid transitionId, CancellationToken ct = default);

    /// <summary>Closes a RUNNING segment: a conditional write on <c>IsRunning == true</c>. <c>false</c> when another
    /// writer closed it first — only the winner goes on to write the draft (so a close is never counted twice).</summary>
    Task<bool> TryCloseAsync(
        Guid segmentId, DateTimeOffset stoppedAtUtc, int durationSeconds, int outsideWorkingMinutes,
        TimerStopReason reason, Guid? stopTransitionId, CancellationToken ct = default);

    /// <summary>Closed segments of one person on one tenant-local day.</summary>
    Task<IReadOnlyList<TimerSegment>> ListClosedForDayAsync(Guid userId, DateOnly localDate, CancellationToken ct = default);

    /// <summary>Every segment of one person in one ISO week, running or not.</summary>
    Task<IReadOnlyList<TimerSegment>> ListForWeekAsync(Guid userId, string weekKey, CancellationToken ct = default);

    /// <summary>Segments of one person's closed midnight closes on one day — the "your timer ran until midnight" banner.</summary>
    Task<IReadOnlyList<TimerSegment>> ListClosedAtMidnightAsync(Guid userId, DateOnly localDate, CancellationToken ct = default);

    /// <summary>BL-484 — the segments a midnight close ended (D3), for several (person, ISO week) pairs in ONE read: the
    /// approvals page's "cut at midnight" mark. Exactly the pairs named — never another week of the same person.</summary>
    Task<IReadOnlyList<TimerSegment>> ListClosedAtMidnightForWeeksAsync(
        IReadOnlyCollection<(Guid UserId, string WeekKey)> weeks, CancellationToken ct = default);

    /// <summary>Claims the one notification of a midnight close (null → now). <c>false</c> when it was already claimed.</summary>
    Task<bool> TryClaimAutoCloseNotificationAsync(Guid segmentId, DateTimeOffset claimedAtUtc, CancellationToken ct = default);

    /// <summary>How many closed segments of the person's week still carry their instants.</summary>
    Task<long> CountUnminimisedAsync(Guid userId, string weekKey, CancellationToken ct = default);

    /// <summary>D4 — clears <c>StartedAtUtc</c>/<c>StoppedAtUtc</c> and sets <c>MinimisedAtUtc</c> on the week's CLOSED
    /// segments that still carry their instants — or only on <paramref name="segmentId"/> when one is named. An update,
    /// never a delete; returns how many changed.</summary>
    Task<long> MinimiseWeekAsync(
        Guid userId, string weekKey, DateTimeOffset minimisedAtUtc, CancellationToken ct = default, Guid? segmentId = null);

    /// <summary>T3 (pack §21.3 N1) — the people with any segment, running or not, in any of these ISO weeks.</summary>
    Task<IReadOnlyList<Guid>> ListUserIdsWithSegmentsAsync(IReadOnlyCollection<string> weekKeys, CancellationToken ct = default);
}

/// <summary>Raw storage for <see cref="TimeSuggestion"/> decisions (MOD-0280-FU01 T1b, D8).</summary>
public interface ITimeSuggestionRepository
{
    /// <summary>Every decided suggestion of one person for these meetings.</summary>
    Task<IReadOnlyList<TimeSuggestion>> ListForMeetingsAsync(
        Guid userId, IReadOnlyCollection<Guid> meetingIds, CancellationToken ct = default);

    Task<TimeSuggestion?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Inserts the decision. <c>false</c> when the (meeting, user) unique index refused it — decided already.</summary>
    Task<bool> TryCreateAsync(TimeSuggestion suggestion, CancellationToken ct = default);
}

/// <summary>Raw storage for <see cref="TimeEntryNotificationMark"/> (MOD-0280-FU01 T3, pack §21.3 N6).</summary>
public interface ITimeEntryNotificationMarkRepository
{
    /// <summary>Claims (kind, key) for this tenant. <c>false</c> when the unique index says it was claimed before — the
    /// caller then sends nothing. Called BEFORE the send.</summary>
    Task<bool> TryClaimAsync(string kind, string key, DateTimeOffset claimedAtUtc, CancellationToken ct = default);

    /// <summary>Was (kind, key) already claimed in this tenant? A read only — the reminder job's cheap first question, so
    /// people already reminded never use up a run's limit (CT acceptance round 1, M1).</summary>
    Task<bool> ExistsAsync(string kind, string key, CancellationToken ct = default);
}
