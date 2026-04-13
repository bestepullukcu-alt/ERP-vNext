using Diten.WebUI.Models.EnterpriseStrategy;

namespace Diten.WebUI.Config;

public static class EnterpriseStrategyRegistry
{
    public const string BaseRoute = "/management-governance/enterprise-strategy-business-performance";

    public static IReadOnlyList<StrategyTab> Tabs => new List<StrategyTab>
    {
        new() { Key = "overview", Label = "Overview", Route = BaseRoute, Icon = "bx-home" },
        new() { Key = "planning", Label = "Planning & Periods", Route = $"{BaseRoute}/planning/cycles", Icon = "bx-calendar" },
        new() { Key = "goals", Label = "Goals", Route = $"{BaseRoute}/goals", Icon = "bx-target-lock" },
        new() { Key = "objectives", Label = "Objectives", Route = $"{BaseRoute}/objectives", Icon = "bx-list-check" },
        new() { Key = "connections", Label = "Strategy Alignment Register", Route = $"{BaseRoute}/connections", Icon = "bx-link" },
        new() { Key = "kpis", Label = "KPI & Scorecard", Route = $"{BaseRoute}/kpis", Icon = "bx-chart" },
        new() { Key = "cascade", Label = "Cascade & Consolidation", Route = $"{BaseRoute}/cascade/builder", Icon = "bx-layer" },
        new() { Key = "reviews", Label = "Strategic Reviews", Route = $"{BaseRoute}/reviews/calendar", Icon = "bx-time" }
    };
}
