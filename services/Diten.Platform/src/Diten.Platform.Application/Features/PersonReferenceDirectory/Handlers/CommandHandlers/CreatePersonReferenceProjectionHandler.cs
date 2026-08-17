using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PersonReferenceDirectory.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.PersonReferenceDirectory;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Handlers.CommandHandlers;

public sealed class CreatePersonReferenceProjectionHandler : IRequestHandler<CreatePersonReferenceProjectionCommand, Response<Guid>>
{
    private readonly IPersonReferenceDirectoryRepository _repository;
    private readonly PersonReferenceValidationGuard _validationGuard;
    private readonly ITenantContext _tenantContext;

    public CreatePersonReferenceProjectionHandler(
        IPersonReferenceDirectoryRepository repository,
        IHrisSourceRepository hrisSourceRepository,
        IOrganizationUnitRepository organizationUnitRepository,
        IPositionRepository positionRepository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _validationGuard = new PersonReferenceValidationGuard(hrisSourceRepository, organizationUnitRepository, positionRepository, repository);
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreatePersonReferenceProjectionCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var canonicalCode = PersonReferenceDirectoryCodeNormalizer.Normalize(request.Request.Code);
        if (string.IsNullOrWhiteSpace(canonicalCode))
        {
            return Response<Guid>.Fail("Person reference code is required.", 400);
        }

        if (await _repository.ExistsActiveCodeAsync(canonicalCode, null, ct))
        {
            return Response<Guid>.Fail("Person reference code already exists.", 409);
        }

        var referenceValidation = await _validationGuard.ValidateReferencesAsync(
            request.Request.HrisSourceProfileId,
            request.Request.OrganizationUnitId,
            request.Request.PositionId,
            ct);
        if (!referenceValidation.IsSuccessful)
        {
            return Response<Guid>.Fail(referenceValidation.Errors, referenceValidation.StatusCode);
        }

        if (request.Request.ReferenceState == PersonReferenceState.Validated)
        {
            return Response<Guid>.Fail("New person reference cannot be created as Validated without an active HRIS external correlation.", 409);
        }

        var entity = new PersonReferenceProjection
        {
            TenantId = tenantId,
            Code = canonicalCode,
            ReferenceDisplayName = request.Request.ReferenceDisplayName.Trim(),
            HrisSourceProfileId = request.Request.HrisSourceProfileId,
            OrganizationUnitId = request.Request.OrganizationUnitId,
            PositionId = request.Request.PositionId,
            ReferenceState = request.Request.ReferenceState,
            SourceContractVersion = request.Request.SourceContractVersion.Trim(),
            CorrelationKey = request.Request.CorrelationKey.Trim(),
            ValidationFailureReason = PersonReferenceDirectoryCodeNormalizer.NormalizeOptional(request.Request.ValidationFailureReason)
        };

        await _repository.CreateProjectionAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
