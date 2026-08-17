using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using Diten.Platform.Domain.Entities.TimeAttendanceProviders;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.CommandHandlers;

public sealed class CreateTimeAttendanceEmployeeReferenceMapHandler : IRequestHandler<CreateTimeAttendanceEmployeeReferenceMapCommand, Response<Guid>>
{
    private readonly ITimeAttendanceProviderRepository _repository;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly IPositionRepository _positions;

    public CreateTimeAttendanceEmployeeReferenceMapHandler(
        ITimeAttendanceProviderRepository repository,
        IOrganizationUnitRepository organizationUnits,
        IPositionRepository positions)
    {
        _repository = repository;
        _organizationUnits = organizationUnits;
        _positions = positions;
    }

    public async Task<Response<Guid>> Handle(CreateTimeAttendanceEmployeeReferenceMapCommand request, CancellationToken ct)
    {
        var profile = await _repository.GetProviderProfileByIdAsync(request.ProviderProfileId, ct);
        if (profile == null)
        {
            return Response<Guid>.Fail("Time-attendance provider not found.", 404);
        }

        var referenceValidation = await TimeAttendanceReferenceGuard.ValidateAsync(request.Request, _organizationUnits, _positions, ct);
        if (!referenceValidation.IsSuccessful)
        {
            return Response<Guid>.Fail(referenceValidation.Errors, referenceValidation.StatusCode);
        }

        var map = new TimeAttendanceEmployeeReferenceMap
        {
            TenantId = profile.TenantId,
            ProviderProfileId = profile.Id,
            ExternalEmployeeReference = request.Request.ExternalEmployeeReference.Trim(),
            HrisReferenceId = request.Request.HrisReferenceId,
            PersonReferenceId = request.Request.PersonReferenceId,
            OrganizationUnitReferenceId = request.Request.OrganizationUnitReferenceId,
            PositionReferenceId = request.Request.PositionReferenceId,
            MappingState = request.Request.MappingState,
            LastValidatedAt = request.Request.MappingState == TimeAttendanceReferenceMappingState.Mapped ? DateTimeOffset.UtcNow : null
        };

        await _repository.CreateEmployeeReferenceMapAsync(map, ct);
        return Response<Guid>.Success(map.Id, 201);
    }
}
