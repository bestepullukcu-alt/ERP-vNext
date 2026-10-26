namespace Diten.Web.Models.SupplyChain.CapacityPlans;

/// <summary>MOD-0192 permission keys, mirrored from CapacityPermissions.cs (backend). Display decisions only — the
/// service's [CapacityPermission] stays the authority.</summary>
public static class CapacityUiPermissions
{
    public const string Read = "supplychain.capacity-plans.read";
    public const string Create = "supplychain.capacity-plans.create";
    public const string ScenarioCreate = "supplychain.capacity-plans.scenario.create";
    public const string Evaluate = "supplychain.capacity-plans.evaluate";
}

/// <summary>The page's display flags, computed once on the server and handed to the script as JSON.</summary>
public sealed record CapacityPagePermissions(bool CanCreate, bool CanCreateScenario, bool CanEvaluate)
{
    public static CapacityPagePermissions From(Func<string, bool> has) =>
        new(has(CapacityUiPermissions.Create), has(CapacityUiPermissions.ScenarioCreate), has(CapacityUiPermissions.Evaluate));
}
