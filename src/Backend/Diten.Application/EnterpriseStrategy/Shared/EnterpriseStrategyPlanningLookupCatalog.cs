namespace Diten.Application.EnterpriseStrategy.Shared;

public static class EnterpriseStrategyPlanningLookupCatalog
{
    public static IReadOnlyList<string> PlanningCycleTypeValues { get; } = new[]
    {
        "Annual Plan",
        "Multi-Year Strategy",
        "Rolling Plan",
        "Quarterly Replan",
        "Transformation Horizon"
    };

    public static IReadOnlyList<string> ReviewCadenceValues { get; } = new[]
    {
        "Monthly",
        "Quarterly",
        "Semiannual",
        "Annual"
    };

    public static IReadOnlyList<string> LifecycleStatusValues { get; } = new[]
    {
        "Draft",
        "Active",
        "Archived"
    };

    public static IReadOnlyList<string> ScenarioTypeValues { get; } = new[]
    {
        "Base",
        "Optimistic",
        "Conservative",
        "Stress"
    };

    public static IReadOnlySet<string> PlanningCycleTypes { get; } =
        new HashSet<string>(PlanningCycleTypeValues, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlySet<string> ReviewCadences { get; } =
        new HashSet<string>(ReviewCadenceValues, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlySet<string> LifecycleStatuses { get; } =
        new HashSet<string>(LifecycleStatusValues, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlySet<string> ScenarioTypes { get; } =
        new HashSet<string>(ScenarioTypeValues, StringComparer.OrdinalIgnoreCase);
}
