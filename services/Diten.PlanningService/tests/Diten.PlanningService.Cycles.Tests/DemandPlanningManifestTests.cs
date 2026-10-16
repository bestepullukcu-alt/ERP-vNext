using System.Reflection;
using Diten.PlanningService.Api.Features.DemandPlanning;
using Diten.PlanningService.Api.ModuleRegistration;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class DemandPlanningManifestTests
{
    [Fact]
    public void ManifestCoversEveryDemandControllerPermission()
    {
        var manifest = new DemandPlanningManifestProvider().GetManifest();
        var declared = manifest.Pages.Select(x => x.RequiredPermission)
            .Concat(manifest.Pages.SelectMany(x => x.Actions).Select(x => x.PermissionKey))
            .Concat((manifest.ApiPermissions ?? []).Select(x => x.PermissionKey))
            .ToHashSet(StringComparer.Ordinal);
        var enforced = typeof(DemandHistoryImportBatchesController).Assembly.GetTypes()
            .Where(x => x.IsClass && x.Namespace == typeof(DemandHistoryImportBatchesController).Namespace)
            .SelectMany(x => x.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .SelectMany(x => x.GetCustomAttributes<HasPermissionAttribute>())
            .Select(x => x.Policy!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal("demand-planning", manifest.ModuleCode);
        Assert.Equal("/SupplyChain/DemandPlanning", Assert.Single(manifest.Pages).RoutePath);
        Assert.Equal("demand.plans.read", manifest.Pages[0].RequiredPermission);
        Assert.All(manifest.ApiPermissions!, x => Assert.Equal("Tenant", x.Scope));
        Assert.Equal(enforced.OrderBy(x => x), declared.OrderBy(x => x));
    }
}
