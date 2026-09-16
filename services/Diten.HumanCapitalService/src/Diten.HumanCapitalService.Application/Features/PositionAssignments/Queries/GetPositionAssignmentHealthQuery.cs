using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PositionAssignments.Queries;

public sealed record GetPositionAssignmentHealthQuery() : IRequest<Response<PositionAssignmentHealthDto>>;
