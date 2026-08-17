using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.HrisSources.Commands;

public sealed record UpdateHrisSourceProfileCommand(Guid Id, HrisSourceProfileUpdateRequest Request) : IRequest<Response<NoContent>>;
