namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public enum AcceptedHistoryRoute
{
    ForecastCandidate,
    ManualDraftRequired,
    DataReviewRequired
}

public sealed record AcceptedHistoryReadiness(AcceptedHistoryRoute Route,
    int ReliableWeeks, string? UomId, bool UnitVerificationPending);

public static class AcceptedHistoryForecastReadiness
{
    private const int MinimumReliableWeeks = 26;

    public static AcceptedHistoryReadiness Assess(AcceptedSeriesHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);
        var reliable = history.Weeks.Where(week => week.State is
            AcceptedWeekState.VerifiedDemand or AcceptedWeekState.VerifiedZeroDemand).ToArray();
        if (reliable.Length == 0)
            return new(AcceptedHistoryRoute.ManualDraftRequired, 0, null, false);
        var units = reliable.Select(week => week.UomId).Distinct(StringComparer.Ordinal).ToArray();
        if (units.Length != 1 || string.IsNullOrWhiteSpace(units[0]))
            return new(AcceptedHistoryRoute.DataReviewRequired, reliable.Length, null, false);

        var pending = reliable.Any(week => week.UnitVerificationPending);
        if (reliable.Length < MinimumReliableWeeks)
            return new(AcceptedHistoryRoute.ManualDraftRequired, reliable.Length, units[0], pending);
        if (reliable.Length != history.Weeks.Count)
            return new(AcceptedHistoryRoute.DataReviewRequired, reliable.Length, units[0], pending);
        return new(AcceptedHistoryRoute.ForecastCandidate, reliable.Length, units[0], pending);
    }
}
