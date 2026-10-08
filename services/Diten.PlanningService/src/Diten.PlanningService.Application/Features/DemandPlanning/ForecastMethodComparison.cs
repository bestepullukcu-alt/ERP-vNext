namespace Diten.PlanningService.Application.Features.DemandPlanning;

// This component displays precomputed fixture results; it does not select or run a method.
public static class ForecastMethodComparison
{
    public sealed record Candidate(string MethodName,
        RollingOriginEvaluator.EvaluationResult Evaluation);

    public sealed record CandidateMetrics(string MethodName,
        RollingOriginEvaluator.EvaluationState State,
        decimal? Mae, decimal? WapeRatio, decimal? Bias, decimal? Mase,
        IReadOnlyList<RollingOriginEvaluator.HorizonMetrics> ByWeeksAhead)
    {
        public bool IsWapeUndefined =>
            State == RollingOriginEvaluator.EvaluationState.Complete && WapeRatio is null;
    }

    public static IReadOnlyList<CandidateMetrics> SideBySide(IReadOnlyList<Candidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var rows = new List<CandidateMetrics>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (candidate is null || string.IsNullOrWhiteSpace(candidate.MethodName) ||
                candidate.Evaluation is null)
                throw new ArgumentException("Every candidate needs a name and evaluation.",
                    nameof(candidates));

            var evaluation = candidate.Evaluation;
            rows.Add(new CandidateMetrics(candidate.MethodName, evaluation.State,
                evaluation.Mae, evaluation.WapeRatio, evaluation.Bias, evaluation.Mase,
                Array.AsReadOnly(evaluation.ByWeeksAhead.ToArray())));
        }

        return rows.AsReadOnly();
    }
}
