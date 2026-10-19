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
            return Duplicate();
        }

        var entity = new Domain.Entities.Product
        {
            ProductCode = code,
            CreatedBy = request.Actor,
            UpdatedBy = request.Actor
        };
        ProductMappings.Apply(entity, r);

        // WP-MDM-BRAND-ACCEPT-01 FIX1 (item 10) — a reference another product already carries is named, never shared.
        if (await BrandProductExternalReferences.FirstInUseAsync(entity.ExternalReferences, async (source, externalId) =>
                (await _productRepository.FindByExternalReferenceAsync(source, externalId, null, cancellationToken))?.ProductCode)
            is { } inUse)
        {
            return BrandProductFailures.Fail<Guid>(BrandProductReasonCodes.ExternalReferenceInUse, inUse.Message, inUse.StatusCode);
        }

        // FIX1 (item 9) — a concurrent create of the same code that passed the pre-check is the same 409, never a 500.
        // FIX2 (item 6) — the references are reserved in the same transaction, so of two concurrent creates carrying one
        // pair exactly one is written.
        return await _productRepository.CreateWithReservationsAsync(entity, cancellationToken) switch
        {
            BrandWriteOutcome.Written => Response<Guid>.Success(entity.Id, 201),
            BrandWriteOutcome.ReferenceInUse => BrandProductFailures.Fail<Guid>(
                BrandProductReasonCodes.ExternalReferenceInUse,
                (await BrandProductExternalReferences.FirstInUseAsync(entity.ExternalReferences, async (source, externalId) =>
                    (await _productRepository.FindByExternalReferenceAsync(source, externalId, null, cancellationToken))?.ProductCode))?.Message
                ?? "One of the external references is already used by another product.",
                409),
            BrandWriteOutcome.Unavailable => BrandProductFailures.StoreUnavailable<Guid>(),
            BrandWriteOutcome.Ambiguous => BrandProductFailures.OutcomeUnknown<Guid>(),
            _ => Duplicate()
        };

        static Response<Guid> Duplicate() => BrandProductFailures.Fail<Guid>(
            BrandProductReasonCodes.ProductCodeDuplicate,
            "A product with this code already exists in this tenant.", 409);
    }

    /// <summary>
    /// BrandId is optional (FU01 §4.1) — a null/empty brand is a valid, fully supported product. When a brand IS
    /// supplied it must live in the same tenant (otherwise 404: a foreign brand is indistinguishable from a
    /// missing one) and must be linkable (409): not archived (FU01 §11), and — WP-MDM-BRAND-ACCEPT-01 FIX2 item 2 —
    /// active and inside its validity window today.
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

        return ProductBrandLink.Refusal(brand, DateTimeOffset.UtcNow);
    }
}
