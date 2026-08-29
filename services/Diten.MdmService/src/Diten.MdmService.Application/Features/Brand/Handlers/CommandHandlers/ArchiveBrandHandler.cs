using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.Vocabulary;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Handlers.CommandHandlers;

public sealed class ArchiveBrandHandler : IRequestHandler<Commands.ArchiveBrandCommand, Response<NoContent>>
{
    private readonly IBrandRepository _repository;

    public ArchiveBrandHandler(IBrandRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(Commands.ArchiveBrandCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.BrandId, cancellationToken);
        if (entity is null)
        {
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.BrandNotFound, "Brand not found.", 404);
        }

        // Idempotent: re-archiving an archived brand succeeds instead of 409-ing, so a retried request or a
        // double-clicked button is not reported as a failure.
        if (entity.IsArchived)
        {
            return Response<NoContent>.SuccessWithoutData(204);
        }

        // NO CASCADE (FU01 §11): products under this brand keep their own lifecycle and stay readable. Only
        // NEW product links are refused from here on, and that refusal is visible (409), never silent.
        entity.IsArchived = true;
        entity.BrandStatus = BrandProductVocabulary.StatusArchived;
        entity.ArchivedAt = DateTimeOffset.UtcNow;
        entity.ArchivedBy = request.Actor;
        entity.UpdatedBy = request.Actor;

        var updated = await _repository.UpdateAsync(entity, cancellationToken);
        return updated
            ? Response<NoContent>.SuccessWithoutData(204)
            : BrandProductFailures.Fail<NoContent>(BrandProductReasonCodes.BrandNotFound, "Brand not found.", 404);
    }
}
