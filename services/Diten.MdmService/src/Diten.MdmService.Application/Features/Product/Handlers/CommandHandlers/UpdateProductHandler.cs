using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Product.Handlers.CommandHandlers;

public sealed class UpdateProductHandler : IRequestHandler<Commands.UpdateProductCommand, Response<NoContent>>
{
    private readonly IProductRepository _productRepository;
    private readonly IBrandRepository _brandRepository;

    public UpdateProductHandler(IProductRepository productRepository, IBrandRepository brandRepository)
    {
        _productRepository = productRepository;
        _brandRepository = brandRepository;
    }

    public async Task<Response<NoContent>> Handle(Commands.UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var entity = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (entity is null)
        {
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.ProductNotFound, "Product not found.", 404);
        }

        if (entity.IsArchived)
        {
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.RecordArchived,
                "Archived products are read-only. Historical references stay intact.", 409);
        }

        var r = request.Request;

        if (ProductWriteRules.Validate(r) is { } failure)
        {
            return BrandProductFailures.Fail<NoContent>(failure.ReasonCode, failure.Message, failure.StatusCode);
        }

        // ProductCode is stable (FU01 §4): a changed code is refused, never silently ignored.
        if (!string.Equals(ProductMappings.NormalizeCode(r.ProductCode), entity.ProductCode, StringComparison.Ordinal))
        {
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.CodeImmutable,
                "ProductCode is immutable. Rename the product through ProductName instead.", 409);
        }

        // Re-checked on every update: a brand archived after the product was created must block re-linking too.
        if (await ResolveBrandFailureAsync(r.BrandId, entity.BrandId, cancellationToken) is { } brandFailure)
        {
            return BrandProductFailures.Fail<NoContent>(brandFailure.ReasonCode, brandFailure.Message, brandFailure.StatusCode);
        }

        ProductMappings.Apply(entity, r);
        entity.UpdatedBy = request.Actor;

        var updated = await _productRepository.UpdateAsync(entity, cancellationToken);
        return updated
            ? Response<NoContent>.SuccessWithoutData(204)
            : BrandProductFailures.Fail<NoContent>(BrandProductReasonCodes.ProductNotFound, "Product not found.", 404);
    }

    private async Task<(string ReasonCode, string Message, int StatusCode)?> ResolveBrandFailureAsync(
        Guid? requestedBrandId, Guid? currentBrandId, CancellationToken cancellationToken)
    {
        if (requestedBrandId is not { } id || id == Guid.Empty)
        {
            return null;
        }

        var brand = await _brandRepository.GetByIdAsync(id, cancellationToken);
        if (brand is null)
        {
            return (BrandProductReasonCodes.BrandNotFound, "Brand not found in this tenant.", 404);
        }

        // An already-archived brand stays attached to products that were linked before it was archived — that
        // history must not break. Only MOVING a product onto an archived brand is refused.
        if (!brand.IsLinkable && requestedBrandId != currentBrandId)
        {
            return (BrandProductReasonCodes.BrandArchived, "An archived brand cannot receive new product links.", 409);
        }

        return null;
    }
}
