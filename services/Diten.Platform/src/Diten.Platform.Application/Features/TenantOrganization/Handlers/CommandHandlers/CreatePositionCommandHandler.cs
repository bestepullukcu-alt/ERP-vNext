using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class CreatePositionCommandHandler : IRequestHandler<CreatePositionCommand, Response<Guid>>
{
    private readonly IPositionRepository _positions;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly ITenantContext _tenantContext;

    public CreatePositionCommandHandler(IPositionRepository positions, IOrganizationUnitRepository organizationUnits, ITenantContext tenantContext)
    {
        _positions = positions;
        _organizationUnits = organizationUnits;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreatePositionCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var canonicalCode = OrganizationCodeNormalizer.Normalize(request.Request.Code);
        if (string.IsNullOrWhiteSpace(canonicalCode))
        {
            return Response<Guid>.Fail("Position code is required.", 400);
        }

        if (await _positions.ExistsByCodeAsync(canonicalCode, null, ct))
        {
            return Response<Guid>.Fail("Position code already exists.", 409);
        }

        var referenceCheck = await PositionReferenceGuard.ValidateAsync(_positions, _organizationUnits, null, request.Request.OrganizationUnitId, request.Request.ReportsToPositionId, ct);
        if (!referenceCheck.IsSuccessful)
        {
            return Response<Guid>.Fail(referenceCheck.Errors, referenceCheck.StatusCode);
        }

        var entity = new Position
        {
            TenantId = tenantId,
            Code = canonicalCode,
            Name = request.Request.Name.Trim(),
            OrganizationUnitId = request.Request.OrganizationUnitId,
            ReportsToPositionId = request.Request.ReportsToPositionId
        };

        await _positions.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
