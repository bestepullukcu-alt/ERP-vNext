using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.CycleCapacity.Commands;
using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.CycleCapacity.Handlers.CommandHandlers;

/// <summary>
/// Edits a capacity's inputs.
/// <para><b>The pin does not move.</b> The command carries no <c>CyclePeriodId</c>, and the existing row's value is
/// re-validated rather than re-assigned: re-pointing a capacity at another period would silently change what a past
/// estimate was an estimate OF. The API surface has no way to express the move at all, which is stronger than
/// rejecting it.</para>
/// <para><b>The FTE does not move either.</b> The command carries none, so the stored interim value survives every
/// edit and the row keeps reproducing the same figure (D-FTE).</para>
/// </summary>
public sealed class UpdateCycleCapacityHandler : IRequestHandler<UpdateCycleCapacityCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ICycleCapacityRepository _capacities;
    private readonly CycleCapacityWriteValidator _writes;

    public UpdateCycleCapacityHandler(
        ITenantContext tenant,
        IActorContext actor,
        ICycleCapacityRepository capacities,
        CycleCapacityWriteValidator writes)
    {
        _tenant = tenant;
        _actor = actor;
        _capacities = capacities;
        _writes = writes;
    }

    public async Task<Response<bool>> Handle(UpdateCycleCapacityCommand request, CancellationToken cancellationToken)
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

        // The pin is re-validated, never re-assigned: the closed-period lock and the month-window rule are both judged
        // against the period this capacity has always belonged to.
        var validation = await _writes.ValidateAsync(
            entity.CyclePeriodId,
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
            return Response<bool>.Fail(CycleCapacityValidation.ToErrors(resolved), resolved.StatusCode);
        }

        entity.CalendarCountryCode = validation.CalendarCountryCode;
        entity.DailyWorkMinutes = request.DailyWorkMinutes;
        entity.PromoProductTime = request.PromoProductTime;
        entity.NonPromoProductTime = request.NonPromoProductTime;
        entity.TravelingTime = request.TravelingTime;
        entity.ReportDuration = request.ReportDuration;
        entity.QuizDuration = request.QuizDuration;
        entity.BetweenVisitTimeMinutes = validation.BetweenVisitTimeMinutes;
        entity.Description = CycleCapacityValidation.Trim(request.Description);
        entity.Months = validation.Months.ToList();
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = _actor.ActorName;

        var expectedVersion = request.ExpectedVersion ?? entity.Version;
        var replaced = await _capacities.ReplaceAsync(entity, expectedVersion, cancellationToken);

        return replaced
            ? Response<bool>.Success(true, 200)
            : Response<bool>.Fail(
                new[]
                {
                    "The capacity changed since it was loaded. Reload it and re-apply the edit — nothing was "
                    + "overwritten.",
                    CycleCapacityReasonCodes.ConcurrencyConflict
                },
                409);
    }
}
