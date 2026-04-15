using Diten.Shared.Core;
using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Commands.BulkDeleteProducts;

public sealed class BulkDeleteProductsHandler : IRequestHandler<BulkDeleteProductsCommand, Response<int>>
{
    private readonly IProductRepository _productRepository;

    public BulkDeleteProductsHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Response<int>> Handle(BulkDeleteProductsCommand request, CancellationToken ct)
    {
        if (request.Ids == null || request.Ids.Count == 0)
        {
            return Response<int>.Success(0);
        }

        var deletedCount = await _productRepository.BulkDeleteAsync(request.Ids, ct);
        return Response<int>.Success(deletedCount);
    }
}
