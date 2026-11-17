using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.RecordCodes;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class UpdatePositionCommandHandler : IRequestHandler<UpdatePositionCommand, Response<NoContent>>
{
    private readonly IPositionRepository _positions;
    private readonly IOrganizationUnitRepository _organizationUnits;

    public UpdatePositionCommandHandler(IPositionRepository positions, IOrganizationUnitRepository organizationUnits)
    {
        _positions = positions;
        _organizationUnits = organizationUnits;
    }

    public async Task<Response<NoContent>> Handle(UpdatePositionCommand request, CancellationToken ct)
    {
        var entity = await _positions.GetByIdAsync(request.Id, ct);
        if (entity == null)
        {
            return Response<NoContent>.Fail("Position not found.", 404);
        }

        if (entity.IsArchived)
        {
            return Response<NoContent>.Fail("Archived Position cannot be mutated.", 409);
        }

        /*
         * MOD-0040-FU02 DL-2 (Q-2 A, RCS-001 §6) — NO code changes on update, old hand-typed codes included. Blank keeps the
         * stored code; one EQUAL to it (ordinal — nothing trimmed, upper-cased or normalized) is accepted, because the forms
         * and the drag-and-drop re-link send the whole record back; anything else is 409 and nothing is written.
         */
        if (!string.IsNullOrWhiteSpace(request.Request.Code)
            && !string.Equals(request.Request.Code, entity.Code, StringComparison.Ordinal))
        {
            return Response<NoContent>.Fail("The Position code cannot be changed.", 409, RecordCodeReasonCodes.Immutable);
        }

        var referenceCheck = await PositionReferenceGuard.ValidateAsync(_positions, _organizationUnits, request.Id, request.Request.OrganizationUnitId, request.Request.ReportsToPositionId, ct);
        if (!referenceCheck.IsSuccessful)
        {
            return referenceCheck;
        }

        entity.Name = request.Request.Name.Trim();
        entity.OrganizationUnitId = request.Request.OrganizationUnitId;
        entity.ReportsToPositionId = request.Request.ReportsToPositionId;
        TenantOrganizationMapper.ApplyEnterpriseFields(entity, request.Request);
        await _positions.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }
}
