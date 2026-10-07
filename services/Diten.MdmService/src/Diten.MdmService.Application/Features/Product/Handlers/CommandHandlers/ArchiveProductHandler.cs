using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.Vocabulary;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Product.Handlers.CommandHandlers;

public sealed class ArchiveProductHandler : IRequestHandler<Commands.ArchiveProductCommand, Response<NoContent>>
{
    private readonly IProductRepository _repository;

    public ArchiveProductHandler(IProductRepository repository)
    {
        _repository = repository;
    }

    /// <summary>How many times the archive re-reads a product that an edit changed between the read and the write.</summary>
    private const int MaxAttempts = 3;

    public async Task<Response<NoContent>> Handle(Commands.ArchiveProductCommand request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var entity = await _repository.GetByIdAsync(request.ProductId, cancellationToken);
            if (entity is null)
            {
                return BrandProductFailures.Fail<NoContent>(
                    BrandProductReasonCodes.ProductNotFound, "Product not found.", 404);
            }

            // Idempotent, same as the brand archive path.
            if (entity.IsArchived)
            {
                return Response<NoContent>.SuccessWithoutData(204);
            }

            // Soft only. Campaign / Knowledge / Frequency records that reference this product are untouched
            // (FU01 §11) — this feature never reaches into a consumer aggregate.
            entity.ProductStatus = BrandProductVocabulary.StatusArchived;
            entity.ArchivedAt = DateTimeOffset.UtcNow;
            entity.ArchivedBy = request.Actor;
            entity.UpdatedBy = request.Actor;

            // WP-MDM-BRAND-ACCEPT-01 FIX3 (R-3) — only the archive fields, only on the version read here, never the generic
            // whole-document UpdateAsync: an archive racing an edit can no longer overwrite the edit (or be overwritten).
            // The product's reservations are released with it (M-3).
            switch (await _repository.ArchiveWithReservationsAsync(entity, entity.Version, cancellationToken))
            {
                case BrandWriteOutcome.Written:
                    return Response<NoContent>.SuccessWithoutData(204);
                case BrandWriteOutcome.Unavailable:
                    return BrandProductFailures.StoreUnavailable<NoContent>();
                case BrandWriteOutcome.Ambiguous:
                    return BrandProductFailures.OutcomeUnknown<NoContent>();
            }

            if (attempt == MaxAttempts)
            {
                return BrandProductFailures.Fail<NoContent>(
                    BrandProductReasonCodes.VersionConflict,
                    "The product kept changing while it was being archived. Reload it and try again.", 409);
            }
        }
    }
}
