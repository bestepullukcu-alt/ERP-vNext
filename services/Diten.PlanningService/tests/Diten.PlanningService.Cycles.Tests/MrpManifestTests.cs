using System.Reflection;
using Diten.PlanningService.Api.Features.DemandPlanning;
using Diten.PlanningService.Api.Features.Mrp;
using Diten.PlanningService.Api.ModuleRegistration;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class MrpManifestTests
{
    [Fact]
    public void ManifestCoversMrpControllerPermissions_Only()
    {
        var manifest = new MrpManifestProvider().GetManifest();
        var declared = manifest.ApiPermissions!.Select(p => p.PermissionKey)
            .OrderBy(p => p).ToArray();
        var enforced = typeof(LeadTimeController).Assembly.GetTypes()
            .Where(t => t.IsClass && t.Namespace == typeof(LeadTimeController).Namespace)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .SelectMany(m => m.GetCustomAttributes<HasPermissionAttribute>())
            .Select(p => p.Policy!).Distinct().OrderBy(p => p).ToArray();
        Assert.Equal("mrp-replenishment", manifest.ModuleCode);
        Assert.Equal(enforced, declared);
        Assert.All(manifest.ApiPermissions!, p => Assert.Equal("Tenant", p.Scope));
    }
}
