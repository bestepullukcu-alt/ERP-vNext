using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetGlobalProductByIdHandler : IRequestHandler<GetGlobalProductByIdQuery, Response<ProductItemSkuMasterModels.GlobalProductDetailDto>>
{
    private readonly IGlobalProductRepository _repository;

    public GetGlobalProductByIdHandler(IGlobalProductRepository repository) => _repository = repository;

    public async Task<Response<ProductItemSkuMasterModels.GlobalProductDetailDto>> Handle(
        GetGlobalProductByIdQuery request,
        CancellationToken cancellationToken)
    {
        var product = await _repository.GetByIdAsync(request.Id, cancellationToken);
        return product is null
            ? Response<ProductItemSkuMasterModels.GlobalProductDetailDto>.Fail("GLOBAL_PRODUCT_NOT_FOUND", 404)
            : Response<ProductItemSkuMasterModels.GlobalProductDetailDto>.Success(new(
                product.Id,
                product.CanonicalCode,
                product.GlobalProductName,
                product.LifecycleStatus,
                product.Version,
                product.CreatedAt,
                product.UpdatedAt));
    }
}
