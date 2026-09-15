using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;

// MOD-0357 S7 (K6) — continuation scheduling. Delegates meeting CREATION to the SAME CreateMeetingCommand path
// a Create-page meeting uses (no second creation path); this handler's own job is entirely the carry-forward of
// the source meeting's still-open, agenda-anchored linked tasks, plus the cross-meeting idempotency and
// cycle guards CreateMeetingCommand itself has no reason to know about.

public sealed class ScheduleFollowUpMeetingHandler
    : IRequestHandler<ScheduleFollowUpMeetingCommand, Response<ScheduleFollowUpMeetingResultDto>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IAgendaItemRepository _agendaItems;
    private readonly ITaskItemRepository _tasks;
    private readonly IRecordLinkService _links;
    private readonly ITenantContext _tenantContext;
    private readonly IMeetingIdempotencyKeyResolver _idempotency;
    private readonly ICurrentUserContext _currentUser;
    private readonly IMediator _mediator;

    public ScheduleFollowUpMeetingHandler(
        IMeetingRepository meetings,
        IAgendaItemRepository agendaItems,
        ITaskItemRepository tasks,
        IRecordLinkService links,
        ITenantContext tenantContext,
        IMeetingIdempotencyKeyResolver idempotency,
        ICurrentUserContext currentUser,
        IMediator mediator)
    {
        _meetings = meetings;
        _agendaItems = agendaItems;
        _tasks = tasks;
        _links = links;
        _tenantContext = tenantContext;
        _idempotency = idempotency;
        _currentUser = currentUser;
        _mediator = mediator;
    }

    public async Task<Response<ScheduleFollowUpMeetingResultDto>> Handle(
        ScheduleFollowUpMeetingCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var source = await _meetings.GetByIdAsync(command.SourceMeetingId, ct);
        if (source is null)
        {
            return Response<ScheduleFollowUpMeetingResultDto>.Fail(
                "This meeting could not be found.", 404, MeetingReasonCodes.FollowUpNotFound, command.CorrelationId);
        }

        // K11 — a resubmitted schedule-follow-up request (double-click, client retry) resolves to the SAME new
        // meeting it already made, never a second one. Anchored on a dedicated RecordLink the same way the S4
        // bridge anchors its own idempotency (AddLinkAsync's IdempotencyKey parameter) — the user-facing
        // cross-link display never reads this row, only Meeting.FollowUpOfMeetingId (GetMeetingByIdHandler).
        var idempotencyKey = _idempotency.ResolveForFollowUp(source.Id, _currentUser.UserId, request.IdempotencyKey);
        var existingLink = await _links.FindByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existingLink is not null)
        {
            var alreadyCarried = (await _agendaItems.ListByMeetingIdAsync(existingLink.TargetRecordId, ct))
                .Count(a => a.CarriedFromMeetingId == source.Id);
            return Response<ScheduleFollowUpMeetingResultDto>.Success(
                new ScheduleFollowUpMeetingResultDto(existingLink.TargetRecordId, alreadyCarried),
                200, command.CorrelationId);
        }

        var title = string.IsNullOrWhiteSpace(request.Title) ? $"{source.Title} (devam)" : request.Title!.Trim();

        var meetingRequest = new CreateMeetingRequest(
            Title: title,
            MeetingTypeId: request.MeetingTypeId ?? source.MeetingTypeId,
            StartAt: request.StartAt,
            EndAt: request.EndAt,
            Location: request.Location,
            OrganizerUserId: request.OrganizerUserId ?? source.OrganizerUserId,
            Description: request.Description,
            FollowUpOfMeetingId: source.Id,
            AttendeeUserIds: request.AttendeeUserIds);

        var meetingResult = await _mediator.Send(new CreateMeetingCommand(meetingRequest, command.CorrelationId), ct);
        if (!meetingResult.IsSuccessful || meetingResult.Data is null)
        {
            return Response<ScheduleFollowUpMeetingResultDto>.Fail(
                meetingResult.Errors.Count > 0 ? meetingResult.Errors[0] : "The follow-up meeting could not be scheduled.",
                meetingResult.StatusCode, meetingResult.ReasonCode, command.CorrelationId);
        }

        var newMeetingId = meetingResult.Data.Id;

        // FollowUpOfMeetingId is otherwise write-once (set only at CreateMeetingRequest time, never via
        // UpdateMeetingRequest — measured, not assumed), so the only way a "cycle" can reach this handler is a
        // collision inside CreateMeetingCommand's OWN business-key idempotency (organizer+type+start+end+title
        // all matching an EXISTING meeting), which can hand back a meeting that already existed — including,
        // in a contrived case, the source meeting itself or one already in its own ancestry. Refused
        // defensively (K6: "no cycles"); nothing needs to be undone, since FindOrCreateAsync's replay path
        // never mutates the row it returns.
        if (await CreatesCycleAsync(source.Id, newMeetingId, ct))
        {
            return Response<ScheduleFollowUpMeetingResultDto>.Fail(
                "A meeting cannot be scheduled as its own follow-up.", 400, MeetingReasonCodes.SelfFollowUp, command.CorrelationId);
        }

        await _links.AddLinkAsync(
            new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, source.Id),
            new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, newMeetingId),
            RecordLinkTypes.FollowUp,
            idempotencyKey,
            ct: ct);

        var carried = await CarryForwardOpenAgendaTasksAsync(source, newMeetingId, ct);

        return Response<ScheduleFollowUpMeetingResultDto>.Success(
            new ScheduleFollowUpMeetingResultDto(newMeetingId, carried),
            201, command.CorrelationId);
    }

    /// <summary>
    /// LIVE-MEASURED BUG, FIXED HERE: the first version of this check flagged EVERY ordinary fresh follow-up as
    /// a cycle, because <paramref name="resolvedMeetingId"/> always has <c>FollowUpOfMeetingId == sourceMeetingId</c>
    /// by construction (CreateMeetingCommand just wrote it) — reaching the source ONCE, as the immediate parent,
    /// is the expected shape, not a cycle. A genuine cycle is a REVISIT: some node reappears in the walk, which
    /// can only happen when <paramref name="resolvedMeetingId"/> is a pre-existing row (the business-key
    /// collision case — see this method's own caller) whose ancestry already loops.
    /// </summary>
    private async Task<bool> CreatesCycleAsync(Guid sourceMeetingId, Guid resolvedMeetingId, CancellationToken ct)
    {
        if (resolvedMeetingId == sourceMeetingId)
        {
            return true; // the collision landed exactly on the source meeting's own row.
        }

        var visited = new HashSet<Guid> { resolvedMeetingId };
        var currentId = resolvedMeetingId;
        while (true)
        {
            var current = await _meetings.GetByIdAsync(currentId, ct);
            if (current?.FollowUpOfMeetingId is not { } nextId)
            {
                return false;
            }

            if (!visited.Add(nextId))
            {
                return true; // this id was already seen earlier in the SAME walk — a genuine cycle.
            }

            currentId = nextId;
        }
    }

    /// <summary>
    /// K6 — "the previous meeting's still-open linked-task agenda lines", in original order, at the front of the
    /// new meeting's agenda. Two kinds carry: the agenda-ANCHORED links, in the order the previous meeting
    /// listed them, and then the tasks that meeting PRODUCED (bornFromMeeting — an action captured on the spot
    /// or born from a published decision), which have no agenda line of their own and get one created here.
    /// CT 2026-09-12: counting only the anchored ones dropped exactly the carry-forward a management review
    /// exists for — its own open actions. PREPARATION links stay out on purpose: that work fed INTO the
    /// meeting, it is not an action the meeting owes. A closed
    /// (Done/Cancelled) task is dropped; a dangling link (task deleted/cross-tenant) is silently dropped too,
    /// the same rule <c>GetLinkedTasksHandler</c> already applies for a far end that cannot be read.
    /// </summary>
    private async Task<int> CarryForwardOpenAgendaTasksAsync(Meeting source, Guid newMeetingId, CancellationToken ct)
    {
        var sourceAgenda = (await _agendaItems.ListByMeetingIdAsync(source.Id, ct))
            .Where(a => a.RecordLinkId is not null)
            .OrderBy(a => a.SortOrder)
            .ToList();

        var sourceTaskLinks = (await _links.ListBySourceAsync([source.Id], ct))
            .Where(l => l.TargetModuleCode == RecordLinkModuleCodes.Tasks)
            .ToList();
        var sourceLinksById = sourceTaskLinks.ToDictionary(l => l.Id);

        var candidates = new List<(RecordLink Link, Guid TaskId)>();
        var taken = new HashSet<Guid>();
        foreach (var agenda in sourceAgenda)
        {
            if (sourceLinksById.TryGetValue(agenda.RecordLinkId!.Value, out var anchored)
                && taken.Add(anchored.TargetRecordId))
            {
                candidates.Add((anchored, anchored.TargetRecordId));
            }
        }

        foreach (var produced in sourceTaskLinks
                     .Where(l => l.LinkType == RecordLinkTypes.BornFromMeeting)
                     .OrderBy(l => l.CreatedAt))
        {
            if (taken.Add(produced.TargetRecordId))
            {
                candidates.Add((produced, produced.TargetRecordId));
            }
        }

        if (candidates.Count == 0)
        {
            return 0;
        }

        var tasksById = (await _tasks.ListByIdsAsync(candidates.Select(c => c.TaskId).Distinct().ToList(), ct))
            .ToDictionary(t => t.Id);

        var openTaskIds = candidates
            .Where(c => tasksById.TryGetValue(c.TaskId, out var task)
                        && task.Lifecycle != TaskLifecycle.Done
                        && task.Lifecycle != TaskLifecycle.Cancelled)
            .Select(c => c.TaskId)
            .ToList();

        var sortOrder = 0;
        foreach (var taskId in openTaskIds)
        {
            var task = tasksById[taskId];
            var newLink = await _links.AddLinkAsync(
                new RecordLinkEndpoint(RecordLinkModuleCodes.Meetings, newMeetingId),
                new RecordLinkEndpoint(RecordLinkModuleCodes.Tasks, taskId),
                RecordLinkTypes.Agenda,
                ct: ct);

            await _agendaItems.CreateAsync(new AgendaItem
            {
                TenantId = _tenantContext.TenantId,
                MeetingId = newMeetingId,
                Text = task.Title,
                SortOrder = sortOrder++,
                RecordLinkId = newLink.Id,
                CarriedFromMeetingId = source.Id,
                CreatedBy = _currentUser.ActorName
            }, ct);
        }

        return openTaskIds.Count;
    }
}
