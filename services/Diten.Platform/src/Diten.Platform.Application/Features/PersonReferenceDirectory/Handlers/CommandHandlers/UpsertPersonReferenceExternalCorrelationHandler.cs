using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;
using Diten.Platform.Domain.Entities.PersonReferenceDirectory;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Handlers.CommandHandlers;

public sealed class UpsertPersonReferenceExternalCorrelationHandler : IRequestHandler<UpsertPersonReferenceExternalCorrelationCommand, Response<Guid>>
{
    private readonly IPersonReferenceDirectoryRepository _repository;
    private readonly PersonReferenceValidationGuard _validationGuard;

    public UpsertPersonReferenceExternalCorrelationHandler(
        IPersonReferenceDirectoryRepository repository,
        IHrisSourceRepository hrisSourceRepository,
        IOrganizationUnitRepository organizationUnitRepository,
        IPositionRepository positionRepository)
    {
        _repository = repository;
        _validationGuard = new PersonReferenceValidationGuard(hrisSourceRepository, organizationUnitRepository, positionRepository, repository);
    }

    public async Task<Response<Guid>> Handle(UpsertPersonReferenceExternalCorrelationCommand request, CancellationToken ct)
    {
        var projection = await _repository.GetProjectionByIdAsync(request.ProjectionId, ct);
        if (projection == null)
        {
            return Response<Guid>.Fail("Person reference not found.", 404);
        }

        var referenceValidation = await _validationGuard.ValidateReferencesAsync(
            projection.HrisSourceProfileId,
            projection.OrganizationUnitId,
            projection.PositionId,
            ct);
        if (!referenceValidation.IsSuccessful)
        {
            return Response<Guid>.Fail(referenceValidation.Errors, referenceValidation.StatusCode);
        }

        var correlationKey = request.Request.CorrelationKey.Trim();
        if (await _repository.ExistsActiveCorrelationKeyAsync(correlationKey, projection.HrisSourceProfileId, request.CorrelationId, ct))
        {
            return Response<Guid>.Fail("Person reference correlation key already exists.", 409);
        }

        var entity = request.CorrelationId.HasValue
            ? await _repository.GetCorrelationByIdAsync(request.CorrelationId.Value, ct)
            : null;

        entity ??= new PersonReferenceExternalCorrelation
        {
            TenantId = projection.TenantId,
            PersonReferenceProjectionId = projection.Id,
            HrisSourceProfileId = projection.HrisSourceProfileId,
            ExternalObjectReference = string.Empty,
            CorrelationKey = string.Empty,
            SourceContractVersion = string.Empty
        };

        if (entity.PersonReferenceProjectionId != projection.Id)
        {
            return Response<Guid>.Fail("Person reference correlation not found.", 404);
        }

        entity.HrisSourceProfileId = projection.HrisSourceProfileId;
        entity.ExternalObjectType = request.Request.ExternalObjectType;
        entity.ExternalObjectReference = request.Request.ExternalObjectReference.Trim();
        entity.CorrelationKey = correlationKey;
        entity.CorrelationState = request.Request.CorrelationState;
        entity.SourceContractVersion = request.Request.SourceContractVersion.Trim();
        entity.LastSeenAt = request.Request.LastSeenAt;

        await _repository.UpsertCorrelationAsync(entity, ct);

        if (entity.CorrelationState == PersonReferenceCorrelationState.Validated)
        {
            projection.PrimaryExternalCorrelationId = entity.Id;
            await _repository.UpdateProjectionAsync(projection, ct);
        }

        return Response<Guid>.Success(entity.Id, request.CorrelationId.HasValue ? 200 : 201);
    }
}
