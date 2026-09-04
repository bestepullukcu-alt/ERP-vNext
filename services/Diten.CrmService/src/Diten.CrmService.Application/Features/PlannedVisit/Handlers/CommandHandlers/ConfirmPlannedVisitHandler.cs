using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Application.Features.PlannedVisit.Commands;
using Diten.CrmService.Application.Features.PlannedVisit.Contract;
using Diten.CrmService.Application.Features.PlannedVisit.Provenance;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.PlannedVisit.Handlers.CommandHandlers;

/// <summary>
/// Confirms a plan (planned → confirmed). This is the ONE place the consent guard is fail-closed (D6): consent is
/// re-evaluated fresh, and a <c>blocked</c> / <c>unknown</c> verdict — or a filter that did not apply — answers 409 and
/// leaves the plan <c>planned</c> (never deleted). The fresh verdict is stored either way, so an author can see why the
/// confirm was refused. <c>allowed</c> and <c>not_applicable</c> pass. Nothing else is enforced here.
/// </summary>
public sealed class ConfirmPlannedVisitHandler : IRequestHandler<ConfirmPlannedVisitCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IPlannedVisitRepository _repository;
    private readonly PlannedVisitConsentProbe _consentProbe;

    public ConfirmPlannedVisitHandler(
        ITenantContext tenant,
        IActorContext actor,
        IPlannedVisitRepository repository,
        PlannedVisitConsentProbe consentProbe)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
        _consentProbe = consentProbe;
    }

    public async Task<Response<bool>> Handle(ConfirmPlannedVisitCommand request, CancellationToken cancellationToken)
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
            return Fail("An archived plan cannot be confirmed.", PlannedVisitErrorCodes.Archived, 409);
        }

        if (!PlannedVisitValidation.IsTransitionAllowed(plan.PlanStatus, PlannedVisitStatus.Confirmed)
            || plan.IsConfirmed())
        {
            return Fail(
                "Only a planned visit can be confirmed.", PlannedVisitErrorCodes.InvalidTransition, 409);
        }

        var expectedVersion = request.ExpectedVersion ?? plan.Version;
        if (expectedVersion != plan.Version)
        {
            return ConcurrencyFail();
        }

        // Re-evaluate consent fresh at the confirm instant and store the verdict.
        var consent = await _consentProbe.EvaluateAsync(plan, cancellationToken);
        plan.Consent = consent;

        // D6 fail-closed guard. Filter-not-applied → no eligibility inference; unknown is NEVER treated as allowed.
        if (!consent.FilterApplied)
        {
            await PersistVerdictAsync(plan, expectedVersion, cancellationToken);
            return Fail(
                "Consent filter was not applied; the plan cannot be confirmed.",
                PlannedVisitErrorCodes.ConsentFilterNotApplied, 409);
        }

        if (string.Equals(consent.EligibilityStatus, ConsentEligibilityStatus.Blocked, StringComparison.Ordinal))
        {
            await PersistVerdictAsync(plan, expectedVersion, cancellationToken);
            return Fail(
                "Consent blocks this contact; the plan cannot be confirmed.",
                PlannedVisitErrorCodes.BlockedByConsent, 409);
        }

        if (string.Equals(consent.EligibilityStatus, ConsentEligibilityStatus.Unknown, StringComparison.Ordinal))
        {
            await PersistVerdictAsync(plan, expectedVersion, cancellationToken);
            return Fail(
                "Consent is unknown; the plan cannot be confirmed (unknown is never treated as allowed).",
                PlannedVisitErrorCodes.ConsentUnknown, 409);
        }

        var now = DateTimeOffset.UtcNow;
        plan.PlanStatus = PlannedVisitStatus.Confirmed;
        plan.UpdatedAt = now;
        plan.UpdatedBy = _actor.ActorName;

        var replaced = await _repository.ReplaceAsync(plan, expectedVersion, cancellationToken);
        return replaced ? Response<bool>.Success(true) : ConcurrencyFail();
    }

    /// <summary>Persists ONLY the refreshed consent verdict (the plan stays <c>planned</c>) so the refusal reason is
    /// visible. A concurrency miss here is swallowed: the confirm is being refused anyway.</summary>
    private async Task PersistVerdictAsync(
        Domain.Entities.PlannedVisit plan, int expectedVersion, CancellationToken cancellationToken)
    {
        plan.UpdatedAt = DateTimeOffset.UtcNow;
        plan.UpdatedBy = _actor.ActorName;
        await _repository.ReplaceAsync(plan, expectedVersion, cancellationToken);
    }

    private static Response<bool> Fail(string message, string code, int status)
        => Response<bool>.Fail(new[] { message, code }, status);

    private static Response<bool> ConcurrencyFail()
        => Response<bool>.Fail(
            new[] { "The plan changed since it was loaded. Reload and try again.", PlannedVisitErrorCodes.ConcurrencyConflict },
            409);
}
