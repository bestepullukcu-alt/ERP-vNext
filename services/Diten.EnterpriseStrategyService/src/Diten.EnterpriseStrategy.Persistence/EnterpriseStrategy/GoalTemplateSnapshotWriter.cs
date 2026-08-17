using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Repositories;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Domain.Aggregates.EnterpriseStrategy;

namespace Diten.Persistence.EnterpriseStrategy;

public sealed class GoalTemplateSnapshotWriter : IGoalTemplateSnapshotWriter
{
    private readonly IStrategyLibraryRepository _library;

    public GoalTemplateSnapshotWriter(IStrategyLibraryRepository library) => _library = library;

    public async Task<string?> WriteFromGoalAsync(GoalAggregate goal, GoalTemplateSaveMetadataDto metadata, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(metadata.TemplateName))
            return null;

        var id = "GT-" + Guid.NewGuid().ToString("N")[..12];
        var template = new GoalTemplate
        {
            Id = id,
            Name = metadata.TemplateName.Trim(),
            Category = GoalTemplateTypeCatalog.NormalizeOrDefault(goal.Category),
            Statement = goal.Statement,
            Owner = goal.Owner,
            Status = goal.Status,
            PlanningHorizonStart = goal.PlanningHorizonStart,
            PlanningHorizonEnd = goal.PlanningHorizonEnd,
            Priority = goal.Priority,
            EntityScope = goal.EntityScope,
            DecisionReference = goal.DecisionReference ?? string.Empty,
            EvidenceReference = goal.EvidenceReference ?? string.Empty,
            ChangeLogRef = goal.ChangeLogRef ?? string.Empty,
            Version = 1,
            LifecycleStatus = metadata.PublishReady ? "Published" : "Draft",
            // Keep goal statement as-is, and preserve template-level metadata in tags.
            Tags = MergeTemplateTags(metadata.TemplateCategoryOrTags, metadata.TemplateDescription),
            YearlyBudgets = (goal.YearlyBudgets ?? new()).Select(x => new GoalYearlyBudgetEnvelope
            {
                Year = x.Year,
                RevenueTarget = x.RevenueTarget,
                EbitdaTarget = x.EbitdaTarget,
                CapexEnvelope = x.CapexEnvelope,
                OpexEnvelope = x.OpexEnvelope,
                SavingsTarget = x.SavingsTarget,
                FundingPoolEnvelope = x.FundingPoolEnvelope
            }).ToList(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = actor,
            UpdatedBy = actor
        };

        EnterpriseStrategyLibraryFallbackStore.UpsertGoalTemplates(new[] { template });

        await _library.UpsertGoalTemplatesAsync(new[] { template }, cancellationToken);

        var metrics = (goal.Metrics ?? new()).Select(m => new GoalTemplateMetric
        {
            Id = Guid.NewGuid().ToString("N"),
            GoalTemplateId = id,
            MetricName = m.MetricName,
            MetricType = m.MetricType,
            BaselineValue = m.BaselineValue,
            TargetValue = m.TargetValue,
            UnitOfMeasure = m.UnitOfMeasure,
            AggregationMethod = m.AggregationMethod,
            CascadeMetric = m.CascadeMetric,
            MetricOrigin = NormalizeMetricOriginForSnapshot(m.MetricOrigin),
            MetricRole = m.MetricRole,
            RestrictionMode = m.RestrictionMode,
            RollupEligible = m.RollupEligible,
            YearlyTargets = (m.YearlyTargets ?? new()).Select(y => new GoalMetricYearValue
            {
                Year = y.Year,
                TargetValue = y.TargetValue,
                ActualValue = y.ActualValue,
                ForecastValue = y.ForecastValue,
                ThresholdCommentary = y.ThresholdCommentary
            }).ToList()
        }).ToList();

        EnterpriseStrategyLibraryFallbackStore.ReplaceGoalTemplateMetrics(id, metrics);
        await _library.ReplaceGoalTemplateMetricsAsync(id, metrics, cancellationToken);
        return id;
    }

    private static string? MergeTemplateTags(string? categoryOrTags, string? description)
    {
        var parts = new[] { categoryOrTags, description }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .ToList();
        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    private static string NormalizeMetricOriginForSnapshot(string? origin)
    {
        var o = (origin ?? string.Empty).Trim();
        if (string.Equals(o, "Strategic", StringComparison.OrdinalIgnoreCase))
            return "Local";
        return string.IsNullOrWhiteSpace(o) ? "Local" : o;
    }
}
