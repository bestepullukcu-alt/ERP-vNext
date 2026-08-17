using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TimeAttendanceProviders.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders.Handlers.CommandHandlers;

public sealed class UpdateTimeAttendanceEmployeeReferenceMapHandler : IRequestHandler<UpdateTimeAttendanceEmployeeReferenceMapCommand, Response<NoContent>>
{
    private readonly ITimeAttendanceProviderRepository _repository;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly IPositionRepository _positions;

    public UpdateTimeAttendanceEmployeeReferenceMapHandler(
        ITimeAttendanceProviderRepository repository,
        IOrganizationUnitRepository organizationUnits,
        IPositionRepository positions)
    {
        _repository = repository;
        _organizationUnits = organizationUnits;
        _positions = positions;
    }

    public async Task<Response<NoContent>> Handle(UpdateTimeAttendanceEmployeeReferenceMapCommand request, CancellationToken ct)
    {
        if (await _repository.GetProviderProfileByIdAsync(request.ProviderProfileId, ct) == null)
        {
            return Response<NoContent>.Fail("Time-attendance provider not found.", 404);
        }

        var map = await _repository.GetEmployeeReferenceMapByIdAsync(request.ProviderProfileId, request.MapId, ct);
        if (map == null)
        {
            return Response<NoContent>.Fail("Time-attendance employee reference map not found.", 404);
        }

        var referenceValidation = await TimeAttendanceReferenceGuard.ValidateAsync(request.Request, _organizationUnits, _positions, ct);
        if (!referenceValidation.IsSuccessful)
        {
            return Response<NoContent>.Fail(referenceValidation.Errors, referenceValidation.StatusCode);
        }

        map.ExternalEmployeeReference = request.Request.ExternalEmployeeReference.Trim();
        map.HrisReferenceId = request.Request.HrisReferenceId;
        map.PersonReferenceId = request.Request.PersonReferenceId;
        map.OrganizationUnitReferenceId = request.Request.OrganizationUnitReferenceId;
        map.PositionReferenceId = request.Request.PositionReferenceId;
        map.MappingState = request.Request.MappingState;
        map.LastValidatedAt = request.Request.MappingState == TimeAttendanceReferenceMappingState.Mapped ? DateTimeOffset.UtcNow : null;

        await _repository.UpdateEmployeeReferenceMapAsync(map, ct);
        return Response<NoContent>.Success(204);
    }
}
