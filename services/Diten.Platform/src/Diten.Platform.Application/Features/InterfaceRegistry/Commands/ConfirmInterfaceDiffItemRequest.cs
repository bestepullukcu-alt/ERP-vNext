using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Commands;

public sealed record ConfirmInterfaceDiffItemRequest(Guid DiffItemId) : IRequest<Response<InterfaceDiscoveryDiffItemDto>>;
