using Diten.Web.Config;

namespace Diten.Web.Services.EnterpriseStrategy;

public static class EnterpriseStrategyModule
{
    public static IReadOnlyList<string> RouteExports =>
        EnterpriseStrategyRegistry.Tabs.Select(x => x.Route).ToArray();
}
