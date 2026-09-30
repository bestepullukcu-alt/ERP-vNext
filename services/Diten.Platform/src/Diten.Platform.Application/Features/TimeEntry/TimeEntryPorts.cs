namespace Diten.Platform.Application.Features.TimeEntry;

// MOD-0280-FU01 (pack §2.4, §3.3, ADR-004 decision 3) — the ONLY doors from this module to MOD-0288 (org chart) and
// MOD-0024 (tasks). The v1 implementations live in Adapters/ and are the only files under Features/TimeEntry allowed to
// name another module's repository or entity; TimeEntrySourceGuardTests fails the build otherwise. On extraction these
// become HTTP clients and nothing else in the module changes.

/// <summary>A person's primary seat: the position that decides their line manager and their legal entity.</summary>
public sealed record TimeEntryPrimarySeat(Guid PositionId, Guid? LegalEntityId);

/// <summary>One node of the reporting chain.</summary>
public sealed record TimeEntryPosition(Guid PositionId, Guid? ReportsToPositionId, bool IsArchived);

/// <summary>MOD-0288, read only.</summary>
public interface ITimeEntryOrgGateway
{
    /// <summary>The person's primary active seat (primary assignment first, as MOD-0288 orders them), or null.</summary>
    Task<TimeEntryPrimarySeat?> PrimarySeatAsync(Guid userId, CancellationToken ct = default);

    /// <summary>One position, or null when it does not exist in this tenant.</summary>
    Task<TimeEntryPosition?> PositionAsync(Guid positionId, CancellationToken ct = default);

    /// <summary>Who holds this seat right now.</summary>
    Task<IReadOnlyList<Guid>> HoldersOfAsync(Guid positionId, CancellationToken ct = default);
}

/// <summary>What the timer needs to know about a task: is it InProgress, and who holds it.</summary>
public sealed record TimeEntryTaskFacts(Guid TaskItemId, bool IsInProgress, Guid? HolderUserId);

/// <summary>A task's plan block (MOD-0024 create-runtime §22): when the holder planned to work on it, and for how long.</summary>
public sealed record TimeEntryPlannedBlock(Guid TaskItemId, DateTimeOffset PlannedStartAt, int PlannedDurationMinutes);

/// <summary>What a person may see of a task in their own time records (T2a): its title and its lifecycle, by name.
/// Only ever built for a task the person can READ.</summary>
public sealed record TimeEntryTaskSummary(Guid TaskItemId, string Title, string Status);

/// <summary>MOD-0024, read only: readability (T1a), then lifecycle, holder, the transition that ended a run, and the
/// plan block (T1b), then titles and the person's own open tasks (T2a).</summary>
public interface ITimeEntryTaskGateway
{
    /// <summary>Facts for these tasks. A task that does not exist in this tenant (or was deleted) is absent.</summary>
    Task<IReadOnlyDictionary<Guid, TimeEntryTaskFacts>> TaskFactsAsync(
        IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default);

    /// <summary>The instant the task stopped being <paramref name="holderUserId"/>'s InProgress work after
    /// <paramref name="since"/> — the first recorded transition that left InProgress or moved the holder. Null when the log
    /// names none (the caller then closes at "now").</summary>
    Task<DateTimeOffset?> InvalidatedAtAsync(
        Guid taskItemId, Guid holderUserId, DateTimeOffset since, CancellationToken ct = default);

    /// <summary>Plan blocks of the tasks the person holds that start inside [<paramref name="fromUtc"/>,
    /// <paramref name="toUtc"/>).</summary>
    Task<IReadOnlyList<TimeEntryPlannedBlock>> PlannedBlocksAsync(
        Guid userId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default);

    /// <summary>Which of these task ids <paramref name="userId"/> may READ, by MOD-0024's own read-access rule (F5). A
    /// task that does not exist and a task the person cannot read are both simply absent — the caller cannot tell them
    /// apart, so a time row can never be used to probe for another team's task ids.</summary>
    Task<IReadOnlySet<Guid>> ReadableTaskIdsAsync(Guid userId, IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default);

    /// <summary>T2a — title and lifecycle of the tasks among <paramref name="taskIds"/> that <paramref name="userId"/> may
    /// READ, in ONE batched read (a week asks once, never once per task). The same rule as
    /// <see cref="ReadableTaskIdsAsync"/>: a task the person cannot read is absent, so its title never leaves the server.</summary>
    Task<IReadOnlyDictionary<Guid, TimeEntryTaskSummary>> ReadableTaskSummariesAsync(
        Guid userId, IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default);

    /// <summary>T2a — the open tasks (Open, Planned, InProgress, Waiting, PendingReview — CT v3) <paramref name="userId"/> holds, filtered by the
    /// same read rule.</summary>
    Task<IReadOnlyList<TimeEntryTaskSummary>> OwnOpenTasksAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>What the meeting's minutes say about one attendee, as they stand now.</summary>
public enum TimeEntryMeetingAttendance
{
    /// <summary>No published minutes record this person (yet).</summary>
    NotRecorded = 0,
    Present = 1,
    Absent = 2,
    Excused = 3
}

/// <summary>One meeting the person ACCEPTED, with the minutes' current word on their attendance.</summary>
public sealed record TimeEntryAcceptedMeeting(
    Guid MeetingId,
    string Title,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    bool IsCancelled,
    TimeEntryMeetingAttendance Attendance);

/// <summary>MOD-0357, read only (pack §2.2, D8). Nothing in the meetings feature is edited; attendance is read, not
/// copied, so a minutes correction is visible on the next read.</summary>
public interface ITimeEntryMeetingGateway
{
    /// <summary>Meetings the person accepted whose start lies in [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).
    /// Declined and still-pending invitations are absent.</summary>
    Task<IReadOnlyList<TimeEntryAcceptedMeeting>> AcceptedMeetingsAsync(
        Guid userId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default);
}
