using Diten.Domain.Aggregates.EnterpriseStrategy;

namespace Diten.Application.EnterpriseStrategy.Shared;

public static class EnterpriseStrategyLibraryFallbackStore
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, GoalTemplate> GoalTemplates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<GoalTemplateMetric>> GoalTemplateMetrics = new(StringComparer.OrdinalIgnoreCase);

    public static void UpsertGoalTemplates(IEnumerable<GoalTemplate> rows)
    {
        lock (Gate)
        {
            foreach (var row in rows ?? Array.Empty<GoalTemplate>())
            {
                if (row is null || string.IsNullOrWhiteSpace(row.Id)) continue;
                GoalTemplates[row.Id] = Clone(row);
            }
        }
    }

    public static void ReplaceGoalTemplateMetrics(string goalTemplateId, IEnumerable<GoalTemplateMetric> rows)
    {
        if (string.IsNullOrWhiteSpace(goalTemplateId)) return;
        lock (Gate)
        {
            GoalTemplateMetrics[goalTemplateId] = (rows ?? Array.Empty<GoalTemplateMetric>())
                .Where(x => x is not null)
                .Select(Clone)
                .ToList();
        }
    }

    public static IReadOnlyList<GoalTemplate> ListGoalTemplates()
    {
        lock (Gate)
        {
            return GoalTemplates.Values.Select(Clone).ToList();
        }
    }

    public static GoalTemplate? GetGoalTemplate(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        lock (Gate)
        {
            return GoalTemplates.TryGetValue(id, out var row) ? Clone(row) : null;
        }
    }

    public static IReadOnlyList<GoalTemplateMetric> ListGoalTemplateMetrics(string goalTemplateId)
    {
        if (string.IsNullOrWhiteSpace(goalTemplateId)) return Array.Empty<GoalTemplateMetric>();
        lock (Gate)
        {
            return GoalTemplateMetrics.TryGetValue(goalTemplateId, out var rows)
                ? rows.Select(Clone).ToList()
                : Array.Empty<GoalTemplateMetric>();
        }
    }

    private static GoalTemplate Clone(GoalTemplate row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Category = row.Category,
        Statement = row.Statement,
        Owner = row.Owner,
        Status = row.Status,
        PlanningHorizonStart = row.PlanningHorizonStart,
        PlanningHorizonEnd = row.PlanningHorizonEnd,
        Priority = row.Priority,
        EntityScope = row.EntityScope,
        DecisionReference = row.DecisionReference,
        EvidenceReference = row.EvidenceReference,
        ChangeLogRef = row.ChangeLogRef,
        Version = row.Version,
        LifecycleStatus = row.LifecycleStatus,
        Tags = row.Tags,
        YearlyBudgets = (row.YearlyBudgets ?? new()).Select(x => new GoalYearlyBudgetEnvelope
        {
            Year = x.Year,
            RevenueTarget = x.RevenueTarget,
            EbitdaTarget = x.EbitdaTarget,
            CapexEnvelope = x.CapexEnvelope,
            OpexEnvelope = x.OpexEnvelope,
            SavingsTarget = x.SavingsTarget,
            FundingPoolEnvelope = x.FundingPoolEnvelope,
            Commentary = x.Commentary
        }).ToList(),
        CreatedAt = row.CreatedAt,
        UpdatedAt = row.UpdatedAt,
        CreatedBy = row.CreatedBy,
        UpdatedBy = row.UpdatedBy
    };

    private static GoalTemplateMetric Clone(GoalTemplateMetric row) => new()
    {
        Id = row.Id,
        GoalTemplateId = row.GoalTemplateId,
        MetricName = row.MetricName,
        MetricType = row.MetricType,
        BaselineValue = row.BaselineValue,
        TargetValue = row.TargetValue,
        UnitOfMeasure = row.UnitOfMeasure,
        AggregationMethod = row.AggregationMethod,
        CascadeMetric = row.CascadeMetric,
        MetricOrigin = row.MetricOrigin,
        MetricRole = row.MetricRole,
        RestrictionMode = row.RestrictionMode,
        RollupEligible = row.RollupEligible,
        YearlyTargets = (row.YearlyTargets ?? new()).Select(x => new GoalMetricYearValue
        {
            Year = x.Year,
            BaselineValue = x.BaselineValue,
            TargetValue = x.TargetValue,
            ActualValue = x.ActualValue,
            ForecastValue = x.ForecastValue,
            ThresholdMin = x.ThresholdMin,
            ThresholdMax = x.ThresholdMax,
            Commentary = x.Commentary,
            ThresholdCommentary = x.ThresholdCommentary
        }).ToList()
    };
}
