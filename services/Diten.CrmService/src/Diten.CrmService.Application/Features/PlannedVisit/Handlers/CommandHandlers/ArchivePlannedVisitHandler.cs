using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.PlannedVisit.Commands;
using Diten.CrmService.Application.Features.PlannedVisit.Contract;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.PlannedVisit.Handlers.CommandHandlers;

/// <summary>
/// Archives a plan (any non-archived status → archived). Terminal (§12.2): there is no unarchive endpoint. An archived
/// row is hidden from the default list and accepts no further mutation. This is NOT a delete — the row and its history
/// stay readable (§8.2).
/// </summary>
public sealed class ArchivePlannedVisitHandler : IRequestHandler<ArchivePlannedVisitCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IPlannedVisitRepository _repository;

    public ArchivePlannedVisitHandler(ITenantContext tenant, IActorContext actor, IPlannedVisitRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(ArchivePlannedVisitCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var plan = await _repository.GetByIdAsync(tenantId, request.PlannedVisitId, cancellationToken);
        if (plan is null)
        {
            return Response<bool>.Fail("Planned visit not found.", 404);
        }

        if (plan.IsArchived())
        {
            return Response<bool>.Fail(
                new[] { "The plan is already archived.", PlannedVisitErrorCodes.Archived }, 409);
        }

        var expectedVersion = request.ExpectedVersion ?? plan.Version;
        if (expectedVersion != plan.Version)
        {
            return ConcurrencyFail();
        }

        var now = DateTimeOffset.UtcNow;
        plan.PlanStatus = PlannedVisitStatus.Archived;
        plan.ArchivedAt = now;
        plan.ArchivedBy = _actor.ActorName;
        plan.UpdatedAt = now;
        plan.UpdatedBy = _actor.ActorName;

        var replaced = await _repository.ReplaceAsync(plan, expectedVersion, cancellationToken);
        return replaced ? Response<bool>.Success(true) : ConcurrencyFail();
    }

    private static Response<bool> ConcurrencyFail()
        => Response<bool>.Fail(
            new[] { "The plan changed since it was loaded. Reload and try again.", PlannedVisitErrorCodes.ConcurrencyConflict },
            409);
}
