using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Commands;

public sealed record BulkDeletePlatformAdministratorsCommand(IReadOnlyList<PlatformAdministratorBulkDeleteItemRequest> Items)
    : IRequest<Response<NoContent>>;
