using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Handlers.QueryHandlers;

public sealed class GetBrandProductsHandler
    : IRequestHandler<Queries.GetBrandProductsQuery, Response<IReadOnlyList<BrandProductRowDto>>>
{
    private readonly IBrandRepository _brandRepository;
    private readonly IProductRepository _productRepository;

    public GetBrandProductsHandler(IBrandRepository brandRepository, IProductRepository productRepository)
    {
        _brandRepository = brandRepository;
        _productRepository = productRepository;
    }

    public async Task<Response<IReadOnlyList<BrandProductRowDto>>> Handle(
        Queries.GetBrandProductsQuery request, CancellationToken cancellationToken)
    {
        // The brand must exist in this tenant before its products are listed, otherwise an unknown/foreign id
        // would silently return an empty list and read as "brand with no products".
        var brand = await _brandRepository.GetByIdAsync(request.BrandId, cancellationToken);
        if (brand is null)
        {
            return BrandProductFailures.Fail<IReadOnlyList<BrandProductRowDto>>(
                BrandProductReasonCodes.BrandNotFound, "Brand not found.", 404);
        }

        var products = await _productRepository.GetByBrandAsync(request.BrandId, cancellationToken);
        IEnumerable<Domain.Entities.Product> filtered = products;

        if (!request.IncludeArchived)
        {
            filtered = filtered.Where(x => !x.IsArchived);
        }

        IReadOnlyList<BrandProductRowDto> items = filtered
            .OrderBy(x => x.ProductName, StringComparer.OrdinalIgnoreCase)
            .Select(BrandMappings.ToBrandProductRow)
            .ToList();

        return Response<IReadOnlyList<BrandProductRowDto>>.Success(items);
    }
}
