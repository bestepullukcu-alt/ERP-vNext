using Diten.Shared.Core;
using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Commands.UpdateProduct;

public sealed class UpdateProductHandler : IRequestHandler<UpdateProductCommand, Response<bool>>
{
    private readonly IProductRepository _productRepository;

    public UpdateProductHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Response<bool>> Handle(UpdateProductCommand request, CancellationToken ct)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, ct);
        if (product is null)
        {
            return Response<bool>.Fail("Product not found.", 404);
        }

        if (await _productRepository.ExistsByCodeAsync(request.Code.Trim(), request.Id, ct))
        {
            return Response<bool>.Fail("Product code must be unique.", 400);
        }

        product.Code = request.Code.Trim();
        product.Name = request.Name.Trim();
        product.ShortName = request.ShortName?.Trim();
        product.Description = request.Description?.Trim();
        product.ProductType = request.ProductType;
        product.CategoryId = request.CategoryId;
        product.LifecycleStateId = request.LifecycleStateId;
        product.IsSaleable = request.IsSaleable;
        product.IsPurchasable = request.IsPurchasable;
        product.IsManufacturable = request.IsManufacturable;

        var result = await _productRepository.UpdateAsync(product, ct);
        return Response<bool>.Success(result);
    }
}
