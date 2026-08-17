using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.InterfaceRegistry.Auditing;
using Diten.Platform.Application.Features.InterfaceRegistry.Commands;
using Diten.Platform.Domain.Entities.InterfaceRegistry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Handlers.CommandHandlers;

public sealed class RejectInterfaceDiscoveryBatchRequestHandler
    : IRequestHandler<RejectInterfaceDiscoveryBatchRequest, Response<InterfaceReviewBatchResultDto>>
{
    private readonly IInterfaceRegistryRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IInterfaceRegistryAuditSink _auditSink;

    public RejectInterfaceDiscoveryBatchRequestHandler(
        IInterfaceRegistryRepository repository,
        ICurrentUserContext currentUser,
        IInterfaceRegistryAuditSink auditSink)
    {
        _repository = repository;
        _currentUser = currentUser;
        _auditSink = auditSink;
    }

    public async Task<Response<InterfaceReviewBatchResultDto>> Handle(RejectInterfaceDiscoveryBatchRequest request, CancellationToken ct)
    {
        var batch = await _repository.GetBatchByIdAsync(request.BatchId, ct);
        if (batch is null)
        {
            return Response<InterfaceReviewBatchResultDto>.Fail("Discovery batch not found.", 404);
        }

        var actor = InterfaceRegistryReviewSupport.ResolveActor(_currentUser);
        var now = DateTimeOffset.UtcNow;
        var diffItems = await _repository.GetDiffItemsAsync(batch.BatchId, ct);

        foreach (var diffItem in diffItems.Where(x => x.ReviewStatus == InterfaceRegistryStatuses.PendingReview))
        {
            await InterfaceRegistryReviewSupport.RejectAsync(diffItem, request.ReviewReason, _repository, _auditSink, actor, now, ct);
        }

        diffItems = await _repository.GetDiffItemsAsync(batch.BatchId, ct);
        await InterfaceRegistryReviewSupport.UpdateBatchStatusAsync(batch, diffItems, _repository, ct);
        return Response<InterfaceReviewBatchResultDto>.Success(InterfaceRegistryReviewSupport.ToBatchResult(batch, diffItems));
    }
}
