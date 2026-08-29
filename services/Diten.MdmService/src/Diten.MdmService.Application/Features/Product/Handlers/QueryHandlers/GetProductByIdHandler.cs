using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Product.Handlers.QueryHandlers;

public sealed class GetProductByIdHandler : IRequestHandler<Queries.GetProductByIdQuery, Response<ProductDetailDto>>
{
    private readonly IProductRepository _repository;

    public GetProductByIdHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<ProductDetailDto>> Handle(Queries.GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.ProductId, cancellationToken);

        // Archived products stay readable — archiving closes writes, not history.
        return entity is null
            ? BrandProductFailures.Fail<ProductDetailDto>(BrandProductReasonCodes.ProductNotFound, "Product not found.", 404)
            : Response<ProductDetailDto>.Success(ProductMappings.ToDetailDto(entity));
    }
}
