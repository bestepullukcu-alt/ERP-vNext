using System.Collections.ObjectModel;

namespace Diten.PlanningService.Application.Features.DemandPlanning;

// This component accepts verified weekly fixtures only. No import, forecast method or API is wired here.
public static class RollingOriginEvaluator
{
    public enum WeekState { Observed, Missing, Unknown }

    public enum EvaluationState { Complete, InsufficientHistory, IncompleteSeries }

    public enum MaseComparison { NonSeasonal, ApprovedAnnualSeasonal }

    public sealed record WeeklyPoint(DateOnly WeekStart, WeekState State, decimal? Quantity);

    public sealed record TestError(DateOnly WeekStart, int TrainingWeekCount,
        decimal Actual, decimal Forecast, decimal? MaseTrainingScale, int WeeksAhead)
    {
        public decimal SignedError => Actual - Forecast;
    }

    public sealed record HorizonMetrics(int WeeksAhead, int Count, decimal Mae,
        decimal? WapeRatio, decimal Bias, decimal? Mase)
    {
        public bool IsWapeUndefined => WapeRatio is null;

        public bool IsMaseUndefined => Mase is null;
    }

    public sealed record EvaluationResult(EvaluationState State,
        IReadOnlyList<TestError> Errors, decimal? Mae, decimal? WapeRatio,
        decimal? Bias, decimal? Mase, IReadOnlyList<HorizonMetrics> ByWeeksAhead)
    {
        public bool IsWapeUndefined => State == EvaluationState.Complete && WapeRatio is null;

        public bool IsMaseUndefined => State == EvaluationState.Complete && Mase is null;
    }

    public static EvaluationResult Evaluate(IReadOnlyList<WeeklyPoint> series,
        int minimumTrainingWeeks, int horizonWeeks,
        Func<IReadOnlyList<decimal>, int, IReadOnlyList<decimal>> forecast,
        MaseComparison maseComparison = MaseComparison.NonSeasonal)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(forecast);
        if (minimumTrainingWeeks < 26)
            throw new ArgumentOutOfRangeException(nameof(minimumTrainingWeeks));
        if (horizonWeeks < 1)
            throw new ArgumentOutOfRangeException(nameof(horizonWeeks));
        if (!Enum.IsDefined(maseComparison))
            throw new ArgumentOutOfRangeException(nameof(maseComparison));
        if (maseComparison == MaseComparison.ApprovedAnnualSeasonal && minimumTrainingWeeks < 104)
            throw new ArgumentOutOfRangeException(nameof(minimumTrainingWeeks),
                "Annual seasonal evaluation requires at least 104 training weeks.");

        for (var index = 0; index < series.Count; index++)
        {
            var point = series[index];
            if (index > 0 && point.WeekStart != series[index - 1].WeekStart.AddDays(7))
                return Unavailable(EvaluationState.IncompleteSeries);
            if (point.State != WeekState.Observed || point.Quantity is null)
                return Unavailable(EvaluationState.IncompleteSeries);
            if (point.Quantity < 0)
                throw new ArgumentOutOfRangeException(nameof(series), "Observed demand cannot be negative.");
        }

        if (series.Count < minimumTrainingWeeks + horizonWeeks)
            return Unavailable(EvaluationState.InsufficientHistory);

        var errors = new List<TestError>();
        decimal totalAbsoluteError = 0;
        decimal totalActual = 0;
        decimal totalSignedError = 0;
        decimal totalScaledAbsoluteError = 0;
        var isMaseDefined = true;
        var maseLagWeeks = maseComparison == MaseComparison.ApprovedAnnualSeasonal ? 52 : 1;
        for (var origin = minimumTrainingWeeks; origin + horizonWeeks <= series.Count; origin++)
        {
            // Only the prefix is handed to the forecasting callback; holdout weeks are read afterwards.
            var training = new ReadOnlyCollection<decimal>(series.Take(origin)
                .Select(point => point.Quantity!.Value).ToArray());
            decimal? maseTrainingScale = null;
            if (training.Count > maseLagWeeks)
            {
                decimal scaleErrorSum = 0;
                for (var index = maseLagWeeks; index < training.Count; index++)
                    scaleErrorSum += Math.Abs(training[index] - training[index - maseLagWeeks]);
                if (scaleErrorSum > 0)
                {
                    var scale = scaleErrorSum / (training.Count - maseLagWeeks);
                    if (scale > 0)
                        maseTrainingScale = scale;
                }
            }
            if (maseTrainingScale is null)
                isMaseDefined = false;

            var predictions = forecast(training, horizonWeeks);
            if (predictions is null || predictions.Count != horizonWeeks ||
                predictions.Any(prediction => prediction < 0))
                throw new InvalidOperationException("Forecast must return one nonnegative value per holdout week.");

            for (var offset = 0; offset < horizonWeeks; offset++)
            {
                var actualPoint = series[origin + offset];
                var actual = actualPoint.Quantity!.Value;
                var predicted = predictions[offset];
                var testError = new TestError(actualPoint.WeekStart, origin, actual,
                    predicted, maseTrainingScale, offset + 1);
                errors.Add(testError);
                var absoluteError = Math.Abs(testError.SignedError);
                totalAbsoluteError += absoluteError;
                totalActual += actual;
                totalSignedError += testError.SignedError;
                if (maseTrainingScale is not null)
                    totalScaledAbsoluteError += absoluteError / maseTrainingScale.Value;
            }
        }

        return new EvaluationResult(EvaluationState.Complete, errors.AsReadOnly(),
            totalAbsoluteError / errors.Count,
            totalActual == 0 ? null : totalAbsoluteError / totalActual,
            totalSignedError / errors.Count,
            isMaseDefined ? totalScaledAbsoluteError / errors.Count : null,
            BuildHorizonBreakdown(errors));
    }

    private static IReadOnlyList<HorizonMetrics> BuildHorizonBreakdown(IReadOnlyList<TestError> errors)
    {
        var byWeeksAhead = new List<HorizonMetrics>();
        foreach (var group in errors.GroupBy(error => error.WeeksAhead).OrderBy(group => group.Key))
        {
            decimal absoluteErrorSum = 0;
            decimal actualSum = 0;
            decimal signedErrorSum = 0;
            decimal scaledAbsoluteErrorSum = 0;
            var isMaseDefined = true;
            var count = 0;
            foreach (var error in group)
            {
                count++;
                var absoluteError = Math.Abs(error.SignedError);
                absoluteErrorSum += absoluteError;
                actualSum += error.Actual;
                signedErrorSum += error.SignedError;
                if (error.MaseTrainingScale is > 0)
                    scaledAbsoluteErrorSum += absoluteError / error.MaseTrainingScale.Value;
                else
                    isMaseDefined = false;
            }

            byWeeksAhead.Add(new HorizonMetrics(group.Key, count, absoluteErrorSum / count,
                actualSum == 0 ? null : absoluteErrorSum / actualSum,
                signedErrorSum / count,
                isMaseDefined ? scaledAbsoluteErrorSum / count : null));
        }

        return byWeeksAhead.AsReadOnly();
    }

    private static EvaluationResult Unavailable(EvaluationState state) =>
        new(state, Array.Empty<TestError>(), null, null, null, null,
            Array.Empty<HorizonMetrics>());
}
