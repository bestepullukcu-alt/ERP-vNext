using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Meetings.Queries;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Meetings.Handlers.QueryHandlers;

public sealed class GetMeetingByIdHandler : IRequestHandler<GetMeetingByIdQuery, Response<MeetingDto>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingTypeRepository _types;
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly IAgendaItemRepository _agendaItems;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActorPermissionContext _permissions;

    public GetMeetingByIdHandler(
        IMeetingRepository meetings,
        IMeetingTypeRepository types,
        IMeetingAttendeeRepository attendees,
        IAgendaItemRepository agendaItems,
        ICurrentUserContext currentUser,
        IActorPermissionContext permissions)
    {
        _meetings = meetings;
        _types = types;
        _attendees = attendees;
        _agendaItems = agendaItems;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    public async Task<Response<MeetingDto>> Handle(GetMeetingByIdQuery query, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(query.Id, ct);
        if (meeting is null)
        {
            return Response<MeetingDto>.Fail("The meeting does not exist.", 404, MeetingReasonCodes.NotFound, query.CorrelationId);
        }

        var attendees = await _attendees.ListByMeetingIdAsync(query.Id, ct);
        var hasReadAll = _permissions.IsPlatformActor || _permissions.Has(MeetingPermissions.ReadAll);
        if (!MeetingEligibility.CanView(meeting, _currentUser.UserId, hasReadAll, attendees.Select(a => a.UserId).ToHashSet()))
        {
            // 404, never 403 — D3's own posture (pack §13): a caller with no relationship to the record must not
            // learn the meeting exists at all, the same rule cross-tenant reads already follow.
            return Response<MeetingDto>.Fail("The meeting does not exist.", 404, MeetingReasonCodes.NotFound, query.CorrelationId);
        }

        var type = await _types.GetByIdAsync(meeting.MeetingTypeId, ct);
        var agendaItems = await _agendaItems.ListByMeetingIdAsync(query.Id, ct);

        return Response<MeetingDto>.Success(
            MeetingEligibility.ToDto(meeting, type?.Name ?? string.Empty, attendees, agendaItems), 200, query.CorrelationId);
    }
}

public sealed class GetMeetingListHandler : IRequestHandler<GetMeetingListQuery, Response<MeetingListResultDto>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingTypeRepository _types;
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly IRecordLinkService _recordLinks;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActorPermissionContext _permissions;

    public GetMeetingListHandler(
        IMeetingRepository meetings,
        IMeetingTypeRepository types,
        IMeetingAttendeeRepository attendees,
        IRecordLinkService recordLinks,
        ICurrentUserContext currentUser,
        IActorPermissionContext permissions)
    {
        _meetings = meetings;
        _types = types;
        _attendees = attendees;
        _recordLinks = recordLinks;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    public async Task<Response<MeetingListResultDto>> Handle(GetMeetingListQuery query, CancellationToken ct)
    {
        var all = await _meetings.ListAsync(ct);
        var meetingIds = all.Select(m => m.Id).ToList();

        // Batched, never per-row: one call for every meeting's attendees, one pair of calls for every meeting's
        // linked-task presence — the same discipline TaskWorkItemProvider's own relatedRecords read follows.
        var attendeesByMeeting = (await _attendees.ListByMeetingIdsAsync(meetingIds, ct))
            .GroupBy(a => a.MeetingId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.UserId).ToHashSet());

        var linkedSource = await _recordLinks.ListBySourceAsync(meetingIds, ct);
        var linkedTarget = await _recordLinks.ListByTargetAsync(meetingIds, ct);
        var hasLinkedTasks = linkedSource.Select(l => l.SourceRecordId)
            .Concat(linkedTarget.Select(l => l.TargetRecordId))
            .ToHashSet();

        var types = await _types.ListAsync(ct);
        var typeNameById = types.ToDictionary(t => t.Id, t => t.Name);

        var hasReadAll = _permissions.IsPlatformActor || _permissions.Has(MeetingPermissions.ReadAll);
        var callerId = _currentUser.UserId;

        var filter = query.Filter;
        var visible = all.Where(m =>
        {
            var attendeeIds = attendeesByMeeting.GetValueOrDefault(m.Id, []);
            return MeetingEligibility.CanView(m, callerId, hasReadAll, attendeeIds);
        });

        var filtered = visible
            .Where(m => filter.FromUtc is null || m.StartAt >= filter.FromUtc)
            .Where(m => filter.ToUtc is null || m.StartAt < filter.ToUtc)
            .Where(m => filter.MeetingTypeId is null || m.MeetingTypeId == filter.MeetingTypeId)
            .Where(m => filter.OrganizerUserId is null || m.OrganizerUserId == filter.OrganizerUserId)
            .Where(m => filter.IAmAttendeeOnly != true || attendeesByMeeting.GetValueOrDefault(m.Id, []).Contains(callerId))
            .Where(m => filter.HasLinkedTasksOnly != true || hasLinkedTasks.Contains(m.Id))
            .OrderByDescending(m => m.StartAt)
            .ToList();

        var totalCount = filtered.Count;
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Max(filter.PageSize, 1);

        var items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MeetingListItemDto(
                m.Id, m.Title, m.MeetingTypeId, typeNameById.GetValueOrDefault(m.MeetingTypeId, string.Empty),
                m.StartAt, m.EndAt, m.OrganizerUserId, m.Lifecycle,
                attendeesByMeeting.GetValueOrDefault(m.Id, []).Contains(callerId),
                hasLinkedTasks.Contains(m.Id)))
            .ToList();

        return Response<MeetingListResultDto>.Success(new MeetingListResultDto(items, totalCount), 200, query.CorrelationId);
    }
}

public sealed class GetLinkedTasksHandler : IRequestHandler<GetLinkedTasksQuery, Response<IReadOnlyList<LinkedTaskDto>>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly IRecordLinkService _recordLinks;
    private readonly IRelatedRecordResolverRegistry _resolvers;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActorPermissionContext _permissions;

    public GetLinkedTasksHandler(
        IMeetingRepository meetings,
        IMeetingAttendeeRepository attendees,
        IRecordLinkService recordLinks,
        IRelatedRecordResolverRegistry resolvers,
        ICurrentUserContext currentUser,
        IActorPermissionContext permissions)
    {
        _meetings = meetings;
        _attendees = attendees;
        _recordLinks = recordLinks;
        _resolvers = resolvers;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    public async Task<Response<IReadOnlyList<LinkedTaskDto>>> Handle(GetLinkedTasksQuery query, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(query.MeetingId, ct);
        if (meeting is null)
        {
            return Response<IReadOnlyList<LinkedTaskDto>>.Fail(
                "The meeting does not exist.", 404, MeetingReasonCodes.NotFound, query.CorrelationId);
        }

        var attendeeIds = (await _attendees.ListByMeetingIdAsync(query.MeetingId, ct)).Select(a => a.UserId).ToHashSet();
        var hasReadAll = _permissions.IsPlatformActor || _permissions.Has(MeetingPermissions.ReadAll);
        if (!MeetingEligibility.CanView(meeting, _currentUser.UserId, hasReadAll, attendeeIds))
        {
            return Response<IReadOnlyList<LinkedTaskDto>>.Fail(
                "The meeting does not exist.", 404, MeetingReasonCodes.NotFound, query.CorrelationId);
        }

        var bySource = await _recordLinks.ListBySourceAsync([query.MeetingId], ct);
        var byTarget = await _recordLinks.ListByTargetAsync([query.MeetingId], ct);
        var links = bySource.Concat(byTarget).DistinctBy(l => l.Id).ToList();

        if (!_resolvers.TryGet(RecordLinkModuleCodes.Tasks, out var taskResolver))
        {
            return Response<IReadOnlyList<LinkedTaskDto>>.Success([], 200, query.CorrelationId);
        }

        var taskSideLinks = links
            .Where(l => l.SourceModuleCode == RecordLinkModuleCodes.Tasks || l.TargetModuleCode == RecordLinkModuleCodes.Tasks)
            .Select(l => (Link: l, TaskId: l.SourceModuleCode == RecordLinkModuleCodes.Tasks ? l.SourceRecordId : l.TargetRecordId))
            .ToList();

        var resolved = await taskResolver.ResolveAsync(taskSideLinks.Select(x => x.TaskId).Distinct().ToList(), ct);

        // A dangling link (far end deleted/cross-tenant) is DROPPED, never rendered as "a task" with no name
        // behind it — the same rule the pack states for relatedRecords whose far end cannot be read.
        var result = taskSideLinks
            .Where(x => resolved.ContainsKey(x.TaskId))
            .Select(x =>
            {
                var summary = resolved[x.TaskId];
                return new LinkedTaskDto(x.Link.Id, x.Link.LinkType, x.TaskId.ToString(), summary.Title, summary.Link);
            })
            .ToList();

        return Response<IReadOnlyList<LinkedTaskDto>>.Success(result, 200, query.CorrelationId);
    }
}

public sealed class GetMeetingTypeListHandler : IRequestHandler<GetMeetingTypeListQuery, Response<IReadOnlyList<MeetingTypeDto>>>
{
    private readonly IMeetingTypeRepository _types;

    public GetMeetingTypeListHandler(IMeetingTypeRepository types) => _types = types;

    public async Task<Response<IReadOnlyList<MeetingTypeDto>>> Handle(GetMeetingTypeListQuery query, CancellationToken ct)
    {
        var types = await _types.ListAsync(ct);
        IReadOnlyList<MeetingTypeDto> dtos = types.Select(MeetingTypeMapping.ToDto).ToList();
        return Response<IReadOnlyList<MeetingTypeDto>>.Success(dtos, 200, query.CorrelationId);
    }
}

public sealed class GetMeetingTypeByIdHandler : IRequestHandler<GetMeetingTypeByIdQuery, Response<MeetingTypeDto>>
{
    private readonly IMeetingTypeRepository _types;

    public GetMeetingTypeByIdHandler(IMeetingTypeRepository types) => _types = types;

    public async Task<Response<MeetingTypeDto>> Handle(GetMeetingTypeByIdQuery query, CancellationToken ct)
    {
        var type = await _types.GetByIdAsync(query.Id, ct);
        if (type is null)
        {
            return Response<MeetingTypeDto>.Fail("The meeting type does not exist.", 404, MeetingReasonCodes.TypeNotFound, query.CorrelationId);
        }

        return Response<MeetingTypeDto>.Success(MeetingTypeMapping.ToDto(type), 200, query.CorrelationId);
    }
}
