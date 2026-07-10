using Diten.Platform.Application.Common;
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

        var canonicalCode = OrganizationCodeNormalizer.Normalize(request.Request.Code);
        if (string.IsNullOrWhiteSpace(canonicalCode))
        {
            return Response<NoContent>.Fail("Position code is required.", 400);
        }

        if (await _positions.ExistsByCodeAsync(canonicalCode, request.Id, ct))
        {
            return Response<NoContent>.Fail("Position code already exists.", 409);
        }

        var referenceCheck = await PositionReferenceGuard.ValidateAsync(_positions, _organizationUnits, request.Id, request.Request.OrganizationUnitId, request.Request.ReportsToPositionId, ct);
        if (!referenceCheck.IsSuccessful)
        {
            return referenceCheck;
        }

        entity.Code = canonicalCode;
        entity.Name = request.Request.Name.Trim();
        entity.OrganizationUnitId = request.Request.OrganizationUnitId;
        entity.ReportsToPositionId = request.Request.ReportsToPositionId;
        TenantOrganizationMapper.ApplyEnterpriseFields(entity, request.Request);
        await _positions.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }
}
