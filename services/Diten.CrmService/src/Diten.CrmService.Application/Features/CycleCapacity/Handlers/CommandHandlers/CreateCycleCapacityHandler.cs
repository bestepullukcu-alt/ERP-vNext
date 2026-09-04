using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CycleCapacity.Commands;
using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Features.CycleCapacity.Handlers.CommandHandlers;

/// <summary>
/// Creates the capacity model of one cycle period.
/// <para>Order is fixed: the shared write gate (pin → closed-period lock → shape → calendar country → month rows) runs
/// FIRST and completes every external check, then the 1:1 rule is decided, then the row is written. <b>Every external
/// call finishes before the insert</b>, so a dependency outage can never leave a half-authored capacity behind.</para>
/// <para><b>The FTE is written by the server, never by the caller.</b> The command carries no FTE at all; the interim
/// configured average is stamped here together with its provenance. A caller who re-enables the disabled field in the
/// browser and posts a value changes nothing, because there is no field to change.</para>
/// <para>This handler touches exactly one collection. It creates no MicroTarget row, no visit, no frequency policy and
/// no working-calendar entry, and it never writes to CyclePeriod.</para>
/// </summary>
public sealed class CreateCycleCapacityHandler : IRequestHandler<CreateCycleCapacityCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICycleCapacityRepository _capacities;
    private readonly CycleCapacityWriteValidator _writes;
    private readonly ICycleCapacityDefaultsProvider _defaults;

    public CreateCycleCapacityHandler(
        ITenantContext tenant,
        IActorContext actor,
        ICycleCapacityRepository capacities,
        CycleCapacityWriteValidator writes,
        ICycleCapacityDefaultsProvider defaults)
    {
        _tenant = tenant;
        _actor = actor;
        _capacities = capacities;
        _writes = writes;
        _defaults = defaults;
    }

    public async Task<Response<Guid>> Handle(CreateCycleCapacityCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var validation = await _writes.ValidateAsync(
            request.CyclePeriodId,
            request.CalendarCountryCode,
            request.DailyWorkMinutes,
            request.PromoProductTime,
            request.NonPromoProductTime,
            request.TravelingTime,
            request.ReportDuration,
            request.QuizDuration,
            request.Description,
            request.Months,
            request.BetweenVisitTimeMinutes,
            cancellationToken);

        if (validation.Failure is not null || validation.Months is null || validation.CalendarCountryCode is null)
        {
            var resolved = validation.Failure ?? new CycleCapacityValidation.Failure(
                "The capacity could not be validated.", CycleCapacityReasonCodes.MonthsRequired);
            return Response<Guid>.Fail(CycleCapacityValidation.ToErrors(resolved), resolved.StatusCode);
        }

        // 1:1. Decided in the handler AND backed by a partial unique index: the index is the guarantee, the handler is
        // the readable error. A concurrent second create loses at the index rather than producing a second row.
        var existing = await _capacities.GetByCyclePeriodAsync(tenantId, request.CyclePeriodId, cancellationToken);
        if (existing is not null)
        {
            return Response<Guid>.Fail(
                new[]
                {
                    "This cycle period already has a capacity model. A period carries at most one — edit the existing "
                    + "one, or archive it first.",
                    CycleCapacityReasonCodes.DuplicateCapacity
                },
                409);
        }

        var entity = new CapacityEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CyclePeriodId = request.CyclePeriodId,
            CalendarCountryCode = validation.CalendarCountryCode,
            DailyWorkMinutes = request.DailyWorkMinutes,
            PromoProductTime = request.PromoProductTime,
            NonPromoProductTime = request.NonPromoProductTime,
            TravelingTime = request.TravelingTime,
            ReportDuration = request.ReportDuration,
            QuizDuration = request.QuizDuration,

            // FU06B — the between-visit buffer, resolved (payload or configured default) and range-checked by the write
            // validator. It is stored but never enters the capacity arithmetic.
            BetweenVisitTimeMinutes = validation.BetweenVisitTimeMinutes,

            // FU07 — the FTE now lives on each month and is stamped by the write validator, from the same configured
            // average. Nothing capacity-wide is written here any more.
            Description = CycleCapacityValidation.Trim(request.Description),
            Months = validation.Months.ToList(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _actor.ActorName
        };

        await _capacities.InsertAsync(entity, cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
