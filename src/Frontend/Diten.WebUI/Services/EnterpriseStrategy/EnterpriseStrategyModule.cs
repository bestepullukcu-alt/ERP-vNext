using Diten.WebUI.Config;

namespace Diten.WebUI.Services.EnterpriseStrategy;

public static class EnterpriseStrategyModule
{
    public static IReadOnlyList<string> RouteExports =>
        EnterpriseStrategyRegistry.Tabs.Select(x => x.Route).ToArray();
}
