using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Product.Handlers.QueryHandlers;

public sealed class GetProductByIdHandler : IRequestHandler<Queries.GetProductByIdQuery, Response<ProductDetailDto>>
{
    private readonly IProductRepository _repository;
    private readonly IBrandRepository _brands;

    public GetProductByIdHandler(IProductRepository repository, IBrandRepository brands)
    {
        _repository = repository;
        _brands = brands;
    }

    public async Task<Response<ProductDetailDto>> Handle(Queries.GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.ProductId, cancellationToken);

        // Archived products stay readable — archiving closes writes, not history.
        if (entity is null)
        {
            return BrandProductFailures.Fail<ProductDetailDto>(BrandProductReasonCodes.ProductNotFound, "Product not found.", 404);
        }

        // FIX3 (R-4) — the product's own brand, resolved in this tenant (the repository is tenant-scoped).
        var brand = entity.BrandId is { } brandId && brandId != Guid.Empty
            ? await _brands.GetByIdAsync(brandId, cancellationToken)
            : null;
        var linked = brand is null
            ? null
            : new ProductLinkedBrandDto(brand.Id, brand.BrandCode, brand.BrandName, brand.IsLinkableAt(DateTimeOffset.UtcNow));
        return Response<ProductDetailDto>.Success(ProductMappings.ToDetailDto(entity) with { LinkedBrand = linked });
    }
}
