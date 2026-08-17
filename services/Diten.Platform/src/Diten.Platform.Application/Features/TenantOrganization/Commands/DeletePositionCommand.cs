using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Commands;

public sealed record DeletePositionCommand(Guid Id) : IRequest<Response<NoContent>>;
