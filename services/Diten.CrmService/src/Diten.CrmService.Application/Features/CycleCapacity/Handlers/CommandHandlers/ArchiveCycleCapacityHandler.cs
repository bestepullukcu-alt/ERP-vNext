using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CycleCapacity.Commands;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Handlers.CommandHandlers;

/// <summary>
/// Retires a capacity — a SOFT archive. Nothing is deleted: the inputs an old estimate was made from stay readable,
/// which is the point of keeping them at all.
/// <para>Archiving a capacity <b>frees its period</b>: the 1:1 rule counts non-archived rows, so a period whose
/// capacity was archived can be given a fresh one. That is the deliberate, narrow way to redo a capacity without
/// opening scenario comparison (F-SCENARIO).</para>
/// <para>The closed-period lock applies here too: a period that has ended freezes its capacity in every direction,
/// archiving included.</para>
/// </summary>
public sealed class ArchiveCycleCapacityHandler : IRequestHandler<ArchiveCycleCapacityCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICycleCapacityRepository _capacities;
    private readonly ICyclePeriodReader _periods;

    public ArchiveCycleCapacityHandler(
        ITenantContext tenant,
        IActorContext actor,
        ICycleCapacityRepository capacities,
        ICyclePeriodReader periods)
    {
        _tenant = tenant;
        _actor = actor;
        _capacities = capacities;
        _periods = periods;
    }

    public async Task<Response<bool>> Handle(ArchiveCycleCapacityCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _capacities.GetByIdAsync(tenantId, request.CycleCapacityId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail(
                new[] { "Cycle capacity not found.", CycleCapacityReasonCodes.NotFound }, 404);
        }

        if (entity.IsArchived)
        {
            // Already in the requested state. Answering 200 keeps archive idempotent, so a retried request is not an
            // error and a UI need not distinguish "archived now" from "archived a moment ago".
            return Response<bool>.Success(true, 200);
        }

        var period = await _periods.GetByIdAsync(entity.CyclePeriodId, cancellationToken);
        if (period is not null
            && string.Equals(period.CycleStatus, CyclePeriodStatuses.Closed, StringComparison.Ordinal))
        {
            return Response<bool>.Fail(
                new[]
                {
                    $"Cycle period '{period.CycleCode}' is closed, so its capacity can no longer be changed.",
                    CycleCapacityReasonCodes.PeriodClosed
                },
                409);
        }

        entity.IsArchived = true;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = _actor.ActorName;

        var expectedVersion = request.ExpectedVersion ?? entity.Version;
        var replaced = await _capacities.ReplaceAsync(entity, expectedVersion, cancellationToken);

        return replaced
            ? Response<bool>.Success(true, 200)
            : Response<bool>.Fail(
                new[]
                {
                    "The capacity changed since it was loaded. Reload it and try again — nothing was overwritten.",
                    CycleCapacityReasonCodes.ConcurrencyConflict
                },
                409);
    }
}
