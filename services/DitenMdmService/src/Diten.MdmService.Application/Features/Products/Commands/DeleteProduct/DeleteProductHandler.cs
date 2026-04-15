using Diten.Shared.Core;
using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Commands.DeleteProduct;

public sealed class DeleteProductHandler : IRequestHandler<DeleteProductCommand, Response<bool>>
{
    private readonly IProductRepository _productRepository;

    public DeleteProductHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Response<bool>> Handle(DeleteProductCommand request, CancellationToken ct)
    {
        if (!await _productRepository.ExistsAsync(request.Id, ct))
        {
            return Response<bool>.Fail("Product not found.", 404);
        }

        await _productRepository.DeleteAsync(request.Id, ct);
        return Response<bool>.Success(true);
    }
}
