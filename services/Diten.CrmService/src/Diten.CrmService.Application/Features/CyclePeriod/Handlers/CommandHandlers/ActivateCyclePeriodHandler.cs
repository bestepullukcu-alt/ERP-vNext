using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CyclePeriod.Commands;
using Diten.CrmService.Application.Features.CyclePeriod.Contract;
using Diten.CrmService.Application.Features.CyclePeriod.Rules;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Handlers.CommandHandlers;

/// <summary>
/// Puts a period live. This is the ONE gate where the active-overlap ban is enforced, and it is fail-closed: if another
/// ACTIVE period at the SAME scope shares even a single day, the answer is 409 and the row stays <c>draft</c>. Nothing
/// is stamped before the check passes, so a half-activated period cannot exist.
/// <para>Scope here means the (ScopeType, ScopeRef) pair (FU07). Periods at DIFFERENT levels — a country calendar and a
/// business unit's own — may overlap freely, and must: that is exactly the situation the resolver's precedence exists
/// to decide, and banning it would make the fallback unreachable.</para>
/// <para>Why here and not at create: drafts are the planning space, where sketching two competing calendars is normal.
/// The invariant that actually matters is "at any instant, at most one ACTIVE period per scope" — which is exactly what
/// makes <see cref="Rules.CyclePeriodResolveEngine"/> able to answer with one period or honestly say there is none.</para>
/// <para>The refusal names the blocking period and its window, because an author who cannot see what they collide with
/// cannot fix it.</para>
/// </summary>
public sealed class ActivateCyclePeriodHandler : IRequestHandler<ActivateCyclePeriodCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICyclePeriodRepository _periods;

    public ActivateCyclePeriodHandler(ITenantContext tenant, IActorContext actor, ICyclePeriodRepository periods)
    {
        _tenant = tenant;
        _actor = actor;
        _periods = periods;
    }

    public async Task<Response<bool>> Handle(ActivateCyclePeriodCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var period = await _periods.GetByIdAsync(tenantId, request.CyclePeriodId, cancellationToken);
        if (period is null)
        {
            return Response<bool>.Fail("Cycle period not found.", 404);
        }

        if (period.IsClosed())
        {
            return Response<bool>.Fail(
                new[] { "A closed cycle period cannot be activated.", CyclePeriodErrorCodes.Closed }, 409);
        }

        if (period.IsActive())
        {
            return Response<bool>.Fail(
                new[] { "The cycle period is already active.", CyclePeriodErrorCodes.AlreadyActive }, 409);
        }

        var expectedVersion = request.ExpectedVersion ?? period.Version;
        if (expectedVersion != period.Version)
        {
            return Response<bool>.Fail(
                new[]
                {
                    "The cycle period changed since it was loaded. Reload and try again.",
                    CyclePeriodErrorCodes.ConcurrencyConflict
                },
                409);
        }

        var scopeType = period.EffectiveScopeType();
        var scopeRef = period.ScopeRef();

        var active = await _periods.ListActiveAsync(tenantId, cancellationToken);
        var overlaps = CyclePeriodOverlapRules.FindActiveOverlaps(
            active, scopeType, scopeRef, period.StartDate, period.EndDate, period.Id);
        if (overlaps.Count > 0)
        {
            return Response<bool>.Fail(
                new[]
                {
                    "This period overlaps an active cycle period at the same scope "
                    + $"({CyclePeriodScopeRules.Describe(scopeType, scopeRef)}): "
                    + CyclePeriodOverlapRules.DescribeOverlap(overlaps) + ".",
                    CyclePeriodErrorCodes.Overlap
                },
                409);
        }

        var now = DateTimeOffset.UtcNow;
        // Persists the derived scope for a legacy row the first time it goes live: nothing about the row's meaning
        // changes, the value it always had is simply written down.
        period.EnsureScopeType();
        period.CycleStatus = Domain.Entities.CyclePeriodStatuses.Active;
        period.ActivatedAt = now;
        period.ActivatedBy = _actor.ActorName;
        period.UpdatedAt = now;
        period.UpdatedBy = _actor.ActorName;

        var replaced = await _periods.ReplaceAsync(period, expectedVersion, cancellationToken);
        return replaced
            ? Response<bool>.Success(true)
            : Response<bool>.Fail(
                new[]
                {
                    "The cycle period changed since it was loaded. Reload and try again.",
                    CyclePeriodErrorCodes.ConcurrencyConflict
                },
                409);
    }
}
