using Diten.Shared.Core;
using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Commands.ChangeProductLifecycle;

public sealed class ChangeProductLifecycleHandler : IRequestHandler<ChangeProductLifecycleCommand, Response<bool>>
{
    private readonly IProductRepository _productRepository;

    public ChangeProductLifecycleHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Response<bool>> Handle(ChangeProductLifecycleCommand request, CancellationToken ct)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, ct);
        if (product is null)
        {
            return Response<bool>.Fail("Product not found.", 404);
        }

        product.LifecycleStateId = request.LifecycleStateId;
        var result = await _productRepository.UpdateAsync(product, ct);
        return Response<bool>.Success(result);
    }
}
