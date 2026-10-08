namespace Diten.PlanningService.Application.Features.DemandPlanning;

// Isolated synthetic-series boundary. This is not an accepted-history ForecastRun.
internal static class FixtureForecastEngine
{
    // The caller must name the mathematical variant and every applicable parameter.
    // Version identifies this fixture method policy, separately from eligibility policy.
    internal sealed record MethodSpecification(
        ForecastMethodEligibility.Method Method, string Variant, string Version,
        int? WindowSize, decimal? Alpha, decimal? Beta, decimal? Gamma);

    internal interface IWeeklyMethod
    {
        ForecastMethodEligibility.Method Method { get; }
        IReadOnlyDictionary<string, string> Parameters { get; }
        IReadOnlyList<decimal> Forecast(IReadOnlyList<decimal> training, int horizon);
    }

    internal sealed record CandidateResult(ForecastMethodEligibility.SeriesKey Series,
        ForecastMethodEligibility.Method Method,
        string PolicyVersion, DateOnly AsOfWeekStart,
        IReadOnlyDictionary<string, string> Parameters,
        IReadOnlyList<decimal> Forecast52,
        RollingOriginEvaluator.EvaluationResult Evaluation);

    private static readonly IReadOnlyDictionary<ForecastMethodEligibility.Method, IWeeklyMethod>
        SupportedMethods = new Dictionary<ForecastMethodEligibility.Method, IWeeklyMethod>
        {
            [ForecastMethodEligibility.Method.Naive] = new NaiveMethod(),
            [ForecastMethodEligibility.Method.SeasonalNaive] = new SeasonalNaiveMethod()
        };

    internal static IReadOnlyList<CandidateResult> Compare(
        ForecastMethodEligibility.VerifiedHistoryFixture history,
        ForecastMethodEligibility.FixturePolicy policy,
        DateOnly asOfWeekStart,
        IReadOnlyList<ForecastMethodEligibility.Method> candidates)
        => Compare(history, policy, asOfWeekStart, candidates, []);

    internal static IReadOnlyList<CandidateResult> Compare(
        ForecastMethodEligibility.VerifiedHistoryFixture history,
        ForecastMethodEligibility.FixturePolicy policy,
        DateOnly asOfWeekStart,
        IReadOnlyList<ForecastMethodEligibility.Method> candidates,
        IReadOnlyList<MethodSpecification> specifications)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(specifications);
        if (history.Weeks.Count == 0 ||
            history.Weeks[^1].WeekStart.AddDays(7) != asOfWeekStart)
            throw new ArgumentException("The as-of boundary must follow complete fixture history.",
                nameof(asOfWeekStart));
        if (candidates.Count == 0 || candidates.Distinct().Count() != candidates.Count)
            throw new ArgumentException("Candidates must be distinct and nonempty.",
                nameof(candidates));
        if (specifications.Select(item => item.Method).Distinct().Count() != specifications.Count ||
            specifications.Any(item => !candidates.Contains(item.Method)))
            throw new ArgumentException("Method specifications must be unique and requested.",
                nameof(specifications));

        var rows = new List<CandidateResult>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var specification = specifications.SingleOrDefault(item => item.Method == candidate);
            var method = SupportedMethods.TryGetValue(candidate, out var existing)
                ? existing : CreateParameterizedMethod(candidate, specification);
            if (existing is not null && specification is not null)
                throw new ArgumentException("Parameterless methods do not accept a specification.",
                    nameof(specifications));
            if (method.Method != candidate)
                throw new InvalidOperationException("Forecast method registration is inconsistent.");
            var eligibility = ForecastMethodEligibility.Assess(history, candidate, policy);
            if (!eligibility.IsEligible)
                throw new InvalidOperationException($"Forecast method {candidate} is ineligible: {eligibility.Reason}.");

            var weeklyPoints = history.Weeks.Select(week =>
                new RollingOriginEvaluator.WeeklyPoint(week.WeekStart,
                    RollingOriginEvaluator.WeekState.Observed,
                    week.Events.Sum(demandEvent => demandEvent.Quantity))).ToArray();
            var observations = weeklyPoints.Select(point => point.Quantity!.Value).ToArray();
            var minimumTrainingWeeks = candidate switch
            {
                ForecastMethodEligibility.Method.SeasonalNaive or
                    ForecastMethodEligibility.Method.HoltWinters => policy.SeasonalWeeks,
                ForecastMethodEligibility.Method.BiasCorrectedCroston => policy.IntermittentWeeks,
                _ => policy.GeneralWeeks
            };
            if (candidate == ForecastMethodEligibility.Method.BiasCorrectedCroston)
            {
                var eventCount = 0;
                var positiveWeeks = 0;
                var firstQualifiedOrigin = -1;
                for (var weekIndex = 0; weekIndex < history.Weeks.Count; weekIndex++)
                {
                    var count = history.Weeks[weekIndex].Events.Count;
                    eventCount += count;
                    if (count > 0) positiveWeeks++;
                    if (eventCount >= policy.IntermittentDemandEvents && positiveWeeks >= 2)
                    {
                        firstQualifiedOrigin = weekIndex + 1;
                        break;
                    }
                }
                if (firstQualifiedOrigin < 0)
                    throw new InvalidOperationException(
                        "Croston requires two separate nonzero weeks as well as the event threshold.");
                minimumTrainingWeeks = Math.Max(minimumTrainingWeeks, firstQualifiedOrigin);
            }
            var maseComparison = candidate is
                ForecastMethodEligibility.Method.SeasonalNaive or ForecastMethodEligibility.Method.HoltWinters
                ? RollingOriginEvaluator.MaseComparison.ApprovedAnnualSeasonal
                : RollingOriginEvaluator.MaseComparison.NonSeasonal;
            var evaluation = RollingOriginEvaluator.Evaluate(weeklyPoints,
                minimumTrainingWeeks, 52, method.Forecast, maseComparison);
            var forecast = method.Forecast(observations, 52);
            rows.Add(new CandidateResult(history.Series, candidate, policy.Version, asOfWeekStart,
                method.Parameters,
                Array.AsReadOnly(forecast.ToArray()), evaluation));
        }

        return rows.AsReadOnly();
    }

    private static IWeeklyMethod CreateParameterizedMethod(
        ForecastMethodEligibility.Method method, MethodSpecification? specification)
    {
        if (method is not (ForecastMethodEligibility.Method.MovingAverage or
            ForecastMethodEligibility.Method.SimpleExponentialSmoothing or
            ForecastMethodEligibility.Method.HoltTrend or
            ForecastMethodEligibility.Method.HoltWinters or
            ForecastMethodEligibility.Method.BiasCorrectedCroston))
            throw new NotSupportedException($"Forecast method {method} has no approved variant.");
        if (specification is null || string.IsNullOrWhiteSpace(specification.Version))
            throw new ArgumentException($"Forecast method {method} requires a versioned specification.");
        return method switch
        {
            ForecastMethodEligibility.Method.MovingAverage =>
                new MovingAverageMethod(specification),
            ForecastMethodEligibility.Method.SimpleExponentialSmoothing =>
                new SimpleExponentialSmoothingMethod(specification),
            ForecastMethodEligibility.Method.HoltTrend => new HoltMethod(specification),
            ForecastMethodEligibility.Method.HoltWinters => new AdditiveHoltWintersMethod(specification),
            ForecastMethodEligibility.Method.BiasCorrectedCroston => new SbaCrostonMethod(specification),
            _ => throw new NotSupportedException()
        };
    }

    internal static IReadOnlyList<ForecastMethodComparison.CandidateMetrics> SideBySide(
        IReadOnlyList<CandidateResult> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return ForecastMethodComparison.SideBySide(candidates.Select(candidate =>
            new ForecastMethodComparison.Candidate(candidate.Method.ToString(),
                candidate.Evaluation)).ToArray());
    }

    private sealed class NaiveMethod : IWeeklyMethod
    {
        public ForecastMethodEligibility.Method Method => ForecastMethodEligibility.Method.Naive;
        public IReadOnlyDictionary<string, string> Parameters { get; } =
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>());

        public IReadOnlyList<decimal> Forecast(IReadOnlyList<decimal> training, int horizon)
        {
            if (training.Count == 0 || horizon < 1)
                throw new ArgumentException("Naive forecast needs observations and a horizon.");
            return Array.AsReadOnly(Enumerable.Repeat(training[^1], horizon).ToArray());
        }
    }

    private sealed class SeasonalNaiveMethod : IWeeklyMethod
    {
        public ForecastMethodEligibility.Method Method => ForecastMethodEligibility.Method.SeasonalNaive;
        public IReadOnlyDictionary<string, string> Parameters { get; } =
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
                new Dictionary<string, string> { ["annualLagWeeks"] = "52" });

        public IReadOnlyList<decimal> Forecast(IReadOnlyList<decimal> training, int horizon)
        {
            const int annualWeeks = 52;
            if (training.Count < annualWeeks || horizon < 1)
                throw new ArgumentException("Seasonal naive needs a complete annual cycle and a horizon.");
            var output = new decimal[horizon];
            for (var offset = 0; offset < horizon; offset++)
                output[offset] = training[training.Count - annualWeeks + offset % annualWeeks];
            return Array.AsReadOnly(output);
        }
    }

    private static IReadOnlyDictionary<string, string> Describe(MethodSpecification specification,
        params (string Key, string Value)[] parameters)
    {
        var values = new Dictionary<string, string>
        {
            ["variant"] = specification.Variant,
            ["methodPolicyVersion"] = specification.Version
        };
        foreach (var (key, value) in parameters)
            values.Add(key, value);
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(values);
    }

    private static void RequireCoefficient(decimal? value, string name)
    {
        if (value is null or <= 0m or > 1m)
            throw new ArgumentOutOfRangeException(name, "Coefficient must be greater than zero and at most one.");
    }

    private static void RequireNonnegativeTraining(IReadOnlyList<decimal> training, int horizon)
    {
        if (training.Count == 0 || horizon < 1 || training.Any(value => value < 0))
            throw new ArgumentException("Forecast requires nonnegative observations and a positive horizon.");
    }

    // NIST trailing-average one-step rule extended as a frozen-level, direct multi-step forecast.
    // Forecasts are never fed back into the moving window.
    private sealed class MovingAverageMethod : IWeeklyMethod
    {
        private readonly int _window;
        public ForecastMethodEligibility.Method Method => ForecastMethodEligibility.Method.MovingAverage;
        public IReadOnlyDictionary<string, string> Parameters { get; }

        public MovingAverageMethod(MethodSpecification specification)
        {
            if (specification.Variant != "trailing-flat" ||
                specification.WindowSize is null or < 1 ||
                specification.Alpha is not null || specification.Beta is not null ||
                specification.Gamma is not null)
                throw new ArgumentException("Moving Average requires trailing-flat and an explicit window only.");
            _window = specification.WindowSize.Value;
            Parameters = Describe(specification, ("windowWeeks", _window.ToString(
                System.Globalization.CultureInfo.InvariantCulture)),
                ("initialization", "last-observed-window"),
                ("horizonRule", "freeze-last-observed-mean"));
        }

        public IReadOnlyList<decimal> Forecast(IReadOnlyList<decimal> training, int horizon)
        {
            RequireNonnegativeTraining(training, horizon);
            if (training.Count < _window)
                throw new ArgumentException("Training is shorter than the moving-average window.");
            var mean = training.Skip(training.Count - _window).Average();
            return Array.AsReadOnly(Enumerable.Repeat(mean, horizon).ToArray());
        }
    }

    // FPP3 simple exponential smoothing: initial level is the first observed demand.
    private sealed class SimpleExponentialSmoothingMethod : IWeeklyMethod
    {
        private readonly decimal _alpha;
        public ForecastMethodEligibility.Method Method =>
            ForecastMethodEligibility.Method.SimpleExponentialSmoothing;
        public IReadOnlyDictionary<string, string> Parameters { get; }

        public SimpleExponentialSmoothingMethod(MethodSpecification specification)
        {
            if (specification.Variant != "level-flat" ||
                specification.WindowSize is not null || specification.Beta is not null ||
                specification.Gamma is not null)
                throw new ArgumentException("SES requires level-flat and an explicit alpha only.");
            RequireCoefficient(specification.Alpha, nameof(specification.Alpha));
            _alpha = specification.Alpha!.Value;
            Parameters = Describe(specification, ("alpha", _alpha.ToString(
                System.Globalization.CultureInfo.InvariantCulture)),
                ("initialization", "first-observation"),
                ("horizonRule", "final-level-flat"));
        }

        public IReadOnlyList<decimal> Forecast(IReadOnlyList<decimal> training, int horizon)
        {
            RequireNonnegativeTraining(training, horizon);
            var level = training[0];
            for (var index = 1; index < training.Count; index++)
                level = _alpha * training[index] + (1m - _alpha) * level;
            return Array.AsReadOnly(Enumerable.Repeat(level, horizon).ToArray());
        }
    }

    // FPP3 additive, undamped Holt with explicit alpha and beta-star.
    private sealed class HoltMethod : IWeeklyMethod
    {
        private readonly decimal _alpha;
        private readonly decimal _betaStar;
        public ForecastMethodEligibility.Method Method => ForecastMethodEligibility.Method.HoltTrend;
        public IReadOnlyDictionary<string, string> Parameters { get; }

        public HoltMethod(MethodSpecification specification)
        {
            if (specification.Variant != "additive-undamped" ||
                specification.WindowSize is not null || specification.Gamma is not null)
                throw new ArgumentException("Holt requires additive-undamped and explicit alpha/beta-star.");
            RequireCoefficient(specification.Alpha, nameof(specification.Alpha));
            RequireCoefficient(specification.Beta, nameof(specification.Beta));
            _alpha = specification.Alpha!.Value;
            _betaStar = specification.Beta!.Value;
            Parameters = Describe(specification,
                ("alpha", _alpha.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("betaStar", _betaStar.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("initialization", "first-level-first-difference"),
                ("horizonRule", "final-level-plus-h-times-final-trend"));
        }

        public IReadOnlyList<decimal> Forecast(IReadOnlyList<decimal> training, int horizon)
        {
            RequireNonnegativeTraining(training, horizon);
            if (training.Count < 2)
                throw new ArgumentException("Holt requires two observations for initial trend.");
            var level = training[0];
            var trend = training[1] - training[0];
            for (var index = 1; index < training.Count; index++)
            {
                var nextLevel = _alpha * training[index] +
                    (1m - _alpha) * (level + trend);
                var nextTrend = _betaStar * (nextLevel - level) +
                    (1m - _betaStar) * trend;
                level = nextLevel;
                trend = nextTrend;
            }
            var forecast = Enumerable.Range(1, horizon)
                .Select(week => level + week * trend).ToArray();
            if (forecast.Any(value => value < 0m))
                throw new InvalidOperationException("Holt projects negative demand; no silent clipping or fallback.");
            return Array.AsReadOnly(forecast);
        }
    }

    // FPP3 additive Holt-Winters component equations. The deterministic fixture
    // initializer uses two full years: second-year mean as level, difference of
    // yearly means / 52 as weekly trend, and centered second-year seasonal terms.
    private sealed class AdditiveHoltWintersMethod : IWeeklyMethod
    {
        private const int SeasonWeeks = 52;
        private readonly decimal _alpha;
        private readonly decimal _betaStar;
        private readonly decimal _gamma;
        public ForecastMethodEligibility.Method Method => ForecastMethodEligibility.Method.HoltWinters;
        public IReadOnlyDictionary<string, string> Parameters { get; }

        public AdditiveHoltWintersMethod(MethodSpecification specification)
        {
            if (specification.Variant != "additive-52-undamped" ||
                specification.WindowSize is not null)
                throw new ArgumentException("Holt-Winters requires additive-52-undamped and explicit coefficients.");
            RequireCoefficient(specification.Alpha, nameof(specification.Alpha));
            RequireCoefficient(specification.Beta, nameof(specification.Beta));
            if (specification.Gamma is null or < 0m ||
                specification.Gamma > 1m - specification.Alpha!.Value)
                throw new ArgumentOutOfRangeException(nameof(specification.Gamma),
                    "Additive gamma must be between zero and one minus alpha.");
            _alpha = specification.Alpha.Value;
            _betaStar = specification.Beta!.Value;
            _gamma = specification.Gamma.Value;
            Parameters = Describe(specification,
                ("alpha", Format(_alpha)), ("betaStar", Format(_betaStar)),
                ("gamma", Format(_gamma)), ("seasonWeeks", "52"),
                ("initialization", "second-year-mean-yearly-mean-slope-centered-second-year-season"),
                ("horizonRule", "final-level-plus-h-trend-plus-last-season"));
        }

        public IReadOnlyList<decimal> Forecast(IReadOnlyList<decimal> training, int horizon)
        {
            RequireNonnegativeTraining(training, horizon);
            if (training.Count < 2 * SeasonWeeks)
                throw new ArgumentException("Additive Holt-Winters requires two full annual seasons.");
            var firstMean = training.Take(SeasonWeeks).Average();
            var secondMean = training.Skip(SeasonWeeks).Take(SeasonWeeks).Average();
            var level = secondMean;
            var trend = (secondMean - firstMean) / SeasonWeeks;
            var seasons = Enumerable.Range(0, SeasonWeeks)
                .Select(week => training[SeasonWeeks + week] - secondMean).ToArray();
            for (var index = 2 * SeasonWeeks; index < training.Count; index++)
            {
                var seasonIndex = index % SeasonWeeks;
                var priorSeason = seasons[seasonIndex];
                var priorBase = level + trend;
                var nextLevel = _alpha * (training[index] - priorSeason) +
                    (1m - _alpha) * priorBase;
                var nextTrend = _betaStar * (nextLevel - level) +
                    (1m - _betaStar) * trend;
                seasons[seasonIndex] = _gamma * (training[index] - priorBase) +
                    (1m - _gamma) * priorSeason;
                level = nextLevel;
                trend = nextTrend;
            }
            var forecast = Enumerable.Range(1, horizon).Select(week =>
                level + week * trend + seasons[(training.Count + week - 1) % SeasonWeeks]).ToArray();
            if (forecast.Any(value => value < 0m))
                throw new InvalidOperationException(
                    "Additive Holt-Winters projects negative demand; no silent clipping or fallback.");
            return Array.AsReadOnly(forecast);
        }
    }

    // Hyndman's Croston reference: first nonzero size and first inclusive
    // interval initialize two SES states; SBA applies 1 - alpha_interval / 2.
    private sealed class SbaCrostonMethod : IWeeklyMethod
    {
        private readonly decimal _demandAlpha;
        private readonly decimal _intervalAlpha;
        public ForecastMethodEligibility.Method Method =>
            ForecastMethodEligibility.Method.BiasCorrectedCroston;
        public IReadOnlyDictionary<string, string> Parameters { get; }

        public SbaCrostonMethod(MethodSpecification specification)
        {
            if (specification.Variant != "sba-naive-initialization" ||
                specification.WindowSize is not null || specification.Gamma is not null)
                throw new ArgumentException("Croston requires SBA and explicit demand/interval alphas.");
            RequireCoefficient(specification.Alpha, nameof(specification.Alpha));
            RequireCoefficient(specification.Beta, nameof(specification.Beta));
            _demandAlpha = specification.Alpha!.Value;
            _intervalAlpha = specification.Beta!.Value;
            Parameters = Describe(specification,
                ("demandAlpha", Format(_demandAlpha)),
                ("intervalAlpha", Format(_intervalAlpha)),
                ("initialization", "first-nonzero-size-first-inclusive-interval"),
                ("correction", "one-minus-interval-alpha-over-two"),
                ("horizonRule", "corrected-size-over-interval-flat"));
        }

        public IReadOnlyList<decimal> Forecast(IReadOnlyList<decimal> training, int horizon)
        {
            RequireNonnegativeTraining(training, horizon);
            var firstIndex = -1;
            for (var index = 0; index < training.Count; index++)
            {
                if (training[index] > 0m) { firstIndex = index; break; }
            }
            if (firstIndex < 0)
                throw new InvalidOperationException("Croston requires a real positive demand event.");
            var size = training[firstIndex];
            var interval = (decimal)(firstIndex + 1);
            var lastEventIndex = firstIndex;
            var positiveWeeks = 1;
            for (var index = firstIndex + 1; index < training.Count; index++)
            {
                if (training[index] == 0m) continue;
                positiveWeeks++;
                size = _demandAlpha * training[index] + (1m - _demandAlpha) * size;
                interval = _intervalAlpha * (index - lastEventIndex) +
                    (1m - _intervalAlpha) * interval;
                lastEventIndex = index;
            }
            if (positiveWeeks < 2)
                throw new InvalidOperationException("Croston requires at least two nonzero weeks.");
            var estimate = (1m - _intervalAlpha / 2m) * size / interval;
            if (estimate < 0m)
                throw new InvalidOperationException("Croston produced negative demand.");
            return Array.AsReadOnly(Enumerable.Repeat(estimate, horizon).ToArray());
        }
    }

    private static string Format(decimal value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
