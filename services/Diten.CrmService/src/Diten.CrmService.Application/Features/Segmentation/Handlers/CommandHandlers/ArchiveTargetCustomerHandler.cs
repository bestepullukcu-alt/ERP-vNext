using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Commands;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.CommandHandlers;

/// <summary>Closes one hand-written membership row. Soft, like every close in this FU: the row stays readable, so the
/// answer to "who decided this, and why?" survives the decision being reversed.</summary>
public sealed class ArchiveTargetCustomerHandler : IRequestHandler<ArchiveTargetCustomerCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ITargetCustomerRepository _targets;

    public ArchiveTargetCustomerHandler(
        ITenantContext tenant, IActorContext actor, ITargetCustomerRepository targets)
    {
        _tenant = tenant;
        _actor = actor;
        _targets = targets;
    }

    public async Task<Response<bool>> Handle(
        ArchiveTargetCustomerCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var target = await _targets.GetByIdAsync(tenantId, request.TargetCustomerId, cancellationToken);
        if (target is null || target.SegmentId != request.SegmentId)
        {
            return Response<bool>.Fail("Membership row not found.", 404);
        }

        if (target.IsArchived())
        {
            return Response<bool>.Fail("The membership row is already archived.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var expectedVersion = request.ExpectedVersion ?? target.Version;

        target.ArchivedAt = now;
        target.ArchivedBy = _actor.ActorName;
        target.UpdatedAt = now;
        target.UpdatedBy = _actor.ActorName;

        var replaced = await _targets.ReplaceAsync(target, expectedVersion, cancellationToken);
        return replaced
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The membership row changed since it was loaded. Reload and try again.", 409);
    }
}
