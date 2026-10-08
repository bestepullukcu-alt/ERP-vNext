using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

// Builds only a local draft from an already scoped cycle and verified reference fixture.
// It does not infer history sufficiency, grant permission, persist audit, or publish.
public static class ManualDraftFactory
{
    public static Response<DemandRevisionDraft> Create(
        PlanningCycle cycle, Guid tenantId, Guid legalEntityId, Guid actorId,
        string reason, DateTimeOffset occurredAt,
        IReadOnlyList<VerifiedDraftSeries> selectedSeries)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        ArgumentNullException.ThrowIfNull(selectedSeries);

        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty || actorId == Guid.Empty ||
            cycle.TenantId != tenantId || cycle.LegalEntityId != legalEntityId || cycle.IsDeleted)
            return Response<DemandRevisionDraft>.Fail("Planning cycle scope is unavailable.", 404);
        if (string.IsNullOrWhiteSpace(reason) || occurredAt == default)
            return Response<DemandRevisionDraft>.Fail("Manual draft reason, actor and time are required.", 422);
        if (!HasValidCycleSnapshot(cycle))
            return Response<DemandRevisionDraft>.Fail("A valid locked 52-week cycle is required.", 422);
        if (selectedSeries.Count == 0 || selectedSeries.Any(series =>
                series.SkuId == Guid.Empty || string.IsNullOrWhiteSpace(series.WarehouseId) ||
                string.IsNullOrWhiteSpace(series.BaseUomId) || series.Weeks is null ||
                series.Weeks.Count != 52) ||
            selectedSeries.Select(series => (series.SkuId, series.WarehouseId))
                .Distinct().Count() != selectedSeries.Count)
            return Response<DemandRevisionDraft>.Fail("Selected SKU and Warehouse series are invalid.", 422);

        var draftSeries = new List<DemandDraftSeries>(selectedSeries.Count);
        foreach (var series in selectedSeries)
        {
            var byNumber = series.Weeks.OrderBy(week => week.Number).ToArray();
            for (var index = 0; index < 52; index++)
            {
                var input = byNumber[index];
                var lockedWeek = cycle.Weeks[index];
                if (input.Number != lockedWeek.Number ||
                    input.WeekStart != lockedWeek.WeekStart ||
                    input.WeekEnd != lockedWeek.WeekEnd ||
                    !DemandRevisionDraft.IsValidValue(input.ValueKind, input.Quantity))
                    return Response<DemandRevisionDraft>.Fail(
                        "Manual values must match the locked weeks and distinguish known, missing and unknown.", 422);
            }

            draftSeries.Add(new DemandDraftSeries
            {
                SkuId = series.SkuId,
                WarehouseId = series.WarehouseId,
                BaseUomId = series.BaseUomId,
                Weeks = byNumber.Select(week => new DemandDraftWeek(
                    week.Number, week.WeekStart, week.WeekEnd,
                    week.ValueKind, week.Quantity, reason,
                    actorId, occurredAt)).ToList()
            });
        }

        var draft = new DemandRevisionDraft
        {
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            PlanningCycleId = cycle.Id,
            PlanningPeriodKey = cycle.PlanningPeriodKey,
            AsOfDate = cycle.AsOfDate,
            CalendarId = cycle.CalendarId,
            CalendarVersion = cycle.CalendarVersion,
            TimeZoneId = cycle.TimeZoneId,
            HorizonStart = cycle.HorizonStart,
            HorizonEnd = cycle.HorizonEnd,
            Weeks = cycle.Weeks.Select(week => new PlanningWeek
            {
                Number = week.Number,
                WeekStart = week.WeekStart,
                WeekEnd = week.WeekEnd
            }).ToList(),
            Series = draftSeries,
            CreatedBy = actorId,
            CreatedAt = occurredAt,
            CreationReason = reason.Trim()
        };
        return Response<DemandRevisionDraft>.Success(draft, 201);
    }

    private static bool HasValidCycleSnapshot(PlanningCycle cycle)
    {
        if (cycle.Id == Guid.Empty || cycle.Weeks.Count != 52 ||
            string.IsNullOrWhiteSpace(cycle.CalendarId) ||
            string.IsNullOrWhiteSpace(cycle.CalendarVersion) ||
            string.IsNullOrWhiteSpace(cycle.TimeZoneId) ||
            cycle.HorizonStart != cycle.Weeks[0].WeekStart ||
            cycle.HorizonEnd != cycle.Weeks[^1].WeekEnd ||
            cycle.PlanningPeriodKey != cycle.HorizonStart.ToString("yyyy-MM-dd"))
            return false;

        for (var index = 0; index < 52; index++)
        {
            var week = cycle.Weeks[index];
            if (week.Number != index + 1 || week.WeekEnd != week.WeekStart.AddDays(6) ||
                index > 0 && week.WeekStart != cycle.Weeks[index - 1].WeekEnd.AddDays(1))
                return false;
        }
        return true;
    }
}
