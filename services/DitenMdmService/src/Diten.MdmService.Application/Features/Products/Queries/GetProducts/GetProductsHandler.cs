using Diten.Shared.Core;
using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Queries.GetProducts;

public sealed class GetProductsHandler : IRequestHandler<GetProductsQuery, Response<IReadOnlyList<ProductListDto>>>
{
    private readonly IProductRepository _productRepository;
    private readonly IItemCategoryRepository _categoryRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetProductsHandler(
        IProductRepository productRepository,
        IItemCategoryRepository categoryRepository,
        IItemLookupRepository lookupRepository)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<Response<IReadOnlyList<ProductListDto>>> Handle(GetProductsQuery request, CancellationToken ct)
    {
        var products = await _productRepository.GetAllAsync(ct);
        var categories = await _categoryRepository.GetAllAsync(ct);
        var lifecycleStates = await _lookupRepository.GetLifecycleStatesAsync(ct);

        var categoryMap = categories.ToDictionary(x => x.Id);
        var lifecycleMap = lifecycleStates.ToDictionary(x => x.Id);

        var list = products.Select(p => new ProductListDto
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            ProductTypeCode = MapProductTypeCode(p.ProductType),
            Category = categoryMap.TryGetValue(p.CategoryId, out var cat) ? cat.Name : string.Empty,
            LifecycleState = lifecycleMap.TryGetValue(p.LifecycleStateId, out var ls) ? ls.Name : string.Empty,
            LifecycleStateCode = lifecycleMap.TryGetValue(p.LifecycleStateId, out var ls2) ? ls2.Code : string.Empty,
            IsSaleable = p.IsSaleable,
            IsPurchasable = p.IsPurchasable,
            IsManufacturable = p.IsManufacturable
        }).ToList();

        return Response<IReadOnlyList<ProductListDto>>.Success(list);
    }

    private static string MapProductTypeCode(int typeId) => typeId switch
    {
        1 => "FINISHED_PRODUCT",
        2 => "SEMI_FINISHED_PRODUCT",
        3 => "SERVICE",
        4 => "TECHNOLOGY",
        _ => "UNKNOWN"
    };
}
