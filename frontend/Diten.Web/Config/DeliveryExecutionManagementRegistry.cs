using Diten.Web.Models.EnterpriseStrategy;

namespace Diten.Web.Config;

public static class DeliveryExecutionManagementRegistry
{
    public const string BaseRoute = "/management-governance/delivery-execution";

    public static IReadOnlyList<StrategyTab> Tabs => new List<StrategyTab>
    {
        new() { Key = "overview", Label = "Overview", Route = BaseRoute },
        new() { Key = "initiatives", Label = "Initiatives", Route = $"{BaseRoute}/initiatives" },
        new() { Key = "projects", Label = "Projects", Route = $"{BaseRoute}/projects" },
        new() { Key = "programs", Label = "Programs", Route = $"{BaseRoute}/programs" },
        new() { Key = "delivery-map", Label = "Delivery Map / Dependencies", Route = $"{BaseRoute}/delivery-map" }
    };
}
