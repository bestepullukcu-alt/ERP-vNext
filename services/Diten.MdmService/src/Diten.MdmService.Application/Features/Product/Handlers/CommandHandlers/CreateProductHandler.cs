using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Product.Handlers.CommandHandlers;

public sealed class CreateProductHandler : IRequestHandler<Commands.CreateProductCommand, Response<Guid>>
{
    private readonly IProductRepository _productRepository;
    private readonly IBrandRepository _brandRepository;

    public CreateProductHandler(IProductRepository productRepository, IBrandRepository brandRepository)
    {
        _productRepository = productRepository;
        _brandRepository = brandRepository;
    }

    public async Task<Response<Guid>> Handle(Commands.CreateProductCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;

        if (ProductWriteRules.Validate(r) is { } failure)
        {
            return BrandProductFailures.Fail<Guid>(failure.ReasonCode, failure.Message, failure.StatusCode);
        }

        if (await ResolveBrandFailureAsync(r.BrandId, cancellationToken) is { } brandFailure)
        {
            return BrandProductFailures.Fail<Guid>(brandFailure.ReasonCode, brandFailure.Message, brandFailure.StatusCode);
        }

        var code = ProductMappings.NormalizeCode(r.ProductCode);
        if (await _productRepository.ExistsByCodeAsync(code, cancellationToken: cancellationToken))
        {
            return BrandProductFailures.Fail<Guid>(
                BrandProductReasonCodes.ProductCodeDuplicate,
                "A product with this code already exists in this tenant.", 409);
        }

        var entity = new Domain.Entities.Product
        {
            ProductCode = code,
            CreatedBy = request.Actor,
            UpdatedBy = request.Actor
        };
        ProductMappings.Apply(entity, r);

        var created = await _productRepository.CreateAsync(entity, cancellationToken);
        return Response<Guid>.Success(created.Id, 201);
    }

    /// <summary>
    /// BrandId is optional (FU01 §4.1) — a null/empty brand is a valid, fully supported product. When a brand IS
    /// supplied it must live in the same tenant (otherwise 404: a foreign brand is indistinguishable from a
    /// missing one) and must not be archived (409, FU01 §11).
    /// </summary>
    private async Task<(string ReasonCode, string Message, int StatusCode)?> ResolveBrandFailureAsync(
        Guid? brandId, CancellationToken cancellationToken)
    {
        if (brandId is not { } id || id == Guid.Empty)
        {
            return null;
        }

        var brand = await _brandRepository.GetByIdAsync(id, cancellationToken);
        if (brand is null)
        {
            return (BrandProductReasonCodes.BrandNotFound, "Brand not found in this tenant.", 404);
        }

        return brand.IsLinkable
            ? null
            : (BrandProductReasonCodes.BrandArchived, "An archived brand cannot receive new product links.", 409);
    }
}
