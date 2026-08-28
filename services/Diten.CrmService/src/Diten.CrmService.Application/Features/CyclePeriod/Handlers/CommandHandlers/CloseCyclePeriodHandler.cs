using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CyclePeriod.Commands;
using Diten.CrmService.Application.Features.CyclePeriod.Contract;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.CyclePeriod.Handlers.CommandHandlers;

/// <summary>
/// Ends a period. Reachable from <c>draft</c> (a plan that never ran — closed with a trace rather than deleted) and
/// from <c>active</c>.
/// <para><b>Closed is terminal and there is no reopen command anywhere.</b> MicroTarget rows, visits and reports point
/// at a period by id, so re-opening one would retroactively change what a past plan meant. Closing also frees the
/// period's days for a new active period in the same scope, which is the supported way to correct a live calendar.</para>
/// <para>Closing is an explicit operator act: no background job ever closes a period whose end date has passed. An
/// expired but still-active period simply stops resolving, which is a visible state rather than a silent one.</para>
/// </summary>
public sealed class CloseCyclePeriodHandler : IRequestHandler<CloseCyclePeriodCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICyclePeriodRepository _periods;

    public CloseCyclePeriodHandler(ITenantContext tenant, IActorContext actor, ICyclePeriodRepository periods)
    {
        _tenant = tenant;
        _actor = actor;
        _periods = periods;
    }

    public async Task<Response<bool>> Handle(CloseCyclePeriodCommand request, CancellationToken cancellationToken)
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
                new[] { "The cycle period is already closed.", CyclePeriodErrorCodes.Closed }, 409);
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

        var now = DateTimeOffset.UtcNow;
        // Writes down the scope a legacy row already had (FU07 read-time derivation), without changing its meaning.
        period.EnsureScopeType();
        period.CycleStatus = Domain.Entities.CyclePeriodStatuses.Closed;
        period.ClosedAt = now;
        period.ClosedBy = _actor.ActorName;
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
