using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave.Commands;

public sealed record DeleteTimeAttendanceLeaveReadinessCommand(Guid Id) : IRequest<Response<bool>>;
