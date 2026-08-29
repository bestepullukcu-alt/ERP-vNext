using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Product.Handlers.QueryHandlers;

public sealed class GetProductListHandler : IRequestHandler<Queries.GetProductListQuery, Response<ProductListResultDto>>
{
    private readonly IProductRepository _repository;

    public GetProductListHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<ProductListResultDto>> Handle(Queries.GetProductListQuery request, CancellationToken cancellationToken)
    {
        var products = await _repository.GetAllAsync(cancellationToken);
        IEnumerable<Domain.Entities.Product> filtered = products;

        if (!request.IncludeArchived)
        {
            filtered = filtered.Where(x => !x.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            filtered = filtered.Where(x =>
                x.ProductCode.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.ProductName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.ProductStatus))
        {
            var status = request.ProductStatus.Trim();
            filtered = filtered.Where(x => string.Equals(x.ProductStatus, status, StringComparison.OrdinalIgnoreCase));
        }

        if (request.BrandId is { } brandId && brandId != Guid.Empty)
        {
            filtered = filtered.Where(x => x.BrandId == brandId);
        }

        if (!string.IsNullOrWhiteSpace(request.ProductType))
        {
            var productType = request.ProductType.Trim();
            filtered = filtered.Where(x => string.Equals(x.ProductType, productType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.DosageForm))
        {
            var dosageForm = request.DosageForm.Trim();
            filtered = filtered.Where(x => string.Equals(x.DosageForm, dosageForm, StringComparison.OrdinalIgnoreCase));
        }

        if (request.TherapeuticAreaId is { } therapeuticAreaId && therapeuticAreaId != Guid.Empty)
        {
            filtered = filtered.Where(x => x.TherapeuticAreaId == therapeuticAreaId);
        }

        var items = filtered
            .OrderBy(x => x.ProductName, StringComparer.OrdinalIgnoreCase)
            .Select(ProductMappings.ToDetailDto)
            .ToList();

        return Response<ProductListResultDto>.Success(new ProductListResultDto(items, items.Count));
    }
}
