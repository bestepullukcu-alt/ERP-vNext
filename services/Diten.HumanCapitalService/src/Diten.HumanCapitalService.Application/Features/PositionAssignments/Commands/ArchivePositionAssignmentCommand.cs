using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PositionAssignments.Commands;

public sealed record ArchivePositionAssignmentCommand(Guid Id) : IRequest<Response<bool>>;
