using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;

// MOD-0357 S4 — the meeting↔task bridge (pack §3 Commands "bridge", §7, §13, K1/K2/K3/K9/K11). Every write here
// either delegates to MOD-0024's own commands unchanged (K2) or writes exactly one RecordLink (K1) — never
// both a link AND a second copy of MOD-0024's own data.

/// <summary>"Create-and-link" — delegates to <c>CreateTaskItemCommand</c> unchanged; see the command's own
/// doc comment for K2/K11.</summary>
public sealed class CreateTaskFromMeetingHandler
    : IRequestHandler<CreateTaskFromMeetingCommand, Response<CreateTaskFromMeetingResultDto>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingTypeRepository _types;
    private readonly IAgendaItemRepository _agendaItems;
    private readonly IMeetingMinutesVersionRepository _minutes;
    private readonly IRecordLinkService _links;
    private readonly IMeetingIdempotencyKeyResolver _idempotency;
    private readonly ICurrentUserContext _currentUser;
    private readonly IMediator _mediator;

    public CreateTaskFromMeetingHandler(
        IMeetingRepository meetings,
        IMeetingTypeRepository types,
        IAgendaItemRepository agendaItems,
        IMeetingMinutesVersionRepository minutes,
        IRecordLinkService links,
        IMeetingIdempotencyKeyResolver idempotency,
        ICurrentUserContext currentUser,
        IMediator mediator)
    {
        _meetings = meetings;
        _types = types;
        _agendaItems = agendaItems;
        _minutes = minutes;
        _links = links;
        _idempotency = idempotency;
        _currentUser = currentUser;
        _mediator = mediator;
    }

    public async Task<Response<CreateTaskFromMeetingResultDto>> Handle(
        CreateTaskFromMeetingCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var meeting = await _meetings.GetByIdAsync(command.MeetingId, ct);
        if (meeting is null)
        {
            return Response<CreateTaskFromMeetingResultDto>.Fail(
                "This meeting could not be found.", 404, MeetingReasonCodes.NotFound, command.CorrelationId);
        }

        // K11 — the bridge's own idempotency: a resubmitted request (double-click, client retry) resolves to
        // the SAME task and the SAME link, never a second pair of either.
        var idempotencyKey = _idempotency.ResolveForTaskBridge(
            command.MeetingId, _currentUser.UserId, request.IdempotencyKey);
        var existingLink = await _links.FindByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existingLink is not null)
        {
            return Response<CreateTaskFromMeetingResultDto>.Success(
                new CreateTaskFromMeetingResultDto(existingLink.TargetRecordId, existingLink.Id, request.AgendaItemId),
                200, command.CorrelationId);
        }

        AgendaItem? agendaItem = null;
        if (request.AgendaItemId is { } agendaItemId)
        {
            agendaItem = await _agendaItems.GetByIdAsync(agendaItemId, ct);
            if (agendaItem is null || agendaItem.MeetingId != command.MeetingId)
            {
                return Response<CreateTaskFromMeetingResultDto>.Fail(
                    "This agenda item could not be found.", 404, MeetingReasonCodes.AgendaItemNotFound, command.CorrelationId);
            }
        }

        // MOD-0357 S6 — the third bridge moment: a decision inside the meeting's minutes. Validated against
        // the LATEST version whether it is still Draft or already Published (a read, never a write); which
        // half of "found but frozen" applies is decided further down, at the one point this handler might
        // otherwise write into a Published row.
        MeetingMinutesVersion? latestMinutes = null;
        MinutesDecision? decision = null;
        if (request.DecisionCode is { } decisionCode)
        {
            latestMinutes = await _minutes.GetLatestByMeetingIdAsync(command.MeetingId, ct);
            decision = latestMinutes?.Decisions.FirstOrDefault(d => d.Code == decisionCode);
            if (decision is null)
            {
                return Response<CreateTaskFromMeetingResultDto>.Fail(
                    "This decision could not be found.", 404, MeetingReasonCodes.DecisionNotFound, command.CorrelationId);
            }
        }
        else
        {
            latestMinutes = await _minutes.GetLatestByMeetingIdAsync(command.MeetingId, ct);
        }

        // K4 — "a task opened after minutes publish is flagged distinctly". Read here regardless of
        // DecisionCode: the preparation/on-the-spot moments can ALSO land after a publish (e.g. minutes
        // published early, then a further task added from the agenda) and the flag means the same thing either
        // way — this task was not accounted for in the record as it stood when it was signed off.
        var createdAfterMinutesPublished = latestMinutes?.Status == MinutesStatus.Published;

        // AgendaItemId beyond this point is only ever the ALREADY-VALIDATED agenda item's own id — never the
        // caller's raw input again, so a task created against a since-deleted agenda item cannot happen.
        var taskTypeId = request.TaskTypeId;
        if (taskTypeId is null)
        {
            var meetingType = await _types.GetByIdAsync(meeting.MeetingTypeId, ct);
            taskTypeId = meetingType?.DefaultActionTaskTypeId;
        }

        // K2 — the ONLY create path. Same reason codes an ordinary create would return (out-of-scope assignee →
        // 400 TASK_ASSIGNEE_NOT_ASSIGNABLE, missing title → validator 400); no meeting-specific field is set on
        // the task itself (ADR-003 — TaskItem carries no field for this).
        var taskRequest = new CreateTaskItemRequest(
            Title: request.Title,
            Description: request.Description,
            Priority: TaskPriority.Medium,
            AssignmentTarget: request.AssigneeUserId is not null ? TaskAssignmentTarget.Person : TaskAssignmentTarget.SelfAssigned,
            AssigneeUserId: request.AssigneeUserId,
            PoolPositionId: null,
            OrganizationUnitId: null,
            DueAt: request.DueAt,
            StartAt: null,
            PlannedDate: null,
            EstimateHours: null,
            Tags: null,
            ReviewRequired: false,
            ApprovalRequired: false,
            ApprovalManagerUserId: null,
            EmailNotificationsEnabled: true,
            DelegationAllowed: true,
            FieldValues: null,
            Watchers: null,
            TaskTypeId: taskTypeId);

        var taskResult = await _mediator.Send(new CreateTaskItemCommand(taskRequest, command.CorrelationId), ct);
        if (!taskResult.IsSuccessful)
        {
            return Response<CreateTaskFromMeetingResultDto>.Fail(
                taskResult.Errors.Count > 0 ? taskResult.Errors[0] : "The task could not be created.",
                taskResult.StatusCode, taskResult.ReasonCode, command.CorrelationId);
        }

        var taskId = taskResult.Data;

        // The link's own type follows WHEN it was created, per pack §3: before the meeting starts is
        // preparation work, during (or after) is an action raised from the meeting itself. A decision is
        // always "bornFromMeeting" — by definition a decision only exists once the meeting is happening or
        // done, so there is no "preparation" reading of it, unlike the time-based check for the other two
        // moments.
        var linkType = request.DecisionCode is not null || DateTimeOffset.UtcNow >= meeting.StartAt
            ? RecordLinkTypes.BornFromMeeting
            : RecordLinkTypes.Preparation;

        var link = await _links.AddLinkAsync(
            new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, command.MeetingId),
            new RecordLinkEndpoint(RecordLinkModuleCodes.Tasks, taskId),
            linkType,
            idempotencyKey,
            createdAfterMinutesPublished,
            ct);

        if (agendaItem is not null)
        {
            agendaItem.RecordLinkId = link.Id;
            await _agendaItems.UpdateAsync(agendaItem, agendaItem.Version, ct);
        }

        // K4's source guard: a decision inside an ALREADY-Published version is never written to — this
        // embeds the link only while the owning version is still Draft. A decision that spawns a task after
        // its version published stays exactly as published; CreatedAfterMinutesPublished on the link above is
        // how the UI finds that task instead (see MinutesDecision.RecordLinkId's own doc comment).
        if (decision is not null && latestMinutes is not null && latestMinutes.Status == MinutesStatus.Draft)
        {
            decision.RecordLinkId = link.Id;
            latestMinutes.ActionReferences = MinutesEligibility.DeriveActionReferences(latestMinutes.Decisions);
            await _minutes.UpdateAsync(latestMinutes, latestMinutes.Version, ct);
        }

        return Response<CreateTaskFromMeetingResultDto>.Success(
            new CreateTaskFromMeetingResultDto(taskId, link.Id, agendaItem?.Id),
            201, command.CorrelationId);
    }
}

/// <summary>Links an EXISTING MOD-0024 task to this meeting — always <c>LinkType: "agenda"</c>.</summary>
public sealed class LinkExistingTaskHandler : IRequestHandler<LinkExistingTaskCommand, Response<NoContent>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IAgendaItemRepository _agendaItems;
    private readonly ITaskItemRepository _tasks;
    private readonly IRecordLinkService _links;

    public LinkExistingTaskHandler(
        IMeetingRepository meetings,
        IAgendaItemRepository agendaItems,
        ITaskItemRepository tasks,
        IRecordLinkService links)
    {
        _meetings = meetings;
        _agendaItems = agendaItems;
        _tasks = tasks;
        _links = links;
    }

    public async Task<Response<NoContent>> Handle(LinkExistingTaskCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var meeting = await _meetings.GetByIdAsync(command.MeetingId, ct);
        if (meeting is null)
        {
            return Response<NoContent>.Fail(
                "This meeting could not be found.", 404, MeetingReasonCodes.NotFound, command.CorrelationId);
        }

        // Tenant-filtered by the repository's own execution filter — a cross-tenant or deleted task id reads as
        // "not found", never a metadata leak (pack §13).
        var task = await _tasks.GetByIdAsync(request.TaskId, ct);
        if (task is null)
        {
            return Response<NoContent>.Fail(
                "This task could not be found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        AgendaItem? agendaItem = null;
        if (request.AgendaItemId is { } agendaItemId)
        {
            agendaItem = await _agendaItems.GetByIdAsync(agendaItemId, ct);
            if (agendaItem is null || agendaItem.MeetingId != command.MeetingId)
            {
                return Response<NoContent>.Fail(
                    "This agenda item could not be found.", 404, MeetingReasonCodes.AgendaItemNotFound, command.CorrelationId);
            }
        }

        var existingLink = await _links.FindLinkAsync(
            new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, command.MeetingId),
            new RecordLinkEndpoint(RecordLinkModuleCodes.Tasks, request.TaskId),
            RecordLinkTypes.Agenda,
            ct);
        if (existingLink is not null)
        {
            // The caller asked to link a SPECIFIC task and deserves to know it already was — never a silent
            // no-op success for THIS command (unlike the bridge-create's own idempotency, which has no such
            // "the caller must be told" requirement in the pack).
            return Response<NoContent>.Fail(
                "This task is already linked to this meeting.", 409, MeetingReasonCodes.TaskAlreadyLinked, command.CorrelationId);
        }

        var link = await _links.AddLinkAsync(
            new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, command.MeetingId),
            new RecordLinkEndpoint(RecordLinkModuleCodes.Tasks, request.TaskId),
            RecordLinkTypes.Agenda,
            ct: ct);

        if (agendaItem is not null)
        {
            agendaItem.RecordLinkId = link.Id;
            await _agendaItems.UpdateAsync(agendaItem, agendaItem.Version, ct);
        }

        return Response<NoContent>.Success(200, command.CorrelationId);
    }
}

/// <summary>The receiving side of MOD-0024's <c>scheduleReviewMeeting</c> work-item action — delegates to the
/// SAME <c>CreateMeetingCommand</c> path a Create-page meeting uses (no second meeting-creation path).</summary>
public sealed class ScheduleReviewMeetingForTaskHandler
    : IRequestHandler<ScheduleReviewMeetingForTaskCommand, Response<ScheduleReviewMeetingForTaskResultDto>>
{
    private readonly ITaskItemRepository _tasks;
    private readonly IRecordLinkService _links;
    private readonly IMeetingIdempotencyKeyResolver _idempotency;
    private readonly ICurrentUserContext _currentUser;
    private readonly IMediator _mediator;

    public ScheduleReviewMeetingForTaskHandler(
        ITaskItemRepository tasks,
        IRecordLinkService links,
        IMeetingIdempotencyKeyResolver idempotency,
        ICurrentUserContext currentUser,
        IMediator mediator)
    {
        _tasks = tasks;
        _links = links;
        _idempotency = idempotency;
        _currentUser = currentUser;
        _mediator = mediator;
    }

    public async Task<Response<ScheduleReviewMeetingForTaskResultDto>> Handle(
        ScheduleReviewMeetingForTaskCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var task = await _tasks.GetByIdAsync(command.TaskId, ct);
        if (task is null)
        {
            return Response<ScheduleReviewMeetingForTaskResultDto>.Fail(
                "This task could not be found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        // "the task must be one the actor can read" (pack §7 NE, K9-adjacent) — holder or requester, never a
        // 403 that would confirm the task's existence to someone unrelated to it (pack §13's own 404 posture).
        var isHolder = task.AssigneeUserId == _currentUser.UserId;
        var isRequester = task.CreatedByUserId == _currentUser.UserId;
        if (!isHolder && !isRequester)
        {
            return Response<ScheduleReviewMeetingForTaskResultDto>.Fail(
                "This task could not be found.", 404, TaskReasonCodes.NotFound, command.CorrelationId);
        }

        // K11 — the bridge's own idempotency, checked BEFORE the "already scheduled" guard below: a resubmit of
        // THIS SAME request (double-click, client retry) must resolve to the SAME meeting it already made, never
        // the 409 that guard would otherwise raise against the very link this request itself created.
        var idempotencyKey = _idempotency.ResolveForTaskBridge(
            command.TaskId, _currentUser.UserId, request.IdempotencyKey);
        var existingIdempotent = await _links.FindByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existingIdempotent is not null)
        {
            return Response<ScheduleReviewMeetingForTaskResultDto>.Success(
                new ScheduleReviewMeetingForTaskResultDto(existingIdempotent.SourceRecordId, existingIdempotent.Id),
                200, command.CorrelationId);
        }

        // K3 — exactly one reviewMeeting link per task; a genuinely SECOND schedule attempt (a different
        // idempotency key) is refused, not silently re-linked to a different meeting.
        var existingLinks = await _links.ListByTargetAsync([command.TaskId], ct);
        if (existingLinks.Any(l =>
                l.LinkType == RecordLinkTypes.ReviewMeeting && l.SourceModuleCode == RecordLinkModuleCodes.Meetings))
        {
            return Response<ScheduleReviewMeetingForTaskResultDto>.Fail(
                "A review meeting is already scheduled for this task.", 409, MeetingReasonCodes.ReviewAlreadyScheduled, command.CorrelationId);
        }

        var title = string.IsNullOrWhiteSpace(request.Title) ? $"Review: {task.Title}" : request.Title!.Trim();

        // The SAME path a Create-page meeting uses: organizer = the acting user, the SAME MeetingEligibility
        // check (pack §7 — no second meeting-creation path).
        var meetingRequest = new CreateMeetingRequest(
            Title: title,
            MeetingTypeId: request.MeetingTypeId,
            StartAt: request.StartAt,
            EndAt: request.EndAt,
            Location: null,
            OrganizerUserId: _currentUser.UserId,
            Description: null,
            FollowUpOfMeetingId: null,
            AttendeeUserIds: null);

        var meetingResult = await _mediator.Send(new CreateMeetingCommand(meetingRequest, command.CorrelationId), ct);
        if (!meetingResult.IsSuccessful || meetingResult.Data is null)
        {
            return Response<ScheduleReviewMeetingForTaskResultDto>.Fail(
                meetingResult.Errors.Count > 0 ? meetingResult.Errors[0] : "The review meeting could not be scheduled.",
                meetingResult.StatusCode, meetingResult.ReasonCode, command.CorrelationId);
        }

        var meetingId = meetingResult.Data.Id;

        var link = await _links.AddLinkAsync(
            new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, meetingId),
            new RecordLinkEndpoint(RecordLinkModuleCodes.Tasks, command.TaskId),
            RecordLinkTypes.ReviewMeeting,
            idempotencyKey,
            ct: ct);

        return Response<ScheduleReviewMeetingForTaskResultDto>.Success(
            new ScheduleReviewMeetingForTaskResultDto(meetingId, link.Id),
            201, command.CorrelationId);
    }
}
