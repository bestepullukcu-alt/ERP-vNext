using Diten.Platform.Application.Common;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Commands;

public sealed record RejectInterfaceDiscoveryBatchRequest(Guid BatchId, string ReviewReason)
    : IRequest<Response<InterfaceReviewBatchResultDto>>;
