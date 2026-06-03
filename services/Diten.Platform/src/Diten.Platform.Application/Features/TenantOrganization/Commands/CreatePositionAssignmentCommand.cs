using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Commands;

public sealed record CreatePositionAssignmentCommand(PositionAssignmentRequest Request) : IRequest<Response<Guid>>;
