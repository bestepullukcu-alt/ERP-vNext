using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PositionAssignments.Commands;

public sealed record UpdatePositionAssignmentCommand(Guid Id, PositionAssignmentUpdateRequest Request)
    : IRequest<Response<PositionAssignmentDto>>;
