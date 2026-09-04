using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.PlannedVisit.Commands;
using Diten.CrmService.Application.Features.PlannedVisit.Contract;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.PlannedVisit.Handlers.CommandHandlers;

/// <summary>
/// Cancels a plan (draft/planned/confirmed → cancelled). <see cref="CancelPlannedVisitCommand.CancellationReason"/> is
/// REQUIRED (V21). The row is never deleted, so it stays readable with its reason; a cancelled plan no longer holds a
/// slot and drops out of the overlap + same-day-type guards (V25).
/// </summary>
public sealed class CancelPlannedVisitHandler : IRequestHandler<CancelPlannedVisitCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IPlannedVisitRepository _repository;

    public CancelPlannedVisitHandler(ITenantContext tenant, IActorContext actor, IPlannedVisitRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(CancelPlannedVisitCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var reason = PlannedVisitValidation.Trim(request.CancellationReason);
        if (reason is null)
        {
            return Response<bool>.Fail(
                new[] { "A cancellation reason is required.", PlannedVisitErrorCodes.CancellationReasonRequired }, 400);
        }

        if (reason.Length > PlannedVisitLimits.MaxCancellationReasonLength)
        {
            return Response<bool>.Fail(
                new[]
                {
                    $"CancellationReason must be at most {PlannedVisitLimits.MaxCancellationReasonLength} characters.",
                    PlannedVisitErrorCodes.CancellationReasonRequired
                },
                400);
        }

        var plan = await _repository.GetByIdAsync(tenantId, request.PlannedVisitId, cancellationToken);
        if (plan is null)
        {
            return Response<bool>.Fail("Planned visit not found.", 404);
        }

        if (plan.IsArchived())
        {
            return Response<bool>.Fail(
                new[] { "An archived plan cannot be cancelled.", PlannedVisitErrorCodes.Archived }, 409);
        }

        if (plan.IsCancelled() || !PlannedVisitValidation.IsTransitionAllowed(plan.PlanStatus, PlannedVisitStatus.Cancelled))
        {
            return Response<bool>.Fail(
                new[] { "This plan cannot be cancelled from its current status.", PlannedVisitErrorCodes.InvalidTransition },
                409);
        }

        var expectedVersion = request.ExpectedVersion ?? plan.Version;
        if (expectedVersion != plan.Version)
        {
            return ConcurrencyFail();
        }

        var now = DateTimeOffset.UtcNow;
        plan.PlanStatus = PlannedVisitStatus.Cancelled;
        plan.CancellationReason = reason;
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
