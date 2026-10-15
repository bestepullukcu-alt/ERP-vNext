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

        // WP-MDM-BRAND-ACCEPT-01 FIX3 (R-3) — an edit says which read it was made on, as for a brand; an older one writes
        // nothing (409), so two concurrent edits can never both land and orphan a reservation.
        if (r.ExpectedVersion is not { } expectedVersion || expectedVersion < 0)
        {
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.ExpectedVersionRequired,
                "ExpectedVersion must be a non-negative integer.", 400);
        }

        if (entity.Version != expectedVersion)
        {
            return Stale();
        }

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

        var storedReferences = entity.ExternalReferences.ToList();
        ProductMappings.Apply(entity, r);
        entity.UpdatedBy = request.Actor;
        // WP-MDM-BRAND-ACCEPT-01 FIX1 (item 10) — where a reference came from is never re-stamped by an edit, and a
        // reference another product already carries is named.
        BrandProductExternalReferences.PreserveImportedAt(entity.ExternalReferences, storedReferences);
        // FIX2 (R4, item 5) — only references this edit ADDS are checked: a pair the product already carried is not
        // re-litigated (legacy data may have left two products sharing one), and only those are reserved.
        var (added, removed) = BrandProductExternalReferences.Diff(entity.ExternalReferences, storedReferences);
        if (await FirstInUseAsync(added, entity.Id, cancellationToken) is { } inUse)
        {
            return BrandProductFailures.Fail<NoContent>(BrandProductReasonCodes.ExternalReferenceInUse, inUse.Message, inUse.StatusCode);
        }

        return await _productRepository.UpdateWithReservationsAsync(entity, expectedVersion, added, removed, cancellationToken) switch
        {
            BrandWriteOutcome.Written => Response<NoContent>.SuccessWithoutData(204),
            BrandWriteOutcome.Ambiguous => BrandProductFailures.OutcomeUnknown<NoContent>(),
            BrandWriteOutcome.Stale => await LostAsync(request.ProductId, cancellationToken),
            BrandWriteOutcome.ReferenceInUse => BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.ExternalReferenceInUse,
                (await FirstInUseAsync(added, entity.Id, cancellationToken))?.Message
                ?? "One of the external references is already used by another product.",
                409),
            BrandWriteOutcome.Unavailable => BrandProductFailures.StoreUnavailable<NoContent>(),
            _ => BrandProductFailures.Fail<NoContent>(BrandProductReasonCodes.ProductNotFound, "Product not found.", 404)
        };
    }

    // The conditional write lost: gone stays 404, archived meanwhile is record_archived, otherwise changed meanwhile (409).
    private async Task<Response<NoContent>> LostAsync(Guid productId, CancellationToken cancellationToken)
    {
        var current = await _productRepository.GetByIdAsync(productId, cancellationToken);
        if (current is null)
        {
            return BrandProductFailures.Fail<NoContent>(BrandProductReasonCodes.ProductNotFound, "Product not found.", 404);
        }

        return current.IsArchived
            ? BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.RecordArchived,
                "Archived products are read-only. Historical references stay intact.", 409)
            : Stale();
    }

    private static Response<NoContent> Stale() => BrandProductFailures.Fail<NoContent>(
        BrandProductReasonCodes.VersionConflict,
        "The product has changed since it was loaded. Reload it and apply the edit again.", 409);

    private Task<(string Message, int StatusCode)?> FirstInUseAsync(
        IReadOnlyList<Domain.Entities.BrandProductExternalReference> references, Guid productId, CancellationToken cancellationToken)
        => BrandProductExternalReferences.FirstInUseAsync(references, async (source, externalId) =>
            (await _productRepository.FindByExternalReferenceAsync(source, externalId, productId, cancellationToken))?.ProductCode);

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

        // A brand that stopped being linkable (archived, inactive, expired) stays attached to products that were linked
        // before — that history must not break. Only MOVING a product onto such a brand is refused (FIX2 item 2).
        return requestedBrandId == currentBrandId ? null : ProductBrandLink.Refusal(brand, DateTimeOffset.UtcNow);
    }
}
