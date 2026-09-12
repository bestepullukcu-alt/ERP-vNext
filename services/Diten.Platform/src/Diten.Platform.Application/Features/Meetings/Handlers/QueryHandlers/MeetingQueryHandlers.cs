using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Meetings.Queries;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Queries;
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

        // MOD-0357 S7 — both cross-links resolved for display; neither failure (a dangling ancestor, no
        // continuation yet) is an error for THIS read, so both are simply null rather than refused.
        var followUpOf = meeting.FollowUpOfMeetingId is { } followUpId
            ? await _meetings.GetByIdAsync(followUpId, ct)
            : null;
        var followedBy = await _meetings.FindByFollowUpOfMeetingIdAsync(query.Id, ct);

        return Response<MeetingDto>.Success(
            MeetingEligibility.ToDto(meeting, type?.Name ?? string.Empty, attendees, agendaItems, followUpOfMeetingTitle: followUpOf?.Title, followedByMeeting: followedBy),
            200, query.CorrelationId);
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
        // MOD-0357 S7 — `all` already holds every meeting in the tenant; titles for FollowUpOfMeetingId are
        // resolved from THAT same in-memory list, never a second query per row.
        var titleById = all.ToDictionary(m => m.Id, m => m.Title);

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
                hasLinkedTasks.Contains(m.Id),
                m.FollowUpOfMeetingId,
                m.FollowUpOfMeetingId is { } sourceId ? titleById.GetValueOrDefault(sourceId) : null))
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
                return new LinkedTaskDto(
                    x.Link.Id, x.Link.LinkType, x.TaskId.ToString(), summary.Title, summary.Link,
                    x.Link.CreatedAfterMinutesPublished);
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

/// <summary>S3 — the attendee picker's own data source. Forwards to MOD-0024's own person-lookup query
/// (<c>Purpose: Decision</c>, scope-exempt per D2) rather than resolving eligible users a second way.</summary>
public sealed class GetMeetingAttendeeLookupHandler
    : IRequestHandler<GetMeetingAttendeeLookupQuery, Response<AssignablePersonLookupDto>>
{
    private readonly IMediator _mediator;

    public GetMeetingAttendeeLookupHandler(IMediator mediator) => _mediator = mediator;

    public Task<Response<AssignablePersonLookupDto>> Handle(GetMeetingAttendeeLookupQuery query, CancellationToken ct)
        => _mediator.Send(new GetTaskAssignmentPersonLookupQuery(query.CorrelationId, TaskPersonLookupPurpose.Decision), ct);
}

/// <summary>S6 — every minutes version for this meeting, newest first. Same D3 visibility gate every other
/// meeting-scoped read applies (organizer ∨ attendee ∨ <c>read-all</c>) — a caller with no relationship to the
/// meeting gets 404, never a metadata leak, exactly like <c>GetLinkedTasksHandler</c>.</summary>
public sealed class GetMeetingMinutesHandler : IRequestHandler<GetMeetingMinutesQuery, Response<MeetingMinutesDto>>
{
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingAttendeeRepository _attendees;
    private readonly IMeetingMinutesVersionRepository _minutes;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActorPermissionContext _permissions;
    private readonly IUserDisplayNameResolver _displayNames;

    public GetMeetingMinutesHandler(
        IMeetingRepository meetings,
        IMeetingAttendeeRepository attendees,
        IMeetingMinutesVersionRepository minutes,
        ICurrentUserContext currentUser,
        IActorPermissionContext permissions,
        IUserDisplayNameResolver displayNames)
    {
        _meetings = meetings;
        _attendees = attendees;
        _minutes = minutes;
        _currentUser = currentUser;
        _permissions = permissions;
        _displayNames = displayNames;
    }

    public async Task<Response<MeetingMinutesDto>> Handle(GetMeetingMinutesQuery query, CancellationToken ct)
    {
        var meeting = await _meetings.GetByIdAsync(query.MeetingId, ct);
        if (meeting is null)
        {
            return Response<MeetingMinutesDto>.Fail(
                "The meeting does not exist.", 404, MeetingReasonCodes.NotFound, query.CorrelationId);
        }

        var attendeeIds = (await _attendees.ListByMeetingIdAsync(query.MeetingId, ct)).Select(a => a.UserId).ToHashSet();
        var hasReadAll = _permissions.IsPlatformActor || _permissions.Has(MeetingPermissions.ReadAll);
        if (!MeetingEligibility.CanView(meeting, _currentUser.UserId, hasReadAll, attendeeIds))
        {
            return Response<MeetingMinutesDto>.Fail(
                "The meeting does not exist.", 404, MeetingReasonCodes.NotFound, query.CorrelationId);
        }

        var versions = await _minutes.ListByMeetingIdAsync(query.MeetingId, ct);
        var dtos = new List<MeetingMinutesVersionDto>(versions.Count);
        foreach (var version in versions)
        {
            dtos.Add(await MinutesEligibility.ToDtoAsync(version, _displayNames, ct));
        }

        return Response<MeetingMinutesDto>.Success(new MeetingMinutesDto(dtos), 200, query.CorrelationId);
    }
}

/// <summary>S3 — the type dropdown's own data source (pack §12: the type must resolve and be active).</summary>
public sealed class GetMeetingTypeLookupHandler
    : IRequestHandler<GetMeetingTypeLookupQuery, Response<IReadOnlyList<MeetingTypeLookupItemDto>>>
{
    private readonly IMeetingTypeRepository _types;

    public GetMeetingTypeLookupHandler(IMeetingTypeRepository types) => _types = types;

    public async Task<Response<IReadOnlyList<MeetingTypeLookupItemDto>>> Handle(
        GetMeetingTypeLookupQuery query, CancellationToken ct)
    {
        var types = await _types.ListAsync(ct);
        IReadOnlyList<MeetingTypeLookupItemDto> items = types
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => new MeetingTypeLookupItemDto(t.Id, t.Name, t.AgendaTemplate))
            .ToList();

        return Response<IReadOnlyList<MeetingTypeLookupItemDto>>.Success(items, 200, query.CorrelationId);
    }
}

// ── S11 — recurring meeting series ──────────────────────────────────────────────────────────────────────────

public sealed class GetMeetingSeriesListHandler
    : IRequestHandler<GetMeetingSeriesListQuery, Response<IReadOnlyList<MeetingSeriesDto>>>
{
    private readonly IMeetingSeriesRepository _series;
    private readonly IMeetingTypeRepository _types;

    public GetMeetingSeriesListHandler(IMeetingSeriesRepository series, IMeetingTypeRepository types)
    {
        _series = series;
        _types = types;
    }

    public async Task<Response<IReadOnlyList<MeetingSeriesDto>>> Handle(
        GetMeetingSeriesListQuery query, CancellationToken ct)
    {
        var all = await _series.ListAllAsync(ct);
        var types = await _types.ListAsync(ct);
        var typeNameById = types.ToDictionary(t => t.Id, t => t.Name);

        IReadOnlyList<MeetingSeriesDto> dtos = all
            .Select(s => MeetingSeriesMapping.ToDto(s, typeNameById.GetValueOrDefault(s.MeetingTypeId)))
            .ToList();
        return Response<IReadOnlyList<MeetingSeriesDto>>.Success(dtos, 200, query.CorrelationId);
    }
}

public sealed class GetMeetingSeriesByIdHandler : IRequestHandler<GetMeetingSeriesByIdQuery, Response<MeetingSeriesDto>>
{
    private readonly IMeetingSeriesRepository _series;
    private readonly IMeetingTypeRepository _types;

    public GetMeetingSeriesByIdHandler(IMeetingSeriesRepository series, IMeetingTypeRepository types)
    {
        _series = series;
        _types = types;
    }

    public async Task<Response<MeetingSeriesDto>> Handle(GetMeetingSeriesByIdQuery query, CancellationToken ct)
    {
        var series = await _series.GetByIdAsync(query.Id, ct);
        if (series is null)
        {
            return Response<MeetingSeriesDto>.Fail("The meeting series does not exist.", 404, MeetingReasonCodes.SeriesNotFound, query.CorrelationId);
        }

        var type = await _types.GetByIdAsync(series.MeetingTypeId, ct);
        return Response<MeetingSeriesDto>.Success(MeetingSeriesMapping.ToDto(series, type?.Name), 200, query.CorrelationId);
    }
}
