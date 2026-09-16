using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PositionAssignments.Commands;

public sealed record UpdatePositionAssignmentReferenceLinkCommand(Guid Id, PositionAssignmentReferenceLinkRequest Request)
    : IRequest<Response<PositionAssignmentDto>>;
