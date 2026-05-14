using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.InterfaceRegistry.Auditing;
using Diten.Platform.Application.Features.InterfaceRegistry.Commands;
using Diten.Platform.Domain.Entities.InterfaceRegistry;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.InterfaceRegistry.Handlers.CommandHandlers;

public sealed class RejectInterfaceDiffItemRequestHandler
    : IRequestHandler<RejectInterfaceDiffItemRequest, Response<InterfaceDiscoveryDiffItemDto>>
{
    private readonly IInterfaceRegistryRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IInterfaceRegistryAuditSink _auditSink;

    public RejectInterfaceDiffItemRequestHandler(
        IInterfaceRegistryRepository repository,
        ICurrentUserContext currentUser,
        IInterfaceRegistryAuditSink auditSink)
    {
        _repository = repository;
        _currentUser = currentUser;
        _auditSink = auditSink;
    }

    public async Task<Response<InterfaceDiscoveryDiffItemDto>> Handle(RejectInterfaceDiffItemRequest request, CancellationToken ct)
    {
        var diffItem = await _repository.GetDiffItemByIdAsync(request.DiffItemId, ct);
        if (diffItem is null)
        {
            return Response<InterfaceDiscoveryDiffItemDto>.Fail("Diff item not found.", 404);
        }

        if (diffItem.ReviewStatus == InterfaceRegistryStatuses.Confirmed)
        {
            return Response<InterfaceDiscoveryDiffItemDto>.Fail("Confirmed diff items cannot be rejected.", 409);
        }

        if (diffItem.ReviewStatus == InterfaceRegistryStatuses.Rejected)
        {
            return Response<InterfaceDiscoveryDiffItemDto>.Success(InterfaceRegistryMapper.ToDto(diffItem));
        }

        await InterfaceRegistryReviewSupport.RejectAsync(
            diffItem,
            request.ReviewReason,
            _repository,
            _auditSink,
            InterfaceRegistryReviewSupport.ResolveActor(_currentUser),
            DateTimeOffset.UtcNow,
            ct);

        var batch = await _repository.GetBatchByIdAsync(diffItem.BatchId, ct);
        if (batch is not null)
        {
            var diffItems = await _repository.GetDiffItemsAsync(batch.BatchId, ct);
            await InterfaceRegistryReviewSupport.UpdateBatchStatusAsync(batch, diffItems, _repository, ct);
        }

        return Response<InterfaceDiscoveryDiffItemDto>.Success(InterfaceRegistryMapper.ToDto(diffItem));
    }
}
