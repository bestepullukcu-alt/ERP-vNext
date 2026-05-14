using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Commands;

public sealed record RejectInterfaceDiffItemRequest(Guid DiffItemId, string ReviewReason)
    : IRequest<Response<InterfaceDiscoveryDiffItemDto>>;
