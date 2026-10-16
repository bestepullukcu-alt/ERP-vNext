namespace Diten.PlanningService.Application.Features.DemandPlanning;

// Ali-approved initial MVP policy. This immutable version is selected by the server,
// never supplied by a forecast request. A policy-edit workflow is a separate scope.
internal static class ApprovedForecastPolicy
{
    internal const string Version = "MOD0188-MVP4-2026-10-14-v1";
    internal static ForecastMethodEligibility.FixturePolicy Eligibility =>
        ForecastMethodEligibility.FixturePolicy.Mvp(Version);

    internal static FixtureForecastEngine.MethodSpecification? Specification(
        ForecastMethodEligibility.Method method) => method switch
    {
        ForecastMethodEligibility.Method.Naive => null,
        ForecastMethodEligibility.Method.MovingAverage => new(method,
            "trailing-flat", Version, 4, null, null, null),
        ForecastMethodEligibility.Method.SimpleExponentialSmoothing => new(method,
            "level-flat", Version, null, 0.2m, null, null),
        ForecastMethodEligibility.Method.HoltTrend => new(method,
            "additive-undamped", Version, null, 0.4m, 0.3m, null),
        ForecastMethodEligibility.Method.SeasonalNaive => null,
        ForecastMethodEligibility.Method.HoltWinters => new(method,
            "additive-52-undamped", Version, null, 0.5m, 0.5m, 0.2m),
        ForecastMethodEligibility.Method.BiasCorrectedCroston => new(method,
            "sba-naive-initialization", Version, null, 0.2m, 0.2m, null),
        _ => throw new ArgumentOutOfRangeException(nameof(method))
    };
}