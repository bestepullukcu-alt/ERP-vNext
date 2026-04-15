using Diten.Shared.Core;
using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Queries.GetProductById;

public sealed class GetProductByIdHandler : IRequestHandler<GetProductByIdQuery, Response<ProductDto>>
{
    private readonly IProductRepository _productRepository;
    private readonly IItemCategoryRepository _categoryRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetProductByIdHandler(
        IProductRepository productRepository,
        IItemCategoryRepository categoryRepository,
        IItemLookupRepository lookupRepository)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<Response<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken ct)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, ct);
        if (product is null)
        {
            return Response<ProductDto>.Fail("Product not found.", 404);
        }

        var category = await _categoryRepository.GetByIdAsync(product.CategoryId, ct);
        var lifecycle = await _lookupRepository.GetLifecycleStateByIdAsync(product.LifecycleStateId, ct);

        var dto = new ProductDto
        {
            Id = product.Id,
            Code = product.Code,
            Name = product.Name,
            ShortName = product.ShortName,
            Description = product.Description,
            ProductType = product.ProductType,
            CategoryId = product.CategoryId,
            CategoryCode = category?.Code ?? string.Empty,
            CategoryName = category?.Name ?? string.Empty,
            LifecycleStateId = product.LifecycleStateId,
            LifecycleStateCode = lifecycle?.Code ?? string.Empty,
            LifecycleStateName = lifecycle?.Name ?? string.Empty,
            IsSaleable = product.IsSaleable,
            IsPurchasable = product.IsPurchasable,
            IsManufacturable = product.IsManufacturable,
            CreatedAt = product.CreatedAt
        };

        return Response<ProductDto>.Success(dto);
    }
}
