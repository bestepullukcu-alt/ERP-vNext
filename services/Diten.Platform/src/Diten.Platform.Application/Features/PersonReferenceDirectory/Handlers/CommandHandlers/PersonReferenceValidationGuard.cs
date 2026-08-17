using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Entities.PersonReferenceDirectory;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory.Handlers.CommandHandlers;

internal sealed class PersonReferenceValidationGuard
{
    private readonly IHrisSourceRepository _hrisSourceRepository;
    private readonly IOrganizationUnitRepository _organizationUnitRepository;
    private readonly IPositionRepository _positionRepository;
    private readonly IPersonReferenceDirectoryRepository _personReferenceRepository;

    public PersonReferenceValidationGuard(
        IHrisSourceRepository hrisSourceRepository,
        IOrganizationUnitRepository organizationUnitRepository,
        IPositionRepository positionRepository,
        IPersonReferenceDirectoryRepository personReferenceRepository)
    {
        _hrisSourceRepository = hrisSourceRepository;
        _organizationUnitRepository = organizationUnitRepository;
        _positionRepository = positionRepository;
        _personReferenceRepository = personReferenceRepository;
    }

    public async Task<Response<NoContent>> ValidateReferencesAsync(
        Guid hrisSourceProfileId,
        Guid? organizationUnitId,
        Guid? positionId,
        CancellationToken ct)
    {
        var source = await _hrisSourceRepository.GetSourceProfileByIdAsync(hrisSourceProfileId, ct);
        if (source == null)
        {
            return Response<NoContent>.Fail("HRIS source profile not found for current tenant.", 404);
        }

        if (organizationUnitId.HasValue)
        {
            var organizationUnit = await _organizationUnitRepository.GetByIdAsync(organizationUnitId.Value, ct);
            if (organizationUnit == null || organizationUnit.IsArchived)
            {
                return Response<NoContent>.Fail("Organization Unit not found for current tenant.", 404);
            }
        }

        if (positionId.HasValue)
        {
            var position = await _positionRepository.GetByIdAsync(positionId.Value, ct);
            if (position == null || position.IsArchived)
            {
                return Response<NoContent>.Fail("Position not found for current tenant.", 404);
            }

            if (organizationUnitId.HasValue && position.OrganizationUnitId != organizationUnitId.Value)
            {
                return Response<NoContent>.Fail("Position does not belong to the provided Organization Unit.", 409);
            }
        }

        return Response<NoContent>.Success(204);
    }

    public async Task<Response<PersonReferenceExternalCorrelation>> RequireValidatedCorrelationAsync(
        PersonReferenceProjection projection,
        CancellationToken ct)
    {
        if (!projection.PrimaryExternalCorrelationId.HasValue)
        {
            return Response<PersonReferenceExternalCorrelation>.Fail("Validated person reference requires an active HRIS external correlation.", 409);
        }

        var correlation = await _personReferenceRepository.GetCorrelationByIdAsync(projection.PrimaryExternalCorrelationId.Value, ct);
        if (correlation == null
            || correlation.PersonReferenceProjectionId != projection.Id
            || correlation.HrisSourceProfileId != projection.HrisSourceProfileId
            || correlation.CorrelationState != PersonReferenceCorrelationState.Validated)
        {
            return Response<PersonReferenceExternalCorrelation>.Fail("Validated person reference requires an active validated same-tenant HRIS external correlation.", 409);
        }

        return Response<PersonReferenceExternalCorrelation>.Success(correlation);
    }
}
