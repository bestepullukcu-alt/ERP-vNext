using Diten.Application.Dtos.EnterpriseStrategy;

namespace Diten.Application.EnterpriseStrategy.Services;

public sealed class WorkbookGoalRowDto
{
    public string GoalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string? MetricId { get; set; }
    public string? MetricName { get; set; }
    public decimal? MetricBaseline { get; set; }
    public decimal? MetricTarget { get; set; }
}

public sealed class WorkbookObjectiveRowDto
{
    public string ObjectiveId { get; set; } = string.Empty;
    public string ParentGoalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string? MetricId { get; set; }
    public string? MetricName { get; set; }
    public decimal? MetricBaseline { get; set; }
    public decimal? MetricTarget { get; set; }
}

public interface IEnterpriseStrategyNormalizationService
{
    GoalDto NormalizeGoalRows(IReadOnlyList<WorkbookGoalRowDto> rows);
    ObjectiveDto NormalizeObjectiveRows(IReadOnlyList<WorkbookObjectiveRowDto> rows);
}

public sealed class EnterpriseStrategyNormalizationService : IEnterpriseStrategyNormalizationService
{
    public GoalDto NormalizeGoalRows(IReadOnlyList<WorkbookGoalRowDto> rows)
    {
        var first = rows.First();
        var metrics = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.MetricId))
            .GroupBy(x => x.MetricId!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new GoalMetricDto
            {
                Id = g.Key,
                GoalId = first.GoalId,
                MetricName = g.First().MetricName ?? string.Empty,
                BaselineValue = g.First().MetricBaseline ?? 0,
                TargetValue = g.First().MetricTarget ?? 0
            })
            .ToList();

        return new GoalDto
        {
            Id = first.GoalId,
            Name = first.Name,
            Category = first.Category,
            Statement = first.Statement,
            Owner = first.Owner,
            Status = first.Status,
            Metrics = metrics
        };
    }

    public ObjectiveDto NormalizeObjectiveRows(IReadOnlyList<WorkbookObjectiveRowDto> rows)
    {
        var first = rows.First();
        var metrics = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.MetricId))
            .GroupBy(x => x.MetricId!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ObjectiveMetricDto
            {
                Id = g.Key,
                ObjectiveId = first.ObjectiveId,
                MetricName = g.First().MetricName ?? string.Empty,
                BaselineValue = g.First().MetricBaseline ?? 0,
                TargetValue = g.First().MetricTarget ?? 0
            })
            .ToList();

        return new ObjectiveDto
        {
            Id = first.ObjectiveId,
            ParentGoalId = first.ParentGoalId,
            Name = first.Name,
            Statement = first.Statement,
            Owner = first.Owner,
            Status = first.Status,
            Metrics = metrics
        };
    }
}
