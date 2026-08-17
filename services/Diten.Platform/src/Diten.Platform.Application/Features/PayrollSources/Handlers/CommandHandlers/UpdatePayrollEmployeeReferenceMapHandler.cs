using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollSources.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.CommandHandlers;

public sealed class UpdatePayrollEmployeeReferenceMapHandler : IRequestHandler<UpdatePayrollEmployeeReferenceMapCommand, Response<NoContent>>
{
    private readonly IPayrollSourceRepository _repository;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly IPositionRepository _positions;

    public UpdatePayrollEmployeeReferenceMapHandler(
        IPayrollSourceRepository repository,
        IOrganizationUnitRepository organizationUnits,
        IPositionRepository positions)
    {
        _repository = repository;
        _organizationUnits = organizationUnits;
        _positions = positions;
    }

    public async Task<Response<NoContent>> Handle(UpdatePayrollEmployeeReferenceMapCommand request, CancellationToken ct)
    {
        if (await _repository.GetExternalSystemProfileByIdAsync(request.SourceProfileId, ct) == null)
        {
            return Response<NoContent>.Fail("Payroll source not found.", 404);
        }

        var map = await _repository.GetEmployeeReferenceMapByIdAsync(request.SourceProfileId, request.MapId, ct);
        if (map == null)
        {
            return Response<NoContent>.Fail("Payroll employee reference map not found.", 404);
        }

        var referenceValidation = await PayrollMod0288ReferenceGuard.ValidateAsync(request.Request, _organizationUnits, _positions, ct);
        if (!referenceValidation.IsSuccessful)
        {
            return Response<NoContent>.Fail(referenceValidation.Errors, referenceValidation.StatusCode);
        }

        map.ExternalEmployeeReference = request.Request.ExternalEmployeeReference.Trim();
        map.PersonReferenceId = request.Request.PersonReferenceId;
        map.OrganizationUnitReferenceId = request.Request.OrganizationUnitReferenceId;
        map.PositionReferenceId = request.Request.PositionReferenceId;
        map.MappingState = request.Request.MappingState;
        map.LastValidatedAt = request.Request.MappingState == PayrollReferenceMappingState.Mapped ? DateTimeOffset.UtcNow : null;

        await _repository.UpdateEmployeeReferenceMapAsync(map, ct);
        return Response<NoContent>.Success(204);
    }
}
