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
            return BrandProductFailures.Fail<Guid>(
                BrandProductReasonCodes.BrandCodeDuplicate,
                "A brand with this code already exists in this tenant.", 409);
        }

        var entity = new Domain.Entities.Brand
        {
            BrandCode = code,
            CreatedBy = request.Actor,
            UpdatedBy = request.Actor
        };
        BrandMappings.Apply(entity, r);

        var created = await _repository.CreateAsync(entity, cancellationToken);
        return Response<Guid>.Success(created.Id, 201);
    }
}
