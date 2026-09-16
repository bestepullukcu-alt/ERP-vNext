using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PositionAssignments.Queries;

public sealed record GetPositionAssignmentByIdQuery(Guid Id) : IRequest<Response<PositionAssignmentDto>>;
