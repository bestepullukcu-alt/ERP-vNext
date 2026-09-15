using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave.Commands;

public sealed record CreateTimeAttendanceLeaveReadinessCommand(TimeAttendanceLeaveReadinessCreateRequest Request) : IRequest<Response<Guid>>;
