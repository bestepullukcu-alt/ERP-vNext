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

    public async Task<Response<NoContent>> Handle(Commands.ArchiveProductCommand request, CancellationToken cancellationToken)
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
        entity.IsArchived = true;
        entity.ProductStatus = BrandProductVocabulary.StatusArchived;
        entity.ArchivedAt = DateTimeOffset.UtcNow;
        entity.ArchivedBy = request.Actor;
        entity.UpdatedBy = request.Actor;

        var updated = await _repository.UpdateAsync(entity, cancellationToken);
        return updated
            ? Response<NoContent>.SuccessWithoutData(204)
            : BrandProductFailures.Fail<NoContent>(BrandProductReasonCodes.ProductNotFound, "Product not found.", 404);
    }
}
