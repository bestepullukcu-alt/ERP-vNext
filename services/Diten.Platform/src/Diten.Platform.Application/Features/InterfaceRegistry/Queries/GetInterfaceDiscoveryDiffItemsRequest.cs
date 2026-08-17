using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Queries;

public sealed record GetInterfaceDiscoveryDiffItemsRequest(Guid BatchId) : IRequest<Response<IReadOnlyList<InterfaceDiscoveryDiffItemDto>>>;
