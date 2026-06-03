using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Commands;

public sealed record UpdatePositionAssignmentCommand(Guid Id, PositionAssignmentRequest Request) : IRequest<Response<NoContent>>;
