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

/// <summary>S6 — every minutes version for this meeting, newest first (draft or published; the editor decides
/// what to render read-only from <see cref="MeetingMinutesVersionDto.Status"/>).</summary>
public sealed record GetMeetingMinutesQuery(Guid MeetingId, string CorrelationId)
    : IRequest<Response<MeetingMinutesDto>>;

public sealed record GetMeetingTypeListQuery(string CorrelationId) : IRequest<Response<IReadOnlyList<MeetingTypeDto>>>;

public sealed record GetMeetingTypeByIdQuery(Guid Id, string CorrelationId) : IRequest<Response<MeetingTypeDto>>;

// ── Lookups (S3) ─────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>D2 — the SAME seam MOD-0024's own assignee picker uses (<c>GetTaskAssignmentPersonLookupQuery</c>,
/// <c>Purpose: Decision</c>, scope-EXEMPT); this query only forwards to it, never a second resolution path.</summary>
public sealed record GetMeetingAttendeeLookupQuery(string CorrelationId)
    : IRequest<Response<Diten.Platform.Application.Features.Tasks.AssignablePersonLookupDto>>;

public sealed record GetMeetingTypeLookupQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<MeetingTypeLookupItemDto>>>;

// ── S11 — recurring meeting series ──────────────────────────────────────────────────────────────────────────

public sealed record GetMeetingSeriesListQuery(string CorrelationId)
    : IRequest<Response<IReadOnlyList<MeetingSeriesDto>>>;

public sealed record GetMeetingSeriesByIdQuery(Guid Id, string CorrelationId)
    : IRequest<Response<MeetingSeriesDto>>;
