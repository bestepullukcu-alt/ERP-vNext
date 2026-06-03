using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Commands;

public sealed record UpdatePositionCommand(Guid Id, PositionRequest Request) : IRequest<Response<NoContent>>;
