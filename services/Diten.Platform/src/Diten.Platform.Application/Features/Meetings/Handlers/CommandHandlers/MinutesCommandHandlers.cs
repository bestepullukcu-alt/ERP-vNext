using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Repositories;
using MediatR;

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

    public PublishMinutesHandler(
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

    public async Task<Response<MeetingMinutesVersionDto>> Handle(PublishMinutesCommand command, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(command.MeetingId, ct);
        if (meeting is null)
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "The meeting does not exist.", 404, MeetingReasonCodes.NotFound, command.CorrelationId);
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
        var updated = await _minutes.UpdateAsync(latest, command.Request.ExpectedVersion, ct);
        if (!updated)
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "The record changed meanwhile; reload and retry.", 409, MeetingReasonCodes.MinutesConcurrencyConflict, command.CorrelationId);
        }

        await SyncAsync(_meetings, _attendees, meeting, latest, ct);

        return Response<MeetingMinutesVersionDto>.Success(
            await MinutesEligibility.ToDtoAsync(latest, _displayNames, ct), 200, command.CorrelationId);
    }

    /// <summary>
    /// The two side effects a publish carries (pack §4 Entity Fields): <see cref="MeetingAttendee.AttendanceStatus"/>
    /// is written ONE-WAY from the published version (never the reverse), and <see cref="Meeting.Lifecycle"/>
    /// becomes <see cref="MeetingLifecycle.Completed"/> — but ONLY on the meeting's FIRST publish. Idempotent by
    /// that guard, so <see cref="CorrectPublishedMinutesHandler"/> (whose publish-equivalent step happens on an
    /// ALREADY-Completed meeting) can call this too, and only the attendance half of it does anything.
    /// </summary>
    internal static async Task SyncAsync(
        IMeetingRepository meetings, IMeetingAttendeeRepository attendees,
        Meeting meeting, MeetingMinutesVersion version, CancellationToken ct)
    {
        foreach (var record in version.Attendance)
        {
            await attendees.UpdateAttendanceStatusAsync(meeting.Id, record.AttendeeUserId, record.Status, ct);
        }

        if (meeting.Lifecycle != MeetingLifecycle.Completed)
        {
            meeting.Lifecycle = MeetingLifecycle.Completed;
            // Best-effort: a concurrent meeting edit racing this exact instant is rare, and the minutes row is
            // already durably Published either way — a lost race here does not undo the publish.
            await meetings.UpdateAsync(meeting, meeting.Version, ct);
        }
    }
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

    public CorrectPublishedMinutesHandler(
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

        var created = await _minutes.TryCreateAsync(next, ct);
        if (created is null)
        {
            return Response<MeetingMinutesVersionDto>.Fail(
                "The record changed meanwhile; reload and retry.", 409, MeetingReasonCodes.MinutesConcurrencyConflict, command.CorrelationId);
        }

        // Meeting.Lifecycle is already Completed from the original publish — this call's Lifecycle half is a
        // guarded no-op, and only the attendance sync runs (PublishMinutesHandler.SyncAsync's own doc comment).
        await PublishMinutesHandler.SyncAsync(_meetings, _attendees, meeting, created, ct);

        return Response<MeetingMinutesVersionDto>.Success(
            await MinutesEligibility.ToDtoAsync(created, _displayNames, ct), 201, command.CorrelationId);
    }
}
