namespace Diten.Web.Models.ManagementGovernance.ProcessModeling.ProcessPerformance;

public static class ProcessPerformanceFrontendPermissions
{
    public const string Read = "management-governance.process-modeling.kpi-bindings.read";
    public const string Create = "management-governance.process-modeling.kpi-bindings.create";
    public const string Archive = "management-governance.process-modeling.kpi-bindings.archive";
    public static IReadOnlySet<string> Exact { get; } = new HashSet<string>([Read, Create, Archive], StringComparer.Ordinal);
}

public sealed record ProcessPerformanceIndexViewModel(IReadOnlySet<string> Permissions);
