using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TenantOrganization.Commands;
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

    public CreatePositionAssignmentCommandHandler(IPositionAssignmentRepository assignments, IPositionRepository positions, ITenantContext tenantContext)
    {
        _assignments = assignments;
        _positions = positions;
        _tenantContext = tenantContext;
    }

    public async Task<Response<Guid>> Handle(CreatePositionAssignmentCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(_tenantContext);
        var position = await _positions.GetByIdAsync(request.Request.PositionId, ct);
        if (position == null || position.IsArchived)
        {
            return Response<Guid>.Fail("Position not found.", 404);
        }

        if (await _assignments.HasOverlapAsync(request.Request.PositionId, request.Request.EffectiveFrom, request.Request.EffectiveTo, null, ct))
        {
            return Response<Guid>.Fail("Position Assignment interval overlaps an existing assignment.", 409);
        }

        var entity = new PositionAssignment
        {
            TenantId = tenantId,
            PositionId = request.Request.PositionId,
            UserId = request.Request.UserId,
            EffectiveFrom = request.Request.EffectiveFrom,
            EffectiveTo = request.Request.EffectiveTo
        };

        await _assignments.CreateAsync(entity, ct);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
