using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Diten.PlanningService.Cycles.Tests")]

namespace Diten.PlanningService.Application.Features.DemandPlanning;

// Fixture-only boundary. Accepted history and an authoritative policy source are not wired yet.
internal static class ForecastMethodEligibility
{
    internal enum Method
    {
        Naive,
        MovingAverage,
        SimpleExponentialSmoothing,
        HoltTrend,
        SeasonalNaive,
        HoltWinters,
        BiasCorrectedCroston
    }

    internal enum WeekEvidence
    {
        VerifiedDemand,
        VerifiedZeroDemand,
        Missing,
        Unknown,
        StockoutUnresolved
    }

    internal enum EligibilityReason
    {
        Eligible,
        InsufficientReliableWeeks,
        IncompleteOrUntrustedHistory,
        InsufficientRealDemandEvents
    }

    internal sealed record SeriesKey(Guid TenantId, Guid LegalEntityId, Guid SkuId,
        string WarehouseId);

    internal sealed record FixtureDemandEvent(Guid SourceRecordId, SeriesKey Series,
        DateOnly OccurredOn, decimal Quantity);

    internal sealed record FixtureWeek(DateOnly WeekStart, WeekEvidence Evidence,
        IReadOnlyList<FixtureDemandEvent> Events);

    internal sealed class VerifiedHistoryFixture
    {
        private readonly IReadOnlyList<FixtureWeek> _weeks;

        internal SeriesKey Series { get; }
        internal IReadOnlyList<FixtureWeek> Weeks => _weeks;

        internal VerifiedHistoryFixture(SeriesKey series, IReadOnlyList<FixtureWeek> weeks)
        {
            ArgumentNullException.ThrowIfNull(series);
            ArgumentNullException.ThrowIfNull(weeks);
            if (series.TenantId == Guid.Empty || series.LegalEntityId == Guid.Empty ||
                series.SkuId == Guid.Empty || string.IsNullOrWhiteSpace(series.WarehouseId))
                throw new ArgumentException("The fixture needs a complete series scope.", nameof(series));

            var seenEvents = new HashSet<Guid>();
            var copiedWeeks = new List<FixtureWeek>(weeks.Count);
            for (var index = 0; index < weeks.Count; index++)
            {
                var week = weeks[index] ?? throw new ArgumentException("Null fixture week.", nameof(weeks));
                if (!Enum.IsDefined(week.Evidence) || week.Events is null)
                    throw new ArgumentException("Invalid fixture week.", nameof(weeks));
                if (index > 0 && week.WeekStart != weeks[index - 1].WeekStart.AddDays(7))
                    throw new ArgumentException("Fixture weeks must be consecutive.", nameof(weeks));

                var events = week.Events.ToArray();
                if (week.Evidence == WeekEvidence.VerifiedDemand && events.Length == 0 ||
                    week.Evidence != WeekEvidence.VerifiedDemand && events.Length != 0)
                    throw new ArgumentException("Week evidence and events disagree.", nameof(weeks));
                foreach (var demandEvent in events)
                {
                    if (demandEvent is null || demandEvent.SourceRecordId == Guid.Empty ||
                        demandEvent.Series != series || demandEvent.Quantity <= 0 ||
                        demandEvent.OccurredOn < week.WeekStart ||
                        demandEvent.OccurredOn >= week.WeekStart.AddDays(7) ||
                        !seenEvents.Add(demandEvent.SourceRecordId))
                        throw new ArgumentException("Untrusted or duplicate fixture demand event.",
                            nameof(weeks));
                }

                copiedWeeks.Add(new FixtureWeek(week.WeekStart, week.Evidence,
                    Array.AsReadOnly(events)));
            }

            Series = series;
            _weeks = Array.AsReadOnly(copiedWeeks.ToArray());
        }
    }

    internal sealed record FixturePolicy(string Version, int GeneralWeeks,
        int SeasonalWeeks, int IntermittentWeeks, int IntermittentDemandEvents)
    {
        internal static FixturePolicy Mvp(string version) => new(version, 26, 104, 26, 4);
    }

    internal sealed record Decision(SeriesKey Series, Method Method, bool IsEligible,
        EligibilityReason Reason, string PolicyVersion, int ReliableWeeks,
        int RealDemandEvents);

    internal static Decision Assess(VerifiedHistoryFixture history, Method method,
        FixturePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(policy);
        if (!Enum.IsDefined(method))
            throw new ArgumentOutOfRangeException(nameof(method));
        if (string.IsNullOrWhiteSpace(policy.Version) || policy.GeneralWeeks < 1 ||
            policy.SeasonalWeeks < 1 || policy.IntermittentWeeks < 1 ||
            policy.IntermittentDemandEvents < 1)
            throw new ArgumentException("Invalid fixture policy.", nameof(policy));

        var reliableWeeks = history.Weeks.Count(week =>
            week.Evidence is WeekEvidence.VerifiedDemand or WeekEvidence.VerifiedZeroDemand);
        var realEvents = history.Weeks.Sum(week => week.Events.Count);
        Decision Result(EligibilityReason reason) => new(history.Series, method,
            reason == EligibilityReason.Eligible, reason, policy.Version,
            reliableWeeks, realEvents);

        if (history.Weeks.Any(week => week.Evidence is
                WeekEvidence.Missing or WeekEvidence.Unknown or WeekEvidence.StockoutUnresolved))
            return Result(EligibilityReason.IncompleteOrUntrustedHistory);

        var minimumWeeks = method switch
        {
            Method.Naive or Method.MovingAverage or Method.SimpleExponentialSmoothing or
                Method.HoltTrend => policy.GeneralWeeks,
            Method.SeasonalNaive or Method.HoltWinters => policy.SeasonalWeeks,
            Method.BiasCorrectedCroston => policy.IntermittentWeeks,
            _ => throw new InvalidOperationException("Method category is unresolved.")
        };
        if (reliableWeeks < minimumWeeks)
            return Result(EligibilityReason.InsufficientReliableWeeks);
        if (method == Method.BiasCorrectedCroston &&
            realEvents < policy.IntermittentDemandEvents)
            return Result(EligibilityReason.InsufficientRealDemandEvents);

        return Result(EligibilityReason.Eligible);
    }
}
