using Diten.PlanningService.Domain.Features.DemandPlanning;

namespace Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;

public sealed record ForecastCandidateView(string Method, bool Eligible, string Reason,
    string PolicyVersion, int ReliableWeeks, int RealDemandEvents,
    decimal? WapeRatio, decimal? Mae, decimal? Bias, decimal? Mase,
    IReadOnlyList<RollingOriginEvaluator.HorizonMetrics> ByWeeksAhead,
    string Variant, IReadOnlyDictionary<string, string> Parameters,
    string? ForecastUomId, bool UnitVerificationPending);

internal sealed record EvaluatedForecastCandidate(ForecastCandidateView View,
    FixtureForecastEngine.CandidateResult? Result, AcceptedSeriesHistory? History);

// One server-side snapshot is read for all candidate methods. The newest verified
// suffix is used; an untrusted week never becomes zero and never crosses a gap.
public sealed class AcceptedHistoryForecastEvaluation(IAcceptedWeeklyHistoryReader reader)
{
    internal async Task<IReadOnlyList<EvaluatedForecastCandidate>> EvaluateAsync(
        PlanningCycle cycle, Guid skuId, string skuLevel, string warehouseId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        if (cycle.TenantId == Guid.Empty || cycle.LegalEntityId == Guid.Empty ||
            cycle.Id == Guid.Empty || skuId == Guid.Empty ||
            skuLevel is not ("Gsku" or "Lsku" or "FinishedGood") ||
            string.IsNullOrWhiteSpace(warehouseId) || cycle.IsDeleted)
            throw new ArgumentException("Scoped cycle and series are required.");

        var history = await reader.ReadAsync(cycle.TenantId, cycle.LegalEntityId,
            skuId, skuLevel, warehouseId, cycle.HorizonStart, 156, cancellationToken);
        if (history.TenantId != cycle.TenantId ||
            history.LegalEntityId != cycle.LegalEntityId ||
            history.SkuId != skuId || history.SkuLevel != skuLevel ||
            history.WarehouseId != warehouseId ||
            history.AsOfWeekStart != cycle.HorizonStart || history.Weeks.Count != 156 ||
            history.Weeks[^1].WeekStart.AddDays(7) != cycle.HorizonStart)
            throw new InvalidDataException("Accepted history snapshot mismatches cycle or series.");

        var suffix = history.Weeks.Reverse().TakeWhile(week =>
            week.State is AcceptedWeekState.VerifiedDemand or AcceptedWeekState.VerifiedZeroDemand)
            .Reverse().ToArray();
        // Different units cannot be aggregated or silently trimmed into a
        // selectable history. A pending MOD-0290 conversion is visible instead.
        var unit = suffix.LastOrDefault()?.UomId;
        if (suffix.Length > 0 && (string.IsNullOrWhiteSpace(unit) ||
            suffix.Any(week => week.UomId != unit)))
            return Enum.GetValues<ForecastMethodEligibility.Method>()
                .Select(method => Rejected(method, "MixedOrMissingUnit",
                    suffix.Length, suffix.Sum(week => week.Events.Count))).ToArray();

        var results = new List<EvaluatedForecastCandidate>();
        foreach (var method in Enum.GetValues<ForecastMethodEligibility.Method>())
        {
            var minimum = method is ForecastMethodEligibility.Method.SeasonalNaive or
                ForecastMethodEligibility.Method.HoltWinters ? 104 : 26;
            var window = suffix.TakeLast(minimum).ToArray();
            if (window.Length < minimum)
            {
                results.Add(Rejected(method, "InsufficientReliableWeeks", suffix.Length, 0));
                continue;
            }
            var events = window.Sum(week => week.Events.Count);
            if (method == ForecastMethodEligibility.Method.BiasCorrectedCroston &&
                (events < 4 || window.Count(week => week.Events.Count > 0) < 2))
            {
                results.Add(Rejected(method, "InsufficientRealDemandEvents", window.Length, events));
                continue;
            }
            var usable = new AcceptedSeriesHistory(history.TenantId, history.LegalEntityId,
                history.SkuId, history.SkuLevel, history.WarehouseId,
                history.AsOfWeekStart, suffix);
            if (AcceptedHistoryForecastReadiness.Assess(usable).Route !=
                AcceptedHistoryRoute.ForecastCandidate)
            {
                results.Add(Rejected(method, "UntrustedOrMixedUnitHistory", suffix.Length, events));
                continue;
            }
            var fixture = new ForecastMethodEligibility.VerifiedHistoryFixture(
                new ForecastMethodEligibility.SeriesKey(history.TenantId,
                    history.LegalEntityId, history.SkuId, history.WarehouseId),
                suffix.Select(week => new ForecastMethodEligibility.FixtureWeek(
                    week.WeekStart,
                    week.State == AcceptedWeekState.VerifiedDemand ?
                        ForecastMethodEligibility.WeekEvidence.VerifiedDemand :
                        ForecastMethodEligibility.WeekEvidence.VerifiedZeroDemand,
                    week.Events.Select(item => new ForecastMethodEligibility.FixtureDemandEvent(
                        item.AcceptedRowId,
                        new ForecastMethodEligibility.SeriesKey(history.TenantId,
                            history.LegalEntityId, history.SkuId, history.WarehouseId),
                        item.OccurredOn, item.Quantity)).ToArray())).ToArray());
            try
            {
                var specification = ApprovedForecastPolicy.Specification(method);
                var candidate = FixtureForecastEngine.Compare(fixture,
                    ApprovedForecastPolicy.Eligibility, cycle.HorizonStart, [method],
                    specification is null ? [] : [specification]).Single();
                var metrics = candidate.Evaluation;
                results.Add(new(new(method.ToString(), true,
                    metrics.State == RollingOriginEvaluator.EvaluationState.Complete ?
                        "Eligible" : "EligibleBacktestUnavailable",
                    ApprovedForecastPolicy.Version, suffix.Length, events,
                    metrics.WapeRatio, metrics.Mae, metrics.Bias, metrics.Mase,
                    metrics.ByWeeksAhead,
                    specification?.Variant ?? (method == ForecastMethodEligibility.Method.SeasonalNaive
                        ? "annual-lag-52" : "last-value"),
                    candidate.Parameters, unit,
                    suffix.Any(week => week.UnitVerificationPending)), candidate, usable));
            }
            catch (InvalidOperationException)
            {
                // A method producing negative or undefined output is not selectable.
                results.Add(Rejected(method, "InvalidForecastOutput", suffix.Length, events));
            }
        }
        return results.AsReadOnly();
    }

    private static EvaluatedForecastCandidate Rejected(
        ForecastMethodEligibility.Method method, string reason, int weeks, int events) =>
        new(new(method.ToString(), false, reason, ApprovedForecastPolicy.Version,
            weeks, events, null, null, null, null,
            Array.Empty<RollingOriginEvaluator.HorizonMetrics>(),
            ApprovedForecastPolicy.Specification(method)?.Variant ??
                (method == ForecastMethodEligibility.Method.SeasonalNaive
                    ? "annual-lag-52" : "last-value"),
            new Dictionary<string, string>(), null, false), null, null);
}
