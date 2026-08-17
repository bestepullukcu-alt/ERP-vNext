using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Commands;

public sealed record ValidateHrisSourceConnectionCommand(Guid Id) : IRequest<Response<NoContent>>;
