using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave.Queries;

public sealed record GetTimeAttendanceLeaveReadinessByIdQuery(Guid Id) : IRequest<Response<TimeAttendanceLeaveReadinessDto>>;
