using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PayrollSources.Commands;
using Diten.Platform.Domain.Entities.PayrollSources;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PayrollSources.Handlers.CommandHandlers;

public sealed class CreatePayrollEmployeeReferenceMapHandler : IRequestHandler<CreatePayrollEmployeeReferenceMapCommand, Response<Guid>>
{
    private readonly IPayrollSourceRepository _repository;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly IPositionRepository _positions;

    public CreatePayrollEmployeeReferenceMapHandler(
        IPayrollSourceRepository repository,
        IOrganizationUnitRepository organizationUnits,
        IPositionRepository positions)
    {
        _repository = repository;
        _organizationUnits = organizationUnits;
        _positions = positions;
    }

    public async Task<Response<Guid>> Handle(CreatePayrollEmployeeReferenceMapCommand request, CancellationToken ct)
    {
        var source = await _repository.GetExternalSystemProfileByIdAsync(request.SourceProfileId, ct);
        if (source == null)
        {
            return Response<Guid>.Fail("Payroll source not found.", 404);
        }

        var referenceValidation = await PayrollMod0288ReferenceGuard.ValidateAsync(request.Request, _organizationUnits, _positions, ct);
        if (!referenceValidation.IsSuccessful)
        {
            return Response<Guid>.Fail(referenceValidation.Errors, referenceValidation.StatusCode);
        }

        var map = new PayrollEmployeeReferenceMap
        {
            TenantId = source.TenantId,
            PayrollExternalSystemProfileId = source.Id,
            ExternalEmployeeReference = request.Request.ExternalEmployeeReference.Trim(),
            PersonReferenceId = request.Request.PersonReferenceId,
            OrganizationUnitReferenceId = request.Request.OrganizationUnitReferenceId,
            PositionReferenceId = request.Request.PositionReferenceId,
            MappingState = request.Request.MappingState,
            LastValidatedAt = request.Request.MappingState == PayrollReferenceMappingState.Mapped ? DateTimeOffset.UtcNow : null
        };

        await _repository.CreateEmployeeReferenceMapAsync(map, ct);
        return Response<Guid>.Success(map.Id, 201);
    }
}
