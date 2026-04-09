using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Handlers.CommandHandlers;

public sealed class BulkDeleteProductsRequestHandler : IRequestHandler<BulkDeleteProductsRequest, BulkDeleteProductsResponse>
{
    private readonly IProductRepository _productRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public BulkDeleteProductsRequestHandler(IProductRepository productRepository, IItemLookupRepository lookupRepository)
    {
        _productRepository = productRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<BulkDeleteProductsResponse> Handle(BulkDeleteProductsRequest request, CancellationToken cancellationToken)
    {
        var idList = request.Ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new BulkDeleteProductsResponse { DeletedCount = 0 };
        }

        var draftState = await _lookupRepository.GetLifecycleStateByCodeAsync("DRAFT", cancellationToken)
            ?? throw new KeyNotFoundException("DRAFT lifecycle state not found.");

        var products = await _productRepository.GetAllAsync(cancellationToken);
        var targetProducts = products.Where(p => idList.Contains(p.Id)).ToList();

        if (targetProducts.Any(p => p.LifecycleStateId != draftState.Id))
        {
            throw new InvalidOperationException("One or more selected products are not in DRAFT state and cannot be deleted.");
        }

        var deletedCount = await _productRepository.BulkDeleteAsync(idList, cancellationToken);
        return new BulkDeleteProductsResponse { DeletedCount = deletedCount };
    }
}
