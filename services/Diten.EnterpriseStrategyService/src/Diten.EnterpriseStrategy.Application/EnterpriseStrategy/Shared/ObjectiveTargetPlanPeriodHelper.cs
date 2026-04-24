namespace Diten.Application.EnterpriseStrategy.Shared;

internal sealed class ObjectiveTargetPlanPeriodDefinition
{
    public string PeriodKey { get; init; } = string.Empty;
    public string PeriodLabel { get; init; } = string.Empty;
    public DateTime PeriodStart { get; init; }
    public DateTime PeriodEnd { get; init; }
    public int Year { get; init; }
    public int SortOrder { get; init; }
}

internal static class ObjectiveTargetPlanPeriodHelper
{
    public const string GranularityYearly = "Yearly";
    public const string GranularityQuarterly = "Quarterly";
    public const string GranularityMonthly = "Monthly";
    public const string GranularityTotalStrategyPeriod = "TotalStrategyPeriod";

    public static string NormalizeGranularity(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", string.Empty);
        return normalized switch
        {
            "quarterly" => GranularityQuarterly,
            "monthly" => GranularityMonthly,
            "totalstrategyperiod" => GranularityTotalStrategyPeriod,
            "totalperiod" => GranularityTotalStrategyPeriod,
            "total" => GranularityTotalStrategyPeriod,
            _ => GranularityYearly
        };
    }

    public static IReadOnlyList<ObjectiveTargetPlanPeriodDefinition> BuildPeriods(DateTime? start, DateTime? end, string? granularity)
    {
        if (!start.HasValue || !end.HasValue)
            return Array.Empty<ObjectiveTargetPlanPeriodDefinition>();

        var normalizedGranularity = NormalizeGranularity(granularity);
        var effectiveStart = start.Value.Date;
        var effectiveEnd = end.Value.Date;
        if (effectiveEnd < effectiveStart)
            return Array.Empty<ObjectiveTargetPlanPeriodDefinition>();

        var periods = new List<ObjectiveTargetPlanPeriodDefinition>();
        var sortOrder = 0;

        if (normalizedGranularity == GranularityTotalStrategyPeriod)
        {
            periods.Add(new ObjectiveTargetPlanPeriodDefinition
            {
                PeriodKey = $"{effectiveStart:yyyyMMdd}-{effectiveEnd:yyyyMMdd}",
                PeriodLabel = "Total Strategy Period",
                PeriodStart = effectiveStart,
                PeriodEnd = effectiveEnd,
                Year = effectiveStart.Year,
                SortOrder = sortOrder
            });
            return periods;
        }

        if (normalizedGranularity == GranularityMonthly)
        {
            var cursor = new DateTime(effectiveStart.Year, effectiveStart.Month, 1);
            while (cursor <= effectiveEnd)
            {
                var monthStart = cursor < effectiveStart ? effectiveStart : cursor;
                var monthEnd = cursor.AddMonths(1).AddDays(-1);
                if (monthEnd > effectiveEnd)
                    monthEnd = effectiveEnd;

                periods.Add(new ObjectiveTargetPlanPeriodDefinition
                {
                    PeriodKey = $"{cursor:yyyy-MM}",
                    PeriodLabel = cursor.ToString("yyyy-MMM"),
                    PeriodStart = monthStart,
                    PeriodEnd = monthEnd,
                    Year = cursor.Year,
                    SortOrder = sortOrder++
                });

                cursor = cursor.AddMonths(1);
            }

            return periods;
        }

        if (normalizedGranularity == GranularityQuarterly)
        {
            var quarterMonth = (((effectiveStart.Month - 1) / 3) * 3) + 1;
            var cursor = new DateTime(effectiveStart.Year, quarterMonth, 1);
            while (cursor <= effectiveEnd)
            {
                var quarter = ((cursor.Month - 1) / 3) + 1;
                var quarterStart = cursor < effectiveStart ? effectiveStart : cursor;
                var quarterEnd = cursor.AddMonths(3).AddDays(-1);
                if (quarterEnd > effectiveEnd)
                    quarterEnd = effectiveEnd;

                periods.Add(new ObjectiveTargetPlanPeriodDefinition
                {
                    PeriodKey = $"{cursor.Year}-Q{quarter}",
                    PeriodLabel = $"{cursor.Year}-Q{quarter}",
                    PeriodStart = quarterStart,
                    PeriodEnd = quarterEnd,
                    Year = cursor.Year,
                    SortOrder = sortOrder++
                });

                cursor = cursor.AddMonths(3);
            }

            return periods;
        }

        for (var year = effectiveStart.Year; year <= effectiveEnd.Year; year++)
        {
            var yearStart = new DateTime(year, 1, 1);
            var yearEnd = new DateTime(year, 12, 31);
            if (yearStart < effectiveStart)
                yearStart = effectiveStart;
            if (yearEnd > effectiveEnd)
                yearEnd = effectiveEnd;

            periods.Add(new ObjectiveTargetPlanPeriodDefinition
            {
                PeriodKey = year.ToString(),
                PeriodLabel = year.ToString(),
                PeriodStart = yearStart,
                PeriodEnd = yearEnd,
                Year = year,
                SortOrder = sortOrder++
            });
        }

        return periods;
    }
}
