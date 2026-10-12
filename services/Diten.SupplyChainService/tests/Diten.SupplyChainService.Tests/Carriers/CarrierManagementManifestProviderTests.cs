using Diten.SupplyChainService.Api.ModuleRegistration;
using Xunit;

namespace Diten.SupplyChainService.Tests.Carriers;

public sealed class CarrierManagementManifestProviderTests
{
    private static readonly Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument Manifest =
        new CarrierManagementManifestProvider().GetManifest();

    [Fact]
    public void Declares_exact_tenant_page_and_three_existing_permissions()
    {
        Assert.Equal("carrier-management", Manifest.ModuleCode);
        Assert.Equal("SupplyChainExecution", Manifest.Domain);
        Assert.Equal("DitenSupplyChainService", Manifest.Service);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.False(Manifest.IsBaseline);

        var page = Assert.Single(Manifest.Pages);
        Assert.Equal("CARRIERS", page.PageCode);
        Assert.Equal("/SupplyChain/Carriers", page.RoutePath);
        Assert.Equal("supplychain.carriers.read", page.RequiredPermission);
        Assert.True(page.IsNavigationVisible);
        Assert.Equal(
            ["supplychain.carriers.create", "supplychain.carriers.status.change"],
            page.Actions.Select(action => action.PermissionKey).OrderBy(value => value, StringComparer.Ordinal));
        Assert.DoesNotContain(page.Actions, action => action.ActionCode.Contains("EDIT", StringComparison.Ordinal));
        Assert.DoesNotContain(page.Actions, action => action.ActionCode.Contains("DELETE", StringComparison.Ordinal));
    }
}
