using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Handlers.CommandHandlers;

public sealed class ValidatePersonReferenceProjectionHandler : IRequestHandler<ValidatePersonReferenceProjectionCommand, Response<PersonReferenceProjectionDto>>
{
    private readonly IPersonReferenceDirectoryRepository _repository;
    private readonly PersonReferenceValidationGuard _validationGuard;

    public ValidatePersonReferenceProjectionHandler(
        IPersonReferenceDirectoryRepository repository,
        IHrisSourceRepository hrisSourceRepository,
        IOrganizationUnitRepository organizationUnitRepository,
        IPositionRepository positionRepository)
    {
        _repository = repository;
        _validationGuard = new PersonReferenceValidationGuard(hrisSourceRepository, organizationUnitRepository, positionRepository, repository);
    }

    public async Task<Response<PersonReferenceProjectionDto>> Handle(ValidatePersonReferenceProjectionCommand request, CancellationToken ct)
    {
        var projection = await _repository.GetProjectionByIdAsync(request.Id, ct);
        if (projection == null)
        {
            return Response<PersonReferenceProjectionDto>.Fail("Person reference not found.", 404);
        }

        var referenceValidation = await _validationGuard.ValidateReferencesAsync(
            projection.HrisSourceProfileId,
            projection.OrganizationUnitId,
            projection.PositionId,
            ct);
        if (!referenceValidation.IsSuccessful)
        {
            return Response<PersonReferenceProjectionDto>.Fail(referenceValidation.Errors, referenceValidation.StatusCode);
        }

        var correlationValidation = await _validationGuard.RequireValidatedCorrelationAsync(projection, ct);
        if (!correlationValidation.IsSuccessful)
        {
            projection.ReferenceState = PersonReferenceState.Deferred;
            projection.ValidationFailureReason = string.Join("; ", correlationValidation.Errors);
            await _repository.UpdateProjectionAsync(projection, ct);
            return Response<PersonReferenceProjectionDto>.Fail(correlationValidation.Errors, correlationValidation.StatusCode);
        }

        projection.ReferenceState = PersonReferenceState.Validated;
        projection.LastValidatedAt = DateTimeOffset.UtcNow;
        projection.ValidationFailureReason = null;
        await _repository.UpdateProjectionAsync(projection, ct);
        return Response<PersonReferenceProjectionDto>.Success(PersonReferenceDirectoryMapper.ToDto(projection));
    }
}
