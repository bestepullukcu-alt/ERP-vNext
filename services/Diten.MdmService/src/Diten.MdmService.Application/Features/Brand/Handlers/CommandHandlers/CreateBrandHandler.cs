using Diten.MdmService.Application.Features.BrandProductContract;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.Vocabulary;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Handlers.CommandHandlers;

public sealed class CreateBrandHandler : IRequestHandler<Commands.CreateBrandCommand, Response<Guid>>
{
    private readonly IBrandRepository _repository;

    public CreateBrandHandler(IBrandRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<Guid>> Handle(Commands.CreateBrandCommand request, CancellationToken cancellationToken)
    {
        var r = request.Request;

        // `archived` is reachable only through the archive endpoint — never through a write payload.
        if (BrandProductVocabulary.IsArchivedStatus(r.BrandStatus))
        {
            return BrandProductFailures.Fail<Guid>(
                BrandProductReasonCodes.ArchivedStatusNotAssignable,
                "Brand status 'archived' is set by the archive endpoint, not by a write request.", 400);
        }

        if (!BrandProductVocabulary.IsBrandStatus(r.BrandStatus))
        {
            return BrandProductFailures.Fail<Guid>(
                BrandProductReasonCodes.InvalidBrandStatus,
                $"Unknown brand status '{r.BrandStatus}'.", 400);
        }

        if (!BrandProductEffectiveWindow.IsValid(r.EffectiveFrom, r.EffectiveTo))
        {
            return BrandProductFailures.Fail<Guid>(
                BrandProductReasonCodes.InvalidEffectiveWindow,
                "EffectiveTo cannot be earlier than EffectiveFrom.", 400);
        }

        if (BrandProductExternalReferences.Validate(r.ExternalReferences) is { } externalReferenceFailure)
        {
            return BrandProductFailures.Fail<Guid>(
                externalReferenceFailure,
                "External references must be unique per (SourceSystem, ExternalId) with at most one primary per source system.",
                409);
        }

        var code = BrandMappings.NormalizeCode(r.BrandCode);
        if (await _repository.ExistsByCodeAsync(code, cancellationToken: cancellationToken))
        {
            return Duplicate();
        }

        var entity = new Domain.Entities.Brand
        {
            BrandCode = code,
            CreatedBy = request.Actor,
            UpdatedBy = request.Actor
        };
        BrandMappings.Apply(entity, r);

        // FIX1 (item 10) — a reference another brand already carries is named, never silently shared.
        if (await BrandProductExternalReferences.FirstInUseAsync(entity.ExternalReferences, async (source, externalId) =>
                (await _repository.FindByExternalReferenceAsync(source, externalId, null, cancellationToken))?.BrandCode)
            is { } inUse)
        {
            return BrandProductFailures.Fail<Guid>(BrandProductReasonCodes.ExternalReferenceInUse, inUse.Message, inUse.StatusCode);
        }

        // FIX1 (items 1, 9) — the brand and its history entry land together; a concurrent create of the same code that
        // passed the pre-check above is answered by the unique index as the same 409, never a 500.
        var outcome = await _repository.CreateWithHistoryAsync(
            entity,
            BrandHistory.Entry(Domain.Entities.BrandHistoryEntry.EventCreated, request.Actor, request.CorrelationId, (Domain.Entities.BrandAuditSnapshot?)null, entity),
            cancellationToken);
        return outcome == BrandWriteOutcome.Written ? Response<Guid>.Success(entity.Id, 201) : Duplicate();

        static Response<Guid> Duplicate() => BrandProductFailures.Fail<Guid>(
            BrandProductReasonCodes.BrandCodeDuplicate,
            "A brand with this code already exists in this tenant.", 409);
    }
}
