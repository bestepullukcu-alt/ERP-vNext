using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Handlers.QueryHandlers;

public sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDetailDto?>
{
    private readonly IProductRepository _productRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetProductByIdQueryHandler(IProductRepository productRepository, IItemLookupRepository lookupRepository)
    {
        _productRepository = productRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<ProductDetailDto?> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        // PERFORMANCE: Seed data calls removed from hot path.
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
        {
            return null;
        }

        var category = ProductCatalog.GetCategoryDefinition(product.CategoryId);
        var lifecycleState = await _lookupRepository.GetLifecycleStateByIdAsync(product.LifecycleStateId, cancellationToken)
            ?? throw new KeyNotFoundException("Lifecycle state not found.");

        return ProductMapping.ToDetailDto(product, category, lifecycleState);
    }
}
