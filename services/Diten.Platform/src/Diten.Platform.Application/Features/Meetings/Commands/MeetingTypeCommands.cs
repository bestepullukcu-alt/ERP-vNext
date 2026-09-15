using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.Meetings.Commands;

public sealed record CreateMeetingTypeCommand(CreateMeetingTypeRequest Request, string CorrelationId)
    : IRequest<Response<MeetingTypeDto>>;

public sealed record UpdateMeetingTypeCommand(Guid Id, UpdateMeetingTypeRequest Request, string CorrelationId)
    : IRequest<Response<NoContent>>;

public sealed record DeleteMeetingTypeCommand(Guid Id, string CorrelationId)
    : IRequest<Response<NoContent>>;
