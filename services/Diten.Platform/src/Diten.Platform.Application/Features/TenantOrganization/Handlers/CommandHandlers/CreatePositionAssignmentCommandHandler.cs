using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class CreatePositionAssignmentCommandHandler : IRequestHandler<CreatePositionAssignmentCommand, Response<Guid>>
{
    private readonly IPositionAssignmentRepository _assignments;
    private readonly IPositionRepository _positions;
    private readonly ITenantContext _tenantContext;
    private readonly IUserReferenceValidator _userReferenceValidator;

    public CreatePositionAssignmentCommandHandler(IPositionAssignmentRepository assignments, IPositionRepository positions, ITenantContext tenantContext, IUserReferenceValidator userReferenceValidator)
    {
        _assignments = assignments;
        _positions = positions;
        _tenantContext = tenantContext;
        _userReferenceValidator = userReferenceValidator;
    }

    public async Task<Response<Guid>> Handle(CreatePositionAssignmentCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var position = await _positions.GetByIdAsync(request.Request.PositionId, ct);
        if (position == null || position.IsArchived)
        {
            return Response<Guid>.Fail("Position not found.", 404);
        }

        var userValidation = await _userReferenceValidator.ValidateAsync(request.Request.UserId, ct);
        if (!userValidation.IsSuccessful || userValidation.Data?.Referenceable != true)
        {
            return Response<Guid>.Fail("User is not referenceable.", 404);
        }

        var entity = new PositionAssignment
        {
            TenantId = tenantId,
            PositionId = request.Request.PositionId,
            UserId = request.Request.UserId,
            EffectiveFrom = request.Request.EffectiveFrom,
            EffectiveTo = request.Request.EffectiveTo
        };
        TenantOrganizationMapper.ApplyEnterpriseFields(entity, request.Request);

        // One-primary-per-position rule (Secondary/Acting/Delegated may overlap).
        if (entity.AssignmentType == AssignmentType.Primary && !entity.IsCancelled
            && await PositionAssignmentPrimaryGuard.HasPrimaryOverlapAsync(_assignments, entity.PositionId, entity.EffectiveFrom, entity.EffectiveTo, null, ct))
        {
            return Response<Guid>.Fail("Another Primary assignment already covers this interval for the position.", 409);
        }

        await _assignments.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
