using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

public sealed class UpdatePositionAssignmentCommandHandler : IRequestHandler<UpdatePositionAssignmentCommand, Response<NoContent>>
{
    private readonly IPositionAssignmentRepository _assignments;
    private readonly IPositionRepository _positions;

    public UpdatePositionAssignmentCommandHandler(IPositionAssignmentRepository assignments, IPositionRepository positions)
    {
        _assignments = assignments;
        _positions = positions;
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

        if (await _assignments.HasOverlapAsync(request.Request.PositionId, request.Request.EffectiveFrom, request.Request.EffectiveTo, request.Id, ct))
        {
            return Response<NoContent>.Fail("Position Assignment interval overlaps an existing assignment.", 409);
        }

        entity.PositionId = request.Request.PositionId;
        entity.UserId = request.Request.UserId;
        entity.EffectiveFrom = request.Request.EffectiveFrom;
        entity.EffectiveTo = request.Request.EffectiveTo;
        await _assignments.UpdateAsync(entity, ct);
        return Response<NoContent>.Success(204);
    }
}
