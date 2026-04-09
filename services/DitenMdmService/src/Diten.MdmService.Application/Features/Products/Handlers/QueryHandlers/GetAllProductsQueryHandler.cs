using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Handlers.QueryHandlers;

public sealed class GetAllProductsQueryHandler : IRequestHandler<GetAllProductsQuery, IReadOnlyList<ProductListItemDto>>
{
    private readonly IProductRepository _productRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetAllProductsQueryHandler(IProductRepository productRepository, IItemLookupRepository lookupRepository)
    {
        _productRepository = productRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<IReadOnlyList<ProductListItemDto>> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
    {
        // PERFORMANCE: Seed data calls removed from hot path.
        var products = await _productRepository.GetAllAsync(cancellationToken);
        var lifecycleStates = await _lookupRepository.GetLifecycleStatesAsync(cancellationToken);
        var lifecycleMap = lifecycleStates.ToDictionary(x => x.Id);

        return products
            .Select(product =>
            {
                var category = ProductCatalog.GetCategoryDefinition(product.CategoryId);
                var lifecycleState = lifecycleMap[product.LifecycleStateId];
                return ProductMapping.ToListDto(product, category, lifecycleState);
            })
            .ToList();
    }
}
