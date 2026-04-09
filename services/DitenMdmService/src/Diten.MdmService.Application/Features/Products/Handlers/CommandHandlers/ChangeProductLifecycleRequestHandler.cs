using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Handlers.CommandHandlers;

public sealed class ChangeProductLifecycleRequestHandler : IRequestHandler<ChangeProductLifecycleRequest, bool>
{
    private readonly IProductRepository _productRepository;
    private readonly IItemLookupRepository _lookupRepository;
    private readonly IProductLifecycleHistoryRepository _historyRepository;

    public ChangeProductLifecycleRequestHandler(
        IProductRepository productRepository,
        IItemLookupRepository lookupRepository,
        IProductLifecycleHistoryRepository historyRepository)
    {
        _productRepository = productRepository;
        _lookupRepository = lookupRepository;
        _historyRepository = historyRepository;
    }

    public async Task<bool> Handle(ChangeProductLifecycleRequest request, CancellationToken cancellationToken)
    {
        // PERFORMANCE: Seed data calls removed from hot path.
        var existing = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        var currentState = await _lookupRepository.GetLifecycleStateByIdAsync(existing.LifecycleStateId, cancellationToken)
            ?? throw new KeyNotFoundException("Current lifecycle state not found.");
        var targetState = await _lookupRepository.GetLifecycleStateByIdAsync(request.LifecycleStateId, cancellationToken)
            ?? throw new KeyNotFoundException("Target lifecycle state not found.");

        ProductLogicHelper.ValidateLifecycleTransition(currentState.Code, targetState.Code, request.Reason);

        if (existing.LifecycleStateId == request.LifecycleStateId)
        {
            return true;
        }

        existing.LifecycleStateId = request.LifecycleStateId;
        var updated = await _productRepository.UpdateAsync(existing, cancellationToken);
        if (!updated)
        {
            return false;
        }

        await _historyRepository.CreateAsync(new ProductLifecycleHistory
        {
            ProductId = existing.Id,
            FromState = currentState.Code,
            ToState = targetState.Code,
            ChangedBy = string.IsNullOrWhiteSpace(request.ChangedBy) ? "system" : request.ChangedBy.Trim(),
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
        }, cancellationToken);

        return true;
    }
}
