using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave.Commands;

public sealed record EvaluateTimeAttendanceLeaveReadinessCommand(Guid Id) : IRequest<Response<TimeAttendanceLeaveReadinessDto>>;
