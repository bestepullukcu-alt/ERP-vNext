using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.Vocabulary;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Handlers.CommandHandlers;

public sealed class UpdateBrandHandler : IRequestHandler<Commands.UpdateBrandCommand, Response<NoContent>>
{
    private readonly IBrandRepository _repository;

    public UpdateBrandHandler(IBrandRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<NoContent>> Handle(Commands.UpdateBrandCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.BrandId, cancellationToken);
        if (entity is null)
        {
            // Cross-tenant reads are already filtered out by the repository, so "not mine" and "not there"
            // both surface as 404 — existence is never leaked across tenants.
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.BrandNotFound, "Brand not found.", 404);
        }

        if (entity.IsArchived)
        {
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.RecordArchived,
                "Archived brands are read-only. Historical references stay intact.", 409);
        }

        var r = request.Request;

        if (BrandProductVocabulary.IsArchivedStatus(r.BrandStatus))
        {
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.ArchivedStatusNotAssignable,
                "Brand status 'archived' is set by the archive endpoint, not by a write request.", 400);
        }

        if (!BrandProductVocabulary.IsBrandStatus(r.BrandStatus))
        {
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.InvalidBrandStatus,
                $"Unknown brand status '{r.BrandStatus}'.", 400);
        }

        // BrandCode is stable (FU01 §3): a changed code is rejected rather than silently ignored, so a caller
        // never believes a rename succeeded.
        if (!string.Equals(BrandMappings.NormalizeCode(r.BrandCode), entity.BrandCode, StringComparison.Ordinal))
        {
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.CodeImmutable,
                "BrandCode is immutable. Rename the brand through BrandName instead.", 409);
        }

        if (!BrandProductEffectiveWindow.IsValid(r.EffectiveFrom, r.EffectiveTo))
        {
            return BrandProductFailures.Fail<NoContent>(
                BrandProductReasonCodes.InvalidEffectiveWindow,
                "EffectiveTo cannot be earlier than EffectiveFrom.", 400);
        }

        if (BrandProductExternalReferences.Validate(r.ExternalReferences) is { } externalReferenceFailure)
        {
            return BrandProductFailures.Fail<NoContent>(
                externalReferenceFailure,
                "External references must be unique per (SourceSystem, ExternalId) with at most one primary per source system.",
                409);
        }

        BrandMappings.Apply(entity, r);
        entity.UpdatedBy = request.Actor;

        var updated = await _repository.UpdateAsync(entity, cancellationToken);
        return updated
            ? Response<NoContent>.SuccessWithoutData(204)
            : BrandProductFailures.Fail<NoContent>(BrandProductReasonCodes.BrandNotFound, "Brand not found.", 404);
    }
}
