using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Meetings.Queries;

public sealed record GetMeetingListQuery(GetMeetingListFilter Filter, string CorrelationId)
    : IRequest<Response<MeetingListResultDto>>;

/// <summary>D3 visibility (organizer ∨ attendee ∨ <c>read-all</c>) is applied by the handler, not by a filter on
/// the query — a caller with no relationship to the record gets 404, never a metadata leak.</summary>
public sealed record GetMeetingByIdQuery(Guid Id, string CorrelationId) : IRequest<Response<MeetingDto>>;

public sealed record GetLinkedTasksQuery(Guid MeetingId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<LinkedTaskDto>>>;

public sealed record GetMeetingTypeListQuery(string CorrelationId) : IRequest<Response<IReadOnlyList<MeetingTypeDto>>>;

public sealed record GetMeetingTypeByIdQuery(Guid Id, string CorrelationId) : IRequest<Response<MeetingTypeDto>>;
