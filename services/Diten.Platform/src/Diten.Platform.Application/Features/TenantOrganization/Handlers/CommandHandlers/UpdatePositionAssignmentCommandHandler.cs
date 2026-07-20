using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Application.Features.TenantOrganization.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class UpdatePositionAssignmentCommandHandler : IRequestHandler<UpdatePositionAssignmentCommand, Response<NoContent>>
{
    private readonly IPositionAssignmentRepository _assignments;
    private readonly IPositionRepository _positions;
    private readonly IUserReferenceValidator _userReferenceValidator;

    public UpdatePositionAssignmentCommandHandler(IPositionAssignmentRepository assignments, IPositionRepository positions, IUserReferenceValidator userReferenceValidator)
    {
        _assignments = assignments;
        _positions = positions;
        _userReferenceValidator = userReferenceValidator;
    }

    public async Task<Response<NoContent>> Handle(UpdatePositionAssignmentCommand request, CancellationToken ct)
    {
        var entity = await _assignments.GetByIdAsync(request.Id, ct);
        if (entity == null)
        {
            return Response<NoContent>.Fail("Position Assignment not found.", 404);
        }

        var position = await _positions.GetByIdAsync(request.Request.PositionId, ct);
        if (position == null || position.IsArchived)
        {
            return Response<NoContent>.Fail("Position not found.", 404);
        }

        var userValidation = await _userReferenceValidator.ValidateAsync(request.Request.UserId, ct);
        if (!userValidation.IsSuccessful || userValidation.Data?.Referenceable != true)
        {
            return Response<NoContent>.Fail("User is not referenceable.", 404);
        }

        entity.PositionId = request.Request.PositionId;
        entity.UserId = request.Request.UserId;
        entity.EffectiveFrom = request.Request.EffectiveFrom;
        entity.EffectiveTo = request.Request.EffectiveTo;
        TenantOrganizationMapper.ApplyEnterpriseFields(entity, request.Request);

        // One-primary-per-position rule (Secondary/Acting/Delegated may overlap); exclude self on update.
        if (entity.AssignmentType == AssignmentType.Primary && !entity.IsCancelled
            && await PositionAssignmentPrimaryGuard.HasPrimaryOverlapAsync(_assignments, entity.PositionId, entity.EffectiveFrom, entity.EffectiveTo, entity.Id, ct))
        {
            return Response<NoContent>.Fail("Another Primary assignment already covers this interval for the position.", 409);
        }

        await _assignments.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }
}
