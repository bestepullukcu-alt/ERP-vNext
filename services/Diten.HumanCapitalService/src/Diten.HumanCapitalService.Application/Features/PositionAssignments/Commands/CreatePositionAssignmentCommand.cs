using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PositionAssignments.Commands;

public sealed record CreatePositionAssignmentCommand(PositionAssignmentCreateRequest Request)
    : IRequest<Response<Guid>>;
