using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Application.Features.VisitWorkspace.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitWorkspace.Handlers.QueryHandlers;

/// <summary>
/// WP-VW-W2 (A2) — the reschedule dialog's day list: the next <see cref="VisitWorkspaceLimits.RescheduleOptionDays"/>
/// days after today (UTC) the visit can be moved to — working days inside the rep's active period, holidays / weekends
/// skipped — with how many visits and minutes the rep already has on each and the day's capacity (4G budget). The same
/// <see cref="VisitWorkspaceDays"/> answer the reschedule date rule checks, so every listed day is accepted.
/// <para>CT (live E4, 2026-10-09) — a day of a DRAFT week carries the draft plan's load too (the same preview the
/// workspace calendar reads): its minutes are the preview day's planned minutes and its count adds the preview visits,
/// so the rep does not see an empty day that the draft week already fills.</para>
/// </summary>
public sealed class GetRescheduleOptionsHandler : IRequestHandler<GetRescheduleOptionsQuery, Response<RescheduleOptionsDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IPlannedVisitRepository _plannedVisits;
    private readonly ICallerScope _caller;
    private readonly VisitWorkspaceDays _days;
    private readonly TimeProvider _clock;
    private readonly IPlanningSessionRepository? _sessions;
    private readonly IWorkspacePlanPreviewSource? _previews;

    public GetRescheduleOptionsHandler(
        ITenantContext tenant, IPlannedVisitRepository plannedVisits, ICallerScope caller, VisitWorkspaceDays days,
        TimeProvider? clock = null,
        IPlanningSessionRepository? sessions = null,
        IWorkspacePlanPreviewSource? previews = null)
    {
        _tenant = tenant;
        _plannedVisits = plannedVisits;
        _caller = caller;
        _days = days;
        _clock = clock ?? TimeProvider.System;
        _sessions = sessions;
        _previews = previews;
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
        var draft = await DraftLoadAsync(tenantId, plan.Resource.ResourceId, cancellationToken);

        return Response<RescheduleOptionsDto>.Success(new RescheduleOptionsDto(
            plan.Id,
            days.Select(d => draft.TryGetValue(d.Date, out var load)
                    ? new RescheduleOptionDto(
                        d.Date.ToString("yyyy-MM-dd"), d.PlannedCount + load.Count, d.CapacityMinutes, load.Minutes, d.IsHoliday)
                    : new RescheduleOptionDto(
                        d.Date.ToString("yyyy-MM-dd"), d.PlannedCount, d.CapacityMinutes, d.PlannedMinutes, d.IsHoliday))
                .ToList()));
    }

    /// <summary>The draft weeks' load per day (preview day minutes — fixed load included — and preview visit count), the
    /// rep's newest non-archived session first, exactly as the workspace calendar reads it. Empty without a preview.</summary>
    private async Task<Dictionary<DateOnly, (int Minutes, int Count)>> DraftLoadAsync(
        Guid tenantId, string resourceId, CancellationToken cancellationToken)
    {
        var load = new Dictionary<DateOnly, (int Minutes, int Count)>();
        if (_sessions is null || _previews is null)
        {
            return load;
        }

        var sessions = (await _sessions.ListAsync(tenantId, cancellationToken))
            .Where(s => string.Equals(s.ResourceId, resourceId, StringComparison.Ordinal)
                        && !string.Equals(s.Status, PlanningSessionStatus.Archived, StringComparison.Ordinal))
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt);
        var claimed = new HashSet<DateOnly>();
        foreach (var session in sessions)
        {
            if (await _previews.PreviewAsync(session, cancellationToken) is not { } preview)
            {
                continue;
            }

            var draftWeeks = (preview.Weeks ?? Array.Empty<PlanningWeekDto>())
                .Where(w => GetWorkspaceCalendarHandler.StateOf(w.Status) == WorkspaceWeekStates.Draft)
                .Select(w => DateOnly.Parse(w.WeekStart))
                .Where(claimed.Add)
                .ToHashSet();
            var counts = preview.Scheduled
                .Where(s => !s.IsFixed)
                .Select(s => DateOnly.Parse(s.PlannedDate))
                .Where(d => draftWeeks.Contains(PlanningWeekCalendar.MondayOf(d)))
                .GroupBy(d => d)
                .ToDictionary(g => g.Key, g => g.Count());
            foreach (var day in preview.Days ?? Array.Empty<PlanningDayPreview>())
            {
                var date = DateOnly.Parse(day.Date);
                if (draftWeeks.Contains(PlanningWeekCalendar.MondayOf(date)))
                {
                    load[date] = (day.PlannedMinutes, counts.GetValueOrDefault(date));
                }
            }
        }

        return load;
    }
}
