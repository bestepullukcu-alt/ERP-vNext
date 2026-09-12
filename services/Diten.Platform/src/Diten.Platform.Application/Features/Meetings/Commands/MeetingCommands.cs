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

// ── S6 — minutes ────────────────────────────────────────────────────────────────────────────────────────────

public sealed record SaveMinutesDraftCommand(
    Guid MeetingId, SaveMinutesDraftRequest Request, string CorrelationId)
    : IRequest<Response<MeetingMinutesVersionDto>>;

public sealed record PublishMinutesCommand(
    Guid MeetingId, PublishMinutesRequest Request, string CorrelationId)
    : IRequest<Response<MeetingMinutesVersionDto>>;

public sealed record CorrectPublishedMinutesCommand(
    Guid MeetingId, CorrectPublishedMinutesRequest Request, string CorrelationId)
    : IRequest<Response<MeetingMinutesVersionDto>>;

// ── S7 — continuation scheduling ────────────────────────────────────────────────────────────────────────────

public sealed record ScheduleFollowUpMeetingCommand(
    Guid SourceMeetingId, ScheduleFollowUpMeetingRequest Request, string CorrelationId)
    : IRequest<Response<ScheduleFollowUpMeetingResultDto>>;

// ── S11 — recurring meeting series ──────────────────────────────────────────────────────────────────────────

public sealed record CreateMeetingSeriesCommand(CreateMeetingSeriesRequest Request, string CorrelationId)
    : IRequest<Response<MeetingSeriesDto>>;

public sealed record UpdateMeetingSeriesCommand(
    Guid Id, UpdateMeetingSeriesRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

public sealed record DeleteMeetingSeriesCommand(Guid Id, string CorrelationId)
    : IRequest<Response<NoContent>>;

/// <summary>Generate whatever the tenant's ACTIVE series owe right now — tenant-scoped, callable on its own
/// (the sweep job runs it inside each tenant's own <c>TenantScope</c>), same shape
/// <c>GenerateDueRecurringTasksCommand</c> already takes for MOD-0024's own recurrence sweep.</summary>
public sealed record GenerateDueMeetingSeriesCommand(
    DateTimeOffset? NowUtc, int MaxSeries, string CorrelationId)
    : IRequest<Response<GenerateDueMeetingSeriesResponse>>;
