using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.PlannedVisit.Commands;
using Diten.CrmService.Application.Features.PlannedVisit.Contract;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Application.Features.VisitWorkspace;
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
    private readonly ICallerScope _caller;
    private readonly VisitReasonValidator? _reasons;
    private readonly TimeProvider _clock;

    public CancelPlannedVisitHandler(
        ITenantContext tenant, IActorContext actor, IPlannedVisitRepository repository, ICallerScope caller,
        // WP-VW-W2 — the reason set check (fail-closed when a code is sent and the check is not wired) + "today" (UTC).
        VisitReasonValidator? reasons = null,
        TimeProvider? clock = null)
    {
        _reasons = reasons;
        _clock = clock ?? TimeProvider.System;
        _caller = caller;
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

        // WP-VW-W2 — the reason is a reference-set code (+ note). Without a code the legacy free-text reason is still
        // accepted for one more release (it becomes required; mobile note).
        var reasonCode = PlannedVisitValidation.Trim(request.ReasonCode);
        var note = PlannedVisitValidation.Trim(request.Note);
        var reason = PlannedVisitValidation.Trim(request.CancellationReason) ?? reasonCode;
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
        // WP-VP-2 (B-1) — another rep's plan answers 404 (no existence leak) unless the caller holds read-all.
        if (plan is null || !_caller.MayAccess(PlannedVisitPermissions.ReadAll, plan.Resource.ResourceId))
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

        // WP-VW-W2 — only today's or a future visit is cancelled; a past one is "missed" (not done / reschedule).
        if (plan.PlannedDate < DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime))
        {
            return Response<bool>.Fail(
                new[]
                {
                    "A visit whose day has passed is not cancelled; record it as not done or reschedule it.",
                    VisitWorkspaceErrorCodes.CancelPastDay
                },
                409);
        }

        if (reasonCode is not null)
        {
            var reasonFailure = _reasons is null
                ? new VisitReasonValidator.Failure(
                    "The reason list could not be read. Nothing was saved — please try again.",
                    VisitWorkspaceErrorCodes.ReferenceDataUnavailable, 503)
                : await _reasons.ValidateAsync(reasonCode, note, VisitOutcomeReasons.Cancel, cancellationToken);
            if (reasonFailure is not null)
            {
                return Response<bool>.Fail(new[] { reasonFailure.Message, reasonFailure.Code }, reasonFailure.StatusCode);
            }
        }

        var expectedVersion = request.ExpectedVersion ?? plan.Version;
        if (expectedVersion != plan.Version)
        {
            return ConcurrencyFail();
        }

        var now = DateTimeOffset.UtcNow;
        plan.PlanStatus = PlannedVisitStatus.Cancelled;
        plan.CancellationReason = reason;
        plan.CancellationReasonCode = reasonCode is null ? null : VisitReasonValidator.Normalize(reasonCode);
        plan.CancellationNote = reasonCode is null ? null : note;
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
