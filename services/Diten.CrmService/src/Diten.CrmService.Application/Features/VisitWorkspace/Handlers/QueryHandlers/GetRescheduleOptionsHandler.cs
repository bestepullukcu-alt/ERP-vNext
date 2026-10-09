using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.VisitWorkspace.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitWorkspace.Handlers.QueryHandlers;

/// <summary>
/// WP-VW-W2 (A2) — the reschedule dialog's day list: the next <see cref="VisitWorkspaceLimits.RescheduleOptionDays"/>
/// days after today (UTC) the visit can be moved to — working days inside the rep's active period, holidays / weekends
/// skipped — with how many visits and minutes the rep already has on each and the day's capacity (4G budget). The same
/// <see cref="VisitWorkspaceDays"/> answer the reschedule date rule checks, so every listed day is accepted.
/// </summary>
public sealed class GetRescheduleOptionsHandler : IRequestHandler<GetRescheduleOptionsQuery, Response<RescheduleOptionsDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IPlannedVisitRepository _plannedVisits;
    private readonly ICallerScope _caller;
    private readonly VisitWorkspaceDays _days;
    private readonly TimeProvider _clock;

    public GetRescheduleOptionsHandler(
        ITenantContext tenant, IPlannedVisitRepository plannedVisits, ICallerScope caller, VisitWorkspaceDays days,
        TimeProvider? clock = null)
    {
        _tenant = tenant;
        _plannedVisits = plannedVisits;
        _caller = caller;
        _days = days;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<RescheduleOptionsDto>> Handle(
        GetRescheduleOptionsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<RescheduleOptionsDto>.Fail("Tenant context is required.", 400);
        }

        var plan = await _plannedVisits.GetByIdAsync(tenantId, request.PlannedVisitId, cancellationToken);
        // Another rep's visit is as absent as a missing one (WP-VP-2 B-1).
        if (plan is null || !_caller.MayAccess(PlannedVisitPermissions.ReadAll, plan.Resource.ResourceId))
        {
            return Response<RescheduleOptionsDto>.Fail("Planned visit not found.", 404);
        }

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var days = await _days.RescheduleOptionsAsync(
            plan.Resource.ResourceId, today, VisitWorkspaceLimits.RescheduleOptionDays, cancellationToken);

        return Response<RescheduleOptionsDto>.Success(new RescheduleOptionsDto(
            plan.Id,
            days.Select(d => new RescheduleOptionDto(
                    d.Date.ToString("yyyy-MM-dd"), d.PlannedCount, d.CapacityMinutes, d.PlannedMinutes, d.IsHoliday))
                .ToList()));
    }
}
