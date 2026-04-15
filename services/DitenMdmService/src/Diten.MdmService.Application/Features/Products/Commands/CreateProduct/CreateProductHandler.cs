using Diten.Shared.Core;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.Products.Commands.CreateProduct;

public sealed class CreateProductHandler : IRequestHandler<CreateProductCommand, Response<Guid>>
{
    private readonly IProductRepository _productRepository;

    public CreateProductHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<Response<Guid>> Handle(CreateProductCommand request, CancellationToken ct)
    {
        if (await _productRepository.ExistsByCodeAsync(request.Code.Trim(), null, ct))
        {
            return Response<Guid>.Fail("Product code must be unique.", 400);
        }

        var product = new Product
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            ShortName = request.ShortName?.Trim(),
            Description = request.Description?.Trim(),
            ProductType = request.ProductType,
            CategoryId = request.CategoryId,
            LifecycleStateId = request.LifecycleStateId,
            IsSaleable = request.IsSaleable,
            IsPurchasable = request.IsPurchasable,
            IsManufacturable = request.IsManufacturable
        };

        var created = await _productRepository.CreateAsync(product, ct);
        return Response<Guid>.Success(created.Id);
    }
}
