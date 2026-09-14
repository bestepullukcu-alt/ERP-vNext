using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;

// MOD-0357 S2 — every write here goes through the SAME eligibility seam MOD-0024's own assignee picker uses
// (GetTaskAssignmentPersonLookupQuery, Purpose: Decision — scope-EXEMPT, per D2: any active tenant user,
// whatever their company/unit). No second user-resolution path is written (the WP's own NE #3 instruction).

internal static class MeetingEligibility
{
    public static async Task<HashSet<Guid>> EligibleUserIdsAsync(IMediator mediator, string correlationId, CancellationToken ct)
    {
        var response = await mediator.Send(
            new GetTaskAssignmentPersonLookupQuery(correlationId, TaskPersonLookupPurpose.Decision), ct);
        return response.Data?.People.Select(p => p.UserId).ToHashSet() ?? [];
    }

    public static MeetingDto ToDto(
        Meeting meeting, string meetingTypeName,
        IReadOnlyList<MeetingAttendee> attendees, IReadOnlyList<AgendaItem> agendaItems,
        MeetingInviteDeliveryResult? delivery = null,
        string? followUpOfMeetingTitle = null,
        Meeting? followedByMeeting = null) => new(
        Id: meeting.Id,
        Title: meeting.Title,
        MeetingTypeId: meeting.MeetingTypeId,
        MeetingTypeName: meetingTypeName,
        StartAt: meeting.StartAt,
        EndAt: meeting.EndAt,
        Location: meeting.Location,
        OrganizerUserId: meeting.OrganizerUserId,
        Description: meeting.Description,
        FollowUpOfMeetingId: meeting.FollowUpOfMeetingId,
        Lifecycle: meeting.Lifecycle,
        CancellationReason: meeting.CancellationReason,
        Version: meeting.Version,
        Attendees: attendees
            .Select(a => new MeetingAttendeeDto(a.Id, a.UserId, null, a.InvitationResponse, a.AttendanceStatus))
            .ToList(),
        AgendaItems: agendaItems
            .OrderBy(a => a.SortOrder)
            .Select(a => new AgendaItemDto(a.Id, a.Text, a.SortOrder, a.Version, a.RecordLinkId, a.CarriedFromMeetingId))
            .ToList(),
        InviteDelivery: delivery is null
            ? null
            : new MeetingInviteDeliveryDto(delivery.Sent, delivery.Failed, delivery.Reason),
        FollowUpOfMeetingTitle: followUpOfMeetingTitle,
        FollowedByMeetingId: followedByMeeting?.Id,
        FollowedByMeetingTitle: followedByMeeting?.Title);

    /// <summary>D3 (§22): visible to the organizer, any attendee, or whoever holds <c>read-all</c>.</summary>
    public static bool CanView(Meeting meeting, Guid callerUserId, bool hasReadAll, IReadOnlySet<Guid> attendeeUserIds)
        => hasReadAll || meeting.OrganizerUserId == callerUserId || attendeeUserIds.Contains(callerUserId);
}

public sealed class CreateMeetingHandler : IRequestHandler<CreateMeetingCommand, Response<MeetingDto>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingTypeRepository _types;
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly IMeetingIdempotencyKeyResolver _idempotency;
    private readonly IMediator _mediator;
    private readonly IMeetingInviteMailer _inviteMailer;

    public CreateMeetingHandler(
        IMeetingRepository meetings,
        IMeetingTypeRepository types,
        IMeetingAttendeeRepository attendees,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        IMeetingIdempotencyKeyResolver idempotency,
        IMediator mediator,
        IMeetingInviteMailer inviteMailer)
    {
        _meetings = meetings;
        _types = types;
        _attendees = attendees;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _idempotency = idempotency;
        _mediator = mediator;
        _inviteMailer = inviteMailer;
    }

    public async Task<Response<MeetingDto>> Handle(CreateMeetingCommand command, CancellationToken ct)
    {
        var request = command.Request;

        if (request.EndAt <= request.StartAt)
        {
            return Response<MeetingDto>.Fail(
                "The end time must be after the start time.", 400, MeetingReasonCodes.EndBeforeStart, command.CorrelationId);
        }

        var type = await _types.GetByIdAsync(request.MeetingTypeId, ct);
        if (type is null)
        {
            return Response<MeetingDto>.Fail(
                "The meeting type does not exist.", 400, MeetingReasonCodes.TypeNotFound, command.CorrelationId);
        }

        var organizerUserId = request.OrganizerUserId ?? _currentUser.UserId;
        var eligible = await MeetingEligibility.EligibleUserIdsAsync(_mediator, command.CorrelationId, ct);
        if (!eligible.Contains(organizerUserId))
        {
            return Response<MeetingDto>.Fail(
                "The organizer must be an active tenant user.", 400, MeetingReasonCodes.OrganizerInvalid, command.CorrelationId);
        }

        if (request.FollowUpOfMeetingId is { } followUpId)
        {
            var followUp = await _meetings.GetByIdAsync(followUpId, ct);
            if (followUp is null)
            {
                return Response<MeetingDto>.Fail(
                    "The meeting this follows up on does not exist.", 404, MeetingReasonCodes.FollowUpNotFound, command.CorrelationId);
            }
        }

        var idempotencyKey = _idempotency.Resolve(
            organizerUserId, request.MeetingTypeId, request.StartAt, request.EndAt, request.Title);

        var candidate = new Meeting
        {
            TenantId = _tenantContext.TenantId,
            Title = request.Title.Trim(),
            MeetingTypeId = request.MeetingTypeId,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            Location = request.Location,
            OrganizerUserId = organizerUserId,
            Description = request.Description,
            FollowUpOfMeetingId = request.FollowUpOfMeetingId,
            IdempotencyKey = idempotencyKey,
            CreatedBy = _currentUser.ActorName
        };

        var meeting = await _meetings.FindOrCreateAsync(candidate, ct);
        var isNew = meeting.Id == candidate.Id;

        var addedAttendees = new List<MeetingAttendee>();
        if (isNew)
        {
            // S5 K5 — the organizer is counted Accepted in their own meeting, written at creation: they never
            // sit in Pending, and never receive an invite e-mail for a meeting they themselves just made
            // (SendInviteAsync's own actor-exclusion rule handles that half).
            addedAttendees.Add(await _attendees.CreateAsync(new MeetingAttendee
            {
                TenantId = _tenantContext.TenantId,
                MeetingId = meeting.Id,
                UserId = organizerUserId,
                InvitationResponse = InvitationResponse.Accepted,
                CreatedBy = _currentUser.ActorName
            }, ct));

            if (request.AttendeeUserIds is { Count: > 0 })
            {
                foreach (var userId in request.AttendeeUserIds.Distinct())
                {
                    if (userId == organizerUserId)
                    {
                        continue; // already written above — the organizer is never a second, Pending row.
                    }

                    if (!eligible.Contains(userId))
                    {
                        continue; // K12 — a create response only ever reports the meeting; a full skip breakdown is AddMeetingAttendeesCommand's own job.
                    }

                    addedAttendees.Add(await _attendees.CreateAsync(new MeetingAttendee
                    {
                        TenantId = _tenantContext.TenantId,
                        MeetingId = meeting.Id,
                        UserId = userId,
                        CreatedBy = _currentUser.ActorName
                    }, ct));
                }
            }
        }

        var attendees = isNew ? addedAttendees : await _attendees.ListByMeetingIdAsync(meeting.Id, ct);

        // K12 — the invite e-mail is attempted after the meeting is durably written and never rolls it back;
        // its own outcome rides on the response instead of being silently absorbed into the 201.
        var delivery = isNew
            ? await _inviteMailer.SendInviteAsync(meeting, type.Name, attendees, _currentUser.UserId, ct)
            : null;

        var dto = MeetingEligibility.ToDto(meeting, type.Name, attendees, [], delivery);
        return Response<MeetingDto>.Success(dto, 201, command.CorrelationId);
    }
}

public sealed class UpdateMeetingHandler : IRequestHandler<UpdateMeetingCommand, Response<NoContent>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingTypeRepository _types;
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly ICurrentUserContext _currentUser;
    private readonly IMeetingInviteMailer _inviteMailer;

    public UpdateMeetingHandler(
        IMeetingRepository meetings,
        IMeetingTypeRepository types,
        IMeetingAttendeeRepository attendees,
        ICurrentUserContext currentUser,
        IMeetingInviteMailer inviteMailer)
    {
        _meetings = meetings;
        _types = types;
        _attendees = attendees;
        _currentUser = currentUser;
        _inviteMailer = inviteMailer;
    }

    public async Task<Response<NoContent>> Handle(UpdateMeetingCommand command, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(command.Id, ct);
        if (meeting is null)
        {
            return Response<NoContent>.Fail("The meeting does not exist.", 404, MeetingReasonCodes.NotFound, command.CorrelationId);
        }

        var stateCheck = CheckEditable(meeting, command.CorrelationId);
        if (stateCheck is not null)
        {
            return stateCheck;
        }

        var request = command.Request;
        if (request.EndAt <= request.StartAt)
        {
            return Response<NoContent>.Fail(
                "The end time must be after the start time.", 400, MeetingReasonCodes.EndBeforeStart, command.CorrelationId);
        }

        var type = await _types.GetByIdAsync(request.MeetingTypeId, ct);
        if (type is null)
        {
            return Response<NoContent>.Fail(
                "The meeting type does not exist.", 400, MeetingReasonCodes.TypeNotFound, command.CorrelationId);
        }

        // K7/S5 — a change e-mail only for what an attendee would actually notice; a title/description edit
        // with the SAME time and place is not a scheduling change and sends nothing.
        var scheduleChanged = meeting.StartAt != request.StartAt
            || meeting.EndAt != request.EndAt
            || meeting.Location != request.Location;

        meeting.Title = request.Title.Trim();
        meeting.MeetingTypeId = request.MeetingTypeId;
        meeting.StartAt = request.StartAt;
        meeting.EndAt = request.EndAt;
        meeting.Location = request.Location;
        meeting.Description = request.Description;

        if (!await _meetings.UpdateAsync(meeting, request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The meeting changed meanwhile; reload and retry.", 409, MeetingReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        if (scheduleChanged)
        {
            var attendees = await _attendees.ListByMeetingIdAsync(meeting.Id, ct);
            await _inviteMailer.SendChangeAsync(meeting, type.Name, attendees, _currentUser.UserId, ct);
        }

        return Response<NoContent>.Success(200, command.CorrelationId);
    }

    internal static Response<NoContent>? CheckEditable(Meeting meeting, string correlationId) => meeting.Lifecycle switch
    {
        MeetingLifecycle.Cancelled => Response<NoContent>.Fail(
            "The meeting is cancelled.", 409, MeetingReasonCodes.Cancelled, correlationId),
        MeetingLifecycle.Completed => Response<NoContent>.Fail(
            "The meeting is completed.", 409, MeetingReasonCodes.Completed, correlationId),
        _ => null
    };
}

public sealed class CancelMeetingHandler : IRequestHandler<CancelMeetingCommand, Response<NoContent>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingTypeRepository _types;
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly ICurrentUserContext _currentUser;
    private readonly IMeetingInviteMailer _inviteMailer;

    public CancelMeetingHandler(
        IMeetingRepository meetings,
        IMeetingTypeRepository types,
        IMeetingAttendeeRepository attendees,
        ICurrentUserContext currentUser,
        IMeetingInviteMailer inviteMailer)
    {
        _meetings = meetings;
        _types = types;
        _attendees = attendees;
        _currentUser = currentUser;
        _inviteMailer = inviteMailer;
    }

    public async Task<Response<NoContent>> Handle(CancelMeetingCommand command, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(command.Id, ct);
        if (meeting is null)
        {
            return Response<NoContent>.Fail("The meeting does not exist.", 404, MeetingReasonCodes.NotFound, command.CorrelationId);
        }

        if (meeting.Lifecycle == MeetingLifecycle.Cancelled)
        {
            return Response<NoContent>.Fail("The meeting is already cancelled.", 409, MeetingReasonCodes.Cancelled, command.CorrelationId);
        }

        if (meeting.Lifecycle == MeetingLifecycle.Completed)
        {
            return Response<NoContent>.Fail("The meeting is completed.", 409, MeetingReasonCodes.Completed, command.CorrelationId);
        }

        if (string.IsNullOrWhiteSpace(command.Request.Reason))
        {
            return Response<NoContent>.Fail(
                "A cancellation reason is required.", 400, MeetingReasonCodes.CancellationReasonRequired, command.CorrelationId);
        }

        meeting.Lifecycle = MeetingLifecycle.Cancelled;
        meeting.CancellationReason = command.Request.Reason;

        if (!await _meetings.UpdateAsync(meeting, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The meeting changed meanwhile; reload and retry.", 409, MeetingReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        // K7/S5 — cancellation always notifies, unlike an ordinary edit: there is no "not notice-worthy" cancel.
        var type = await _types.GetByIdAsync(meeting.MeetingTypeId, ct);
        var attendees = await _attendees.ListByMeetingIdAsync(meeting.Id, ct);
        await _inviteMailer.SendCancelAsync(meeting, type?.Name ?? string.Empty, attendees, _currentUser.UserId, ct);

        return Response<NoContent>.Success(200, command.CorrelationId);
    }
}

public sealed class ReassignMeetingOrganizerHandler : IRequestHandler<ReassignMeetingOrganizerCommand, Response<NoContent>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IMediator _mediator;

    public ReassignMeetingOrganizerHandler(IMeetingRepository meetings, IMediator mediator)
    {
        _meetings = meetings;
        _mediator = mediator;
    }

    public async Task<Response<NoContent>> Handle(ReassignMeetingOrganizerCommand command, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(command.Id, ct);
        if (meeting is null)
        {
            return Response<NoContent>.Fail("The meeting does not exist.", 404, MeetingReasonCodes.NotFound, command.CorrelationId);
        }

        // D4 — reassignment is a Scheduled-only action; a cancelled/completed meeting's organizer is history.
        if (meeting.Lifecycle == MeetingLifecycle.Cancelled)
        {
            return Response<NoContent>.Fail("The meeting is cancelled.", 409, MeetingReasonCodes.Cancelled, command.CorrelationId);
        }

        if (meeting.Lifecycle == MeetingLifecycle.Completed)
        {
            return Response<NoContent>.Fail("The meeting is completed.", 409, MeetingReasonCodes.Completed, command.CorrelationId);
        }

        var eligible = await MeetingEligibility.EligibleUserIdsAsync(_mediator, command.CorrelationId, ct);
        if (!eligible.Contains(command.Request.NewOrganizerUserId))
        {
            return Response<NoContent>.Fail(
                "The new organizer must be an active tenant user.", 400, MeetingReasonCodes.OrganizerInvalid, command.CorrelationId);
        }

        meeting.PreviousOrganizerUserId = meeting.OrganizerUserId;
        meeting.OrganizerUserId = command.Request.NewOrganizerUserId;

        if (!await _meetings.UpdateAsync(meeting, command.Request.ExpectedVersion, ct))
        {
            return Response<NoContent>.Fail(
                "The meeting changed meanwhile; reload and retry.", 409, MeetingReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        return Response<NoContent>.Success(200, command.CorrelationId);
    }
}

public sealed class AddMeetingAttendeesHandler : IRequestHandler<AddMeetingAttendeesCommand, Response<AddMeetingAttendeesResultDto>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUser;
    private readonly IMediator _mediator;

    public AddMeetingAttendeesHandler(
        IMeetingRepository meetings,
        IMeetingAttendeeRepository attendees,
        ITenantContext tenantContext,
        ICurrentUserContext currentUser,
        IMediator mediator)
    {
        _meetings = meetings;
        _attendees = attendees;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _mediator = mediator;
    }

    public async Task<Response<AddMeetingAttendeesResultDto>> Handle(AddMeetingAttendeesCommand command, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(command.MeetingId, ct);
        if (meeting is null)
        {
            return Response<AddMeetingAttendeesResultDto>.Fail(
                "The meeting does not exist.", 404, MeetingReasonCodes.NotFound, command.CorrelationId);
        }

        var editable = UpdateMeetingHandler.CheckEditable(meeting, command.CorrelationId);
        if (editable is not null)
        {
            return Response<AddMeetingAttendeesResultDto>.Fail(
                editable.Errors, editable.StatusCode, editable.ReasonCode, command.CorrelationId);
        }

        var eligible = await MeetingEligibility.EligibleUserIdsAsync(_mediator, command.CorrelationId, ct);
        var existing = (await _attendees.ListByMeetingIdAsync(command.MeetingId, ct))
            .Select(a => a.UserId)
            .ToHashSet();

        var added = new List<MeetingAttendeeDto>();
        var skipped = new List<MeetingAttendeeSkippedDto>();

        foreach (var userId in command.Request.UserIds.Distinct())
        {
            if (existing.Contains(userId))
            {
                skipped.Add(new MeetingAttendeeSkippedDto(userId, MeetingReasonCodes.AttendeeDuplicate));
                continue;
            }

            if (!eligible.Contains(userId))
            {
                skipped.Add(new MeetingAttendeeSkippedDto(userId, MeetingReasonCodes.AttendeeNotEligible));
                continue;
            }

            var attendee = await _attendees.CreateAsync(new MeetingAttendee
            {
                TenantId = _tenantContext.TenantId,
                MeetingId = command.MeetingId,
                UserId = userId,
                CreatedBy = _currentUser.ActorName
            }, ct);

            existing.Add(userId);
            added.Add(new MeetingAttendeeDto(attendee.Id, attendee.UserId, null, attendee.InvitationResponse, attendee.AttendanceStatus));
        }

        return Response<AddMeetingAttendeesResultDto>.Success(
            new AddMeetingAttendeesResultDto(added, skipped), 200, command.CorrelationId);
    }
}

public sealed class RemoveMeetingAttendeeHandler : IRequestHandler<RemoveMeetingAttendeeCommand, Response<NoContent>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingTypeRepository _types;
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly ICurrentUserContext _currentUser;
    private readonly IMeetingInviteMailer _inviteMailer;

    public RemoveMeetingAttendeeHandler(
        IMeetingRepository meetings,
        IMeetingTypeRepository types,
        IMeetingAttendeeRepository attendees,
        ICurrentUserContext currentUser,
        IMeetingInviteMailer inviteMailer)
    {
        _meetings = meetings;
        _types = types;
        _attendees = attendees;
        _currentUser = currentUser;
        _inviteMailer = inviteMailer;
    }

    public async Task<Response<NoContent>> Handle(RemoveMeetingAttendeeCommand command, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(command.MeetingId, ct);
        if (meeting is null)
        {
            return Response<NoContent>.Fail("The meeting does not exist.", 404, MeetingReasonCodes.NotFound, command.CorrelationId);
        }

        var editable = UpdateMeetingHandler.CheckEditable(meeting, command.CorrelationId);
        if (editable is not null)
        {
            return editable;
        }

        var attendee = await _attendees.FindAsync(command.MeetingId, command.UserId, ct);
        if (attendee is null)
        {
            return Response<NoContent>.Fail("The attendee is not on this meeting.", 404, MeetingReasonCodes.AttendeeNotFound, command.CorrelationId);
        }

        // BL-386 — the organizer's own row is never removable; ReassignMeetingOrganizerCommand is the only door
        // from here, so a caller who wants the organizer OFF the meeting is pointed there, not left to guess.
        if (command.UserId == meeting.OrganizerUserId)
        {
            return Response<NoContent>.Fail(
                "The organizer cannot be removed from the meeting.", 409, MeetingReasonCodes.AttendeeIsOrganizer, command.CorrelationId);
        }

        // BL-386 — the version bump happens BEFORE the delete, on purpose: if a concurrent write already moved
        // Version out from under this read, this fails 409 (the meetings' own existing concurrency pattern) with
        // the attendee row still intact — never a state where the row is gone but the caller was told to retry.
        if (!await _meetings.UpdateAsync(meeting, meeting.Version, ct))
        {
            return Response<NoContent>.Fail(
                "The meeting changed meanwhile; reload and retry.", 409, MeetingReasonCodes.ConcurrencyConflict, command.CorrelationId);
        }

        await _attendees.DeleteAsync(attendee.Id, ct);

        // BL-386 — the removed attendee's own calendar entry does not disappear on its own; a single .ics CANCEL
        // (SEQUENCE = the Version this write just bumped to, above the invite's own) tells JUST that one reader's
        // calendar client to drop it. K12 — a mail failure here never undoes the removal already written.
        var type = await _types.GetByIdAsync(meeting.MeetingTypeId, ct);
        await _inviteMailer.SendRemovedAsync(meeting, type?.Name ?? string.Empty, attendee, _currentUser.UserId, ct);

        return Response<NoContent>.Success(200, command.CorrelationId);
    }
}

/// <summary>S5, K5 — Accept/Decline, ERP-internal. §13 :497's own posture: a caller with no attendee row on this
/// meeting gets 404, never a hint that the meeting exists.</summary>
public sealed class RespondToInvitationHandler : IRequestHandler<RespondToInvitationCommand, Response<NoContent>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly ICurrentUserContext _currentUser;

    public RespondToInvitationHandler(
        IMeetingRepository meetings, IMeetingAttendeeRepository attendees, ICurrentUserContext currentUser)
    {
        _meetings = meetings;
        _attendees = attendees;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(RespondToInvitationCommand command, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(command.MeetingId, ct);
        if (meeting is null)
        {
            return Response<NoContent>.Fail("The meeting does not exist.", 404, MeetingReasonCodes.NotFound, command.CorrelationId);
        }

        // Cancelled/completed — same 409s UpdateMeetingHandler's own editable-state gate returns; a decision
        // recorded against a meeting that is no longer happening is not what this endpoint is for.
        var stateCheck = UpdateMeetingHandler.CheckEditable(meeting, command.CorrelationId);
        if (stateCheck is not null)
        {
            return stateCheck;
        }

        var response = command.Request.Response switch
        {
            "Accept" => InvitationResponse.Accepted,
            "Decline" => InvitationResponse.Declined,
            _ => (InvitationResponse?)null
        };

        if (response is null)
        {
            return Response<NoContent>.Fail(
                "The response must be 'Accept' or 'Decline'.", 400, MeetingReasonCodes.InvitationResponseInvalid, command.CorrelationId);
        }

        // §13 :497 — 404, not 403: a caller with no row here never learns the meeting exists.
        var attendee = await _attendees.FindAsync(command.MeetingId, _currentUser.UserId, ct);
        if (attendee is null)
        {
            return Response<NoContent>.Fail(
                "This meeting has no invitation for you.", 404, MeetingReasonCodes.AttendeeNotFound, command.CorrelationId);
        }

        // K5 — idempotent: the SAME response resubmitted still writes (a no-op value change) and still answers
        // 200. No branch skips the write, so there is nothing here that could special-case the repeat wrong.
        await _attendees.UpdateInvitationResponseAsync(attendee.Id, response.Value, ct);
        return Response<NoContent>.Success(200, command.CorrelationId);
    }
}
