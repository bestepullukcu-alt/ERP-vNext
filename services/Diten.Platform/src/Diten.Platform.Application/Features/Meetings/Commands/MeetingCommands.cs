using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Meetings.Commands;

public sealed record CreateMeetingCommand(CreateMeetingRequest Request, string CorrelationId)
    : IRequest<Response<MeetingDto>>;

public sealed record UpdateMeetingCommand(Guid Id, UpdateMeetingRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

public sealed record CancelMeetingCommand(Guid Id, CancelMeetingRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

public sealed record ReassignMeetingOrganizerCommand(
    Guid Id, ReassignMeetingOrganizerRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

public sealed record AddMeetingAttendeesCommand(
    Guid MeetingId, AddMeetingAttendeesRequest Request, string CorrelationId)
    : IRequest<Response<AddMeetingAttendeesResultDto>>;

public sealed record RemoveMeetingAttendeeCommand(Guid MeetingId, Guid UserId, string CorrelationId)
    : IRequest<Response<NoContent>>;

public sealed record AddAgendaItemCommand(Guid MeetingId, AddAgendaItemRequest Request, string CorrelationId)
    : IRequest<Response<AgendaItemDto>>;

public sealed record UpdateAgendaItemCommand(
    Guid MeetingId, Guid AgendaItemId, UpdateAgendaItemRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

public sealed record DeleteAgendaItemCommand(Guid MeetingId, Guid AgendaItemId, string CorrelationId)
    : IRequest<Response<NoContent>>;

public sealed record ReorderAgendaCommand(Guid MeetingId, ReorderAgendaRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

// ── S4 — the meeting↔task bridge ────────────────────────────────────────────────────────────────────────────

public sealed record CreateTaskFromMeetingCommand(
    Guid MeetingId, CreateTaskFromMeetingRequest Request, string CorrelationId)
    : IRequest<Response<CreateTaskFromMeetingResultDto>>;

public sealed record LinkExistingTaskCommand(Guid MeetingId, LinkExistingTaskRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

public sealed record ScheduleReviewMeetingForTaskCommand(
    Guid TaskId, ScheduleReviewMeetingForTaskRequest Request, string CorrelationId)
    : IRequest<Response<ScheduleReviewMeetingForTaskResultDto>>;

// ── S5 — invitation response ─────────────────────────────────────────────────────────────────────────────────

public sealed record RespondToInvitationCommand(
    Guid MeetingId, RespondToInvitationRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;
