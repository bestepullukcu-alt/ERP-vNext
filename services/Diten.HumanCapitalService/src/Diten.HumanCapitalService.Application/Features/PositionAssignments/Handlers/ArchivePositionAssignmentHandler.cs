using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Application.Features.PositionAssignments.Commands;
using Diten.HumanCapitalService.Domain.Enums;
using Diten.HumanCapitalService.Domain.Repositories;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.PositionAssignments.Handlers;

public sealed class ArchivePositionAssignmentHandler
    : IRequestHandler<ArchivePositionAssignmentCommand, Response<bool>>
{
    private readonly IPositionAssignmentOverlayRepository _repository;
    private readonly ITenantContext _tenantContext;

    public ArchivePositionAssignmentHandler(
        IPositionAssignmentOverlayRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<bool>> Handle(ArchivePositionAssignmentCommand request, CancellationToken ct)
    {
        var tenant = PositionAssignmentGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<bool>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var entity = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
        if (entity is null)
        {
            return Response<bool>.Fail("Position assignment overlay was not found.", 404);
        }

        var now = DateTimeOffset.UtcNow;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.AssignmentState = AssignmentOverlayState.Archived;

        await _repository.UpdateAsync(entity, ct);
        return Response<bool>.Success(204);
    }
}
