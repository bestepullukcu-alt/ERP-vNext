using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Handlers.CommandHandlers;

public sealed class UpdatePersonReferenceProjectionHandler : IRequestHandler<UpdatePersonReferenceProjectionCommand, Response<NoContent>>
{
    private readonly IPersonReferenceDirectoryRepository _repository;
    private readonly PersonReferenceValidationGuard _validationGuard;

    public UpdatePersonReferenceProjectionHandler(
        IPersonReferenceDirectoryRepository repository,
        IHrisSourceRepository hrisSourceRepository,
        IOrganizationUnitRepository organizationUnitRepository,
        IPositionRepository positionRepository)
    {
        _repository = repository;
        _validationGuard = new PersonReferenceValidationGuard(hrisSourceRepository, organizationUnitRepository, positionRepository, repository);
    }

    public async Task<Response<NoContent>> Handle(UpdatePersonReferenceProjectionCommand request, CancellationToken ct)
    {
        var projection = await _repository.GetProjectionByIdAsync(request.Id, ct);
        if (projection == null)
        {
            return Response<NoContent>.Fail("Person reference not found.", 404);
        }

        var canonicalCode = PersonReferenceDirectoryCodeNormalizer.Normalize(request.Request.Code);
        if (await _repository.ExistsActiveCodeAsync(canonicalCode, projection.Id, ct))
        {
            return Response<NoContent>.Fail("Person reference code already exists.", 409);
        }

        var referenceValidation = await _validationGuard.ValidateReferencesAsync(
            request.Request.HrisSourceProfileId,
            request.Request.OrganizationUnitId,
            request.Request.PositionId,
            ct);
        if (!referenceValidation.IsSuccessful)
        {
            return Response<NoContent>.Fail(referenceValidation.Errors, referenceValidation.StatusCode);
        }

        projection.Code = canonicalCode;
        projection.ReferenceDisplayName = request.Request.ReferenceDisplayName.Trim();
        projection.HrisSourceProfileId = request.Request.HrisSourceProfileId;
        projection.PrimaryExternalCorrelationId = request.Request.PrimaryExternalCorrelationId;
        projection.OrganizationUnitId = request.Request.OrganizationUnitId;
        projection.PositionId = request.Request.PositionId;
        projection.SourceContractVersion = request.Request.SourceContractVersion.Trim();
        projection.CorrelationKey = request.Request.CorrelationKey.Trim();
        projection.ValidationFailureReason = PersonReferenceDirectoryCodeNormalizer.NormalizeOptional(request.Request.ValidationFailureReason);

        if (request.Request.ReferenceState == PersonReferenceState.Validated)
        {
            projection.ReferenceState = PersonReferenceState.Validated;
            var correlationValidation = await _validationGuard.RequireValidatedCorrelationAsync(projection, ct);
            if (!correlationValidation.IsSuccessful)
            {
                return Response<NoContent>.Fail(correlationValidation.Errors, correlationValidation.StatusCode);
            }

            projection.LastValidatedAt = DateTimeOffset.UtcNow;
            projection.ValidationFailureReason = null;
        }
        else
        {
            projection.ReferenceState = request.Request.ReferenceState;
            if (projection.ReferenceState != PersonReferenceState.Validated)
            {
                projection.LastValidatedAt = null;
            }
        }

        await _repository.UpdateProjectionAsync(projection, ct);
        return Response<NoContent>.Success(204);
    }
}
