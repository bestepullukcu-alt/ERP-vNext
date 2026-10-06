using System.Reflection;
using Diten.ManufacturingService.Api.Controllers;
using Diten.ManufacturingService.Api.ModuleRegistration;
using Diten.ManufacturingService.Infrastructure.Authorization;
using Xunit;

namespace Diten.ManufacturingService.Tests.Registration;

/// <summary>
/// Every permission an endpoint enforces is declared in the manifest — Platform syncs only declared keys to Auth, so an
/// enforced-but-undeclared key is a 403 for everyone, forever (lesson measured in MVP-6, recipe 6.4). Read from the
/// production controller's attributes, not from a list kept here.
/// </summary>
public sealed class BomManifestTests
{
    [Fact]
    public void Every_enforced_key_is_declared_and_nothing_else_is()
    {
        var enforced = typeof(BomController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes<HasPermissionAttribute>())
            .Select(a => a.Policy!.Replace("Permission:", string.Empty))
            .ToHashSet();
        var manifest = new BomRoutingsManifestProvider().GetManifest();
        var declared = manifest.Pages.Select(p => p.RequiredPermission)
            .Concat(manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey))
            .ToHashSet();

        Assert.Equal(BomPermissions.All.Order(), enforced.Order());
        Assert.Equal(enforced.Order(), declared.Order());
    }

    [Fact]
    public void Every_public_endpoint_is_permission_guarded()
    {
        var unguarded = typeof(BomController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.GetCustomAttributes<HasPermissionAttribute>().Any())
            .Select(m => m.Name)
            .ToList();
        Assert.Empty(unguarded);
    }

    [Fact]
    public void The_page_route_is_the_web_adapter_route()
    {
        var page = Assert.Single(new BomRoutingsManifestProvider().GetManifest().Pages);
        Assert.Equal("/Manufacturing/Boms", page.RoutePath);
        Assert.Equal("BOMS", page.PageCode);
    }
}
