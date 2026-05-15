using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAccount.Commands;

public sealed record UpdatePlatformAccountSettingsCommand(UpdatePlatformAccountSettingsRequest Request)
    : IRequest<Response<NoContent>>;
