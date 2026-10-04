using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;

// MOD-0357 S6 — minutes: draft, publish, correct (pack §3 "Minutes as a versioned document", K4). The three
// commands below are the ONLY writers of MeetingMinutesVersion in the codebase; the shared rule they all honour
// is in MinutesEligibility.CheckDraftable/ToDto below, so a fourth writer added later inherits it rather than
// re-deriving it.

internal static class MinutesEligibility
{
    /// <summary>
    /// Server-mints <c>Code</c> from POSITION ("D-1", "D-2"…) and carries a decision's existing
    /// <see cref="MinutesDecision.RecordLinkId"/> forward when it is still at the SAME position it was at
    /// before — a decision that already produced a task keeps that task's link as long as it is not reordered
    /// past the point where position stops identifying it. The request contract carries no id besides
    /// position; a client that reorders a decision after linking a task to it detaches the link, which is a
    /// known, documented limitation (see the S6 report), not something this slice's DTO can express otherwise.
    /// </summary>
    public static List<MinutesDecision> MergeDecisions(
        IReadOnlyList<MinutesDecisionRequest> incoming, IReadOnlyList<MinutesDecision>? previous)
    {
        var result = new List<MinutesDecision>(incoming.Count);
        for (var i = 0; i < incoming.Count; i++)
        {
            var carriedLinkId = previous is not null && i < previous.Count ? previous[i].RecordLinkId : null;
            result.Add(new MinutesDecision
            {
                Code = $"D-{i + 1}",
                Text = incoming[i].Text.Trim(),
                DecidedByUserId = incoming[i].DecidedByUserId,
                RecordLinkId = carriedLinkId
            });
        }

        return result;
    }

    /// <summary><see cref="MeetingMinutesVersion.ActionReferences"/> is DERIVED, always — never independently
    /// mutated — so it can never drift from the per-decision <see cref="MinutesDecision.RecordLinkId"/> fields
    /// it is a flat projection of.</summary>
    public static List<Guid> DeriveActionReferences(IEnumerable<MinutesDecision> decisions)
        => decisions.Where(d => d.RecordLinkId.HasValue).Select(d => d.RecordLinkId!.Value).Distinct().ToList();

    public static List<MinutesAttendanceRecord> MapAttendance(IReadOnlyList<MinutesAttendanceRequest> incoming)
        => incoming.Select(a => new MinutesAttendanceRecord { AttendeeUserId = a.AttendeeUserId, Status = a.Status }).ToList();

    public static async Task<MeetingMinutesVersionDto> ToDtoAsync(
        MeetingMinutesVersion version, IUserDisplayNameResolver displayNames, CancellationToken ct)
    {
        var ids = version.Attendance.Select(a => a.AttendeeUserId)
            .Concat(version.Decisions.Where(d => d.DecidedByUserId.HasValue).Select(d => d.DecidedByUserId!.Value))
            .Concat(version.PublishedByUserId.HasValue ? [version.PublishedByUserId.Value] : [])
            .Distinct()
            .ToList();
        var names = ids.Count == 0
            ? new Dictionary<Guid, string>()
            : (await displayNames.ResolveAsync(ids, ct)).ToDictionary(kv => kv.Key, kv => kv.Value);

        return new MeetingMinutesVersionDto(
            Id: version.Id,
            VersionNumber: version.VersionNumber,
            Status: version.Status,
            Attendance: version.Attendance
                .Select(a => new MinutesAttendanceDto(a.AttendeeUserId, names.GetValueOrDefault(a.AttendeeUserId), a.Status))
                .ToList(),
            Decisions: version.Decisions
                .Select(d => new MinutesDecisionDto(
                    d.Code, d.Text, d.DecidedByUserId,
                    d.DecidedByUserId.HasValue ? names.GetValueOrDefault(d.DecidedByUserId.Value) : null,
                    d.RecordLinkId))
                .ToList(),
            ActionReferences: version.ActionReferences,
            PublishedAtUtc: version.PublishedAtUtc,
            PublishedByUserId: version.PublishedByUserId,
            PublishedByDisplayName: version.PublishedByUserId.HasValue
                ? names.GetValueOrDefault(version.PublishedByUserId.Value) : null,
            CorrectionOfVersionNumber: version.CorrectionOfVersionNumber,
            CorrectionReason: version.CorrectionReason,
            Version: version.Version);
    }
}

/// <summary>
/// A draft is edited in place (pack §4 K4): no minutes row yet → creates v1 Draft; an existing Draft row →
/// replaces it in place, <see cref="SaveMinutesDraftRequest.ExpectedVersion"/>-conditional. A Published row is
/// refused (409) — the only path forward from Published is <see cref="CorrectPublishedMinutesCommand"/>.
/// </summary>
public sealed class SaveMinutesDraftHandler : IRequestHandler<SaveMinutesDraftCommand, Response<MeetingMinutesVersionDto>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly IMeetingMinutesVersionRepository _minutes;
    private readonly ICurrentUserContext _currentUser;
    private readonly IUserDisplayNameResolver _displayNames;

    public SaveMinutesDraftHandler(
        IMeetingRepository meetings,
        IMeetingAttendeeRepository attendees,
        IMeetingMinutesVersionRepository minutes,
        ICurrentUserContext currentUser,
        IUserDisplayNameResolver displayNames)
    {
        _meetings = meetings;
        _attendees = attendees;
        _minutes = minutes;
        _currentUser = currentUser;
        _displayNames = displayNames;
    }

    public async Task<Response<MeetingMinutesVersionDto>> Handle(SaveMinutesDraftCommand command, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(command.MeetingId, ct);
        if (meeting is null)
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "The meeting does not exist.", 404, MeetingReasonCodes.NotFound, command.CorrelationId);
        }

        var request = command.Request;

        // Attendance may only name a REAL attendee of THIS meeting — never an arbitrary user id.
        var attendeeIds = (await _attendees.ListByMeetingIdAsync(command.MeetingId, ct)).Select(a => a.UserId).ToHashSet();
        if (request.Attendance.Any(a => !attendeeIds.Contains(a.AttendeeUserId)))
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "Attendance can only be recorded for an invited attendee.", 400, MeetingReasonCodes.AttendeeNotFound, command.CorrelationId);
        }

        if (request.Decisions.Any(d => string.IsNullOrWhiteSpace(d.Text)))
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "A decision's text cannot be empty.", 400, null, command.CorrelationId);
        }

        var latest = await _minutes.GetLatestByMeetingIdAsync(command.MeetingId, ct);
        if (latest is not null && latest.Status == MinutesStatus.Published)
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "The published minutes cannot be edited directly.", 409, MeetingReasonCodes.MinutesPublished, command.CorrelationId);
        }

        var decisions = MinutesEligibility.MergeDecisions(request.Decisions, latest?.Decisions);
        var attendance = MinutesEligibility.MapAttendance(request.Attendance);
        var actionReferences = MinutesEligibility.DeriveActionReferences(decisions);

        if (latest is null)
        {
            var created = await _minutes.TryCreateAsync(new MeetingMinutesVersion
            {
                TenantId = meeting.TenantId,
                MeetingId = command.MeetingId,
                VersionNumber = 1,
                Status = MinutesStatus.Draft,
                Attendance = attendance,
                Decisions = decisions,
                ActionReferences = actionReferences,
                CreatedBy = _currentUser.ActorName
            }, ct);
            if (created is null)
            {
                return Response<MeetingMinutesVersionDto>.Fail(
                    "The record changed meanwhile; reload and retry.", 409, MeetingReasonCodes.MinutesConcurrencyConflict, command.CorrelationId);
            }

            return Response<MeetingMinutesVersionDto>.Success(
                await MinutesEligibility.ToDtoAsync(created, _displayNames, ct), 201, command.CorrelationId);
        }

        // latest.Status == Draft here (Published was refused above).
        if (request.ExpectedVersion is null)
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "The record changed meanwhile; reload and retry.", 409, MeetingReasonCodes.MinutesConcurrencyConflict, command.CorrelationId);
        }

        latest.Attendance = attendance;
        latest.Decisions = decisions;
        latest.ActionReferences = actionReferences;
        var updated = await _minutes.UpdateAsync(latest, request.ExpectedVersion.Value, ct);
        if (!updated)
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "The record changed meanwhile; reload and retry.", 409, MeetingReasonCodes.MinutesConcurrencyConflict, command.CorrelationId);
        }

        return Response<MeetingMinutesVersionDto>.Success(
            await MinutesEligibility.ToDtoAsync(latest, _displayNames, ct), 200, command.CorrelationId);
    }
}

/// <summary>
/// Draft → Published, ONE WAY (pack §4 K4). Also the one place <see cref="Meeting.Lifecycle"/> ever becomes
/// <see cref="MeetingLifecycle.Completed"/> (pack §4 Entity Fields — "set when minutes publish, never chosen by
/// the user directly"), and the one place <see cref="MeetingAttendee.AttendanceStatus"/> is written (one-way:
/// tutanak → katılımcı, never the reverse).
/// </summary>
public sealed class PublishMinutesHandler : IRequestHandler<PublishMinutesCommand, Response<MeetingMinutesVersionDto>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly IMeetingMinutesVersionRepository _minutes;
    private readonly ICurrentUserContext _currentUser;
    private readonly IUserDisplayNameResolver _displayNames;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly IMeetingAttendanceObserver? _attendanceObserver;
    private readonly ILogger<PublishMinutesHandler>? _logger;

    public PublishMinutesHandler(
        IMeetingRepository meetings,
        IMeetingAttendeeRepository attendees,
        IMeetingMinutesVersionRepository minutes,
        ICurrentUserContext currentUser,
        IUserDisplayNameResolver displayNames,
        IPlatformTransactionExecutor transactions,
        IMeetingAttendanceObserver? attendanceObserver = null,
        ILogger<PublishMinutesHandler>? logger = null)
    {
        _meetings = meetings;
        _attendees = attendees;
        _minutes = minutes;
        _currentUser = currentUser;
        _displayNames = displayNames;
        _transactions = transactions;
        _attendanceObserver = attendanceObserver;
        _logger = logger;
    }

    public async Task<Response<MeetingMinutesVersionDto>> Handle(PublishMinutesCommand command, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(command.MeetingId, ct);
        if (meeting is null)
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "The meeting does not exist.", 404, MeetingReasonCodes.NotFound, command.CorrelationId);
        }

        // BL-533 — a cancelled meeting gets no published minutes (publishing would also make it Completed). Checked here,
        // and again inside the transaction against the meeting as it is when the publish is written.
        if (meeting.Lifecycle == MeetingLifecycle.Cancelled)
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "The meeting is cancelled.", 409, MeetingReasonCodes.Cancelled, command.CorrelationId);
        }

        var latest = await _minutes.GetLatestByMeetingIdAsync(command.MeetingId, ct);
        if (latest is null)
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "There is no draft to publish.", 400, null, command.CorrelationId);
        }

        if (latest.Status == MinutesStatus.Published)
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "These minutes are already published.", 409, MeetingReasonCodes.MinutesPublished, command.CorrelationId);
        }

        latest.Status = MinutesStatus.Published;
        latest.PublishedAtUtc = DateTimeOffset.UtcNow;
        latest.PublishedByUserId = _currentUser.UserId;

        // BL-533 — the publish, the attendance it records and the meeting's Completed are ONE Platform transaction: a
        // refusal of any of them leaves the minutes a Draft at the version they were read at, and the answer is 409.
        var read = new ReadVersionSnapshot();
        read.Remember(latest);
        Meeting completed;
        try
        {
            completed = await _transactions.ExecuteAsync(async (session, tct) =>
            {
                read.Restore();
                if (!await _minutes.UpdateAsync(session, latest, command.Request.ExpectedVersion, tct))
                {
                    throw new MinutesPublicationRefused(
                        409, "The record changed meanwhile; reload and retry.", MeetingReasonCodes.MinutesConcurrencyConflict);
                }

                return await SyncAsync(_meetings, _attendees, session, meeting, latest, refuseCancelled: true, tct);
            }, ct);
        }
        catch (MinutesPublicationRefused refused)
        {
            return refused.ToResponse<MeetingMinutesVersionDto>(command.CorrelationId);
        }

        // MOD-0280-FU01 T3 (N5) — time entry hears who the minutes recorded; never throws into this publish.
        await _attendanceObserver.NotifySafelyAsync(completed, latest, _logger);

        return Response<MeetingMinutesVersionDto>.Success(
            await MinutesEligibility.ToDtoAsync(latest, _displayNames, ct), 200, command.CorrelationId);
    }

    /// <summary>
    /// The two side effects a publish carries (pack §4 Entity Fields), written INSIDE the publish's own transaction:
    /// <see cref="MeetingAttendee.AttendanceStatus"/> ONE-WAY from the published version (never the reverse), and
    /// <see cref="Meeting.Lifecycle"/> → <see cref="MeetingLifecycle.Completed"/> on the meeting's FIRST publish (a
    /// correction, <see cref="CorrectPublishedMinutesHandler"/>, finds it already Completed: only the attendance half runs).
    /// <para>BL-533 — the meeting is read again in the transaction, so the Completed write lands on the meeting as it is
    /// now (an edit made since <paramref name="meetingAsRead"/> was read stays); a write that lands after that is a
    /// transaction write conflict, and the executor runs the whole publish again (bounded) from a fresh read. A refused
    /// write, or — when <paramref name="refuseCancelled"/> — a meeting cancelled meanwhile, aborts the publish. A cancelled
    /// meeting is never made Completed.</para>
    /// </summary>
    internal static async Task<Meeting> SyncAsync(
        IMeetingRepository meetings, IMeetingAttendeeRepository attendees, IPlatformTransactionSession session,
        Meeting meetingAsRead, MeetingMinutesVersion version, bool refuseCancelled, CancellationToken ct)
    {
        var meeting = await meetings.GetByIdAsync(session, meetingAsRead.Id, ct);
        if (meeting is null)
        {
            throw new MinutesPublicationRefused(404, "The meeting does not exist.", MeetingReasonCodes.NotFound);
        }

        if (refuseCancelled && meeting.Lifecycle == MeetingLifecycle.Cancelled)
        {
            throw new MinutesPublicationRefused(409, "The meeting is cancelled.", MeetingReasonCodes.Cancelled);
        }

        foreach (var record in version.Attendance)
        {
            await attendees.UpdateAttendanceStatusAsync(session, meeting.Id, record.AttendeeUserId, record.Status, ct);
        }

        if (meeting.Lifecycle is not (MeetingLifecycle.Completed or MeetingLifecycle.Cancelled))
        {
            meeting.Lifecycle = MeetingLifecycle.Completed;
            if (!await meetings.UpdateAsync(session, meeting, meeting.Version, ct))
            {
                throw new MinutesPublicationRefused(
                    409, "The meeting changed meanwhile; reload and retry.", MeetingReasonCodes.ConcurrencyConflict);
            }
        }

        return meeting;
    }
}

/// <summary>BL-533 — a write of a minutes publication (or correction) was refused: thrown out of the transaction body, so
/// all of it is aborted, and the handler answers with this status and reason code.</summary>
internal sealed class MinutesPublicationRefused(int statusCode, string message, string reasonCode) : Exception(message)
{
    public Response<T> ToResponse<T>(string correlationId) => Response<T>.Fail(Message, statusCode, reasonCode, correlationId);
}

/// <summary>
/// K4's "the only path forward from a Published row": a mandatory <see cref="CorrectPublishedMinutesRequest.CorrectionReason"/>
/// plus the corrected content, written as a NEW row — <see cref="MeetingMinutesVersion.CorrectionOfVersionNumber"/>
/// points at the row being corrected, which this handler NEVER writes to (the source guard: no code path in
/// this file calls <c>UpdateAsync</c> on a row whose <c>Status</c> is already <c>Published</c>).
/// </summary>
public sealed class CorrectPublishedMinutesHandler
    : IRequestHandler<CorrectPublishedMinutesCommand, Response<MeetingMinutesVersionDto>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly IMeetingMinutesVersionRepository _minutes;
    private readonly ICurrentUserContext _currentUser;
    private readonly IUserDisplayNameResolver _displayNames;
    private readonly IPlatformTransactionExecutor _transactions;
    private readonly IMeetingAttendanceObserver? _attendanceObserver;
    private readonly ILogger<CorrectPublishedMinutesHandler>? _logger;

    public CorrectPublishedMinutesHandler(
        IMeetingRepository meetings,
        IMeetingAttendeeRepository attendees,
        IMeetingMinutesVersionRepository minutes,
        ICurrentUserContext currentUser,
        IUserDisplayNameResolver displayNames,
        IPlatformTransactionExecutor transactions,
        IMeetingAttendanceObserver? attendanceObserver = null,
        ILogger<CorrectPublishedMinutesHandler>? logger = null)
    {
        _meetings = meetings;
        _attendees = attendees;
        _minutes = minutes;
        _currentUser = currentUser;
        _displayNames = displayNames;
        _transactions = transactions;
        _attendanceObserver = attendanceObserver;
        _logger = logger;
    }

    public async Task<Response<MeetingMinutesVersionDto>> Handle(CorrectPublishedMinutesCommand command, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(command.MeetingId, ct);
        if (meeting is null)
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "The meeting does not exist.", 404, MeetingReasonCodes.NotFound, command.CorrelationId);
        }

        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.CorrectionReason))
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "A correction reason is required.", 400, MeetingReasonCodes.MinutesCorrectionReasonRequired, command.CorrelationId);
        }

        var attendeeIds = (await _attendees.ListByMeetingIdAsync(command.MeetingId, ct)).Select(a => a.UserId).ToHashSet();
        if (request.Attendance.Any(a => !attendeeIds.Contains(a.AttendeeUserId)))
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "Attendance can only be recorded for an invited attendee.", 400, MeetingReasonCodes.AttendeeNotFound, command.CorrelationId);
        }

        if (request.Decisions.Any(d => string.IsNullOrWhiteSpace(d.Text)))
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "A decision's text cannot be empty.", 400, null, command.CorrelationId);
        }

        var latest = await _minutes.GetLatestByMeetingIdAsync(command.MeetingId, ct);
        if (latest is null || latest.Status != MinutesStatus.Published)
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "There is no published version to correct.", 409, MeetingReasonCodes.MinutesNotPublished, command.CorrelationId);
        }

        var decisions = MinutesEligibility.MergeDecisions(request.Decisions, latest.Decisions);
        var attendance = MinutesEligibility.MapAttendance(request.Attendance);
        var actionReferences = MinutesEligibility.DeriveActionReferences(decisions);

        // K4 — "yeni satır Draft başlar ve kendi yayımıyla yayımlanır": written directly as Published in the
        // SAME call, because a correction that sat in Draft would leave the STALE prior version as the one
        // "currently published" while the fix waited for a second round-trip — the opposite of what correcting
        // a live record is for. This is the ONLY place a row is created already Published; every ordinary
        // publish still goes through PublishMinutesHandler's own Draft → Published step.
        var next = new MeetingMinutesVersion
        {
            TenantId = meeting.TenantId,
            MeetingId = command.MeetingId,
            VersionNumber = latest.VersionNumber + 1,
            Status = MinutesStatus.Published,
            Attendance = attendance,
            Decisions = decisions,
            ActionReferences = actionReferences,
            PublishedAtUtc = DateTimeOffset.UtcNow,
            PublishedByUserId = _currentUser.UserId,
            CorrectionOfVersionNumber = latest.VersionNumber,
            CorrectionReason = request.CorrectionReason.Trim(),
            CreatedBy = _currentUser.ActorName
        };

        // BL-533 — the new row, the attendance it records and the meeting sync are ONE Platform transaction (the same shape
        // a publish has): a refusal leaves no correction row behind a 409.
        Meeting synced;
        try
        {
            synced = await _transactions.ExecuteAsync(async (session, tct) =>
            {
                if (await _minutes.TryCreateAsync(session, next, tct) is null)
                {
                    throw new MinutesPublicationRefused(
                        409, "The record changed meanwhile; reload and retry.", MeetingReasonCodes.MinutesConcurrencyConflict);
                }

                // Meeting.Lifecycle is already Completed from the original publish — this call's Lifecycle half is a
                // guarded no-op, and only the attendance sync runs (PublishMinutesHandler.SyncAsync's own doc comment).
                return await PublishMinutesHandler.SyncAsync(
                    _meetings, _attendees, session, meeting, next, refuseCancelled: false, tct);
            }, ct);
        }
        catch (MinutesPublicationRefused refused)
        {
            return refused.ToResponse<MeetingMinutesVersionDto>(command.CorrelationId);
        }

        // MOD-0280-FU01 T3 (N5) — the correction is heard too; never throws into it.
        await _attendanceObserver.NotifySafelyAsync(synced, next, _logger);

        return Response<MeetingMinutesVersionDto>.Success(
            await MinutesEligibility.ToDtoAsync(next, _displayNames, ct), 201, command.CorrelationId);
    }
}
