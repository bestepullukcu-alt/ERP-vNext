using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Commands;

public sealed record ConfirmInterfaceDiscoveryBatchRequest(Guid BatchId) : IRequest<Response<InterfaceReviewBatchResultDto>>;
