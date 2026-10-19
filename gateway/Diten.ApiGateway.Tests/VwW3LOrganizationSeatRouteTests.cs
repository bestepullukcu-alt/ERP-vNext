using System.Reflection;
using System.Text.Json;
using Diten.ApiGateway.Middleware;
using Microsoft.AspNetCore.Http;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-VW-W3L PLT-ORG-SEAT (K-L8) — the two organization seat endpoints reach the Platform (5057) through pass-through routes
/// for their methods, and the path is a TENANT path (tenant resolution runs, X-Tenant-Id goes downstream, a platform-admin
/// token is refused) — never an admin path. Exactly this sub-path: /api/platform/organization itself stays an admin path.
/// </summary>
public sealed class VwW3LOrganizationSeatRouteTests
{
    private const string User = "00000000-0000-0000-0000-000000000001";

    [Theory]
    [InlineData("/api/platform/organization/seats/" + User, "GET")]
    [InlineData("/api/platform/organization/seats/lookup", "POST")]
    public void Each_seat_endpoint_has_a_platform_route_for_its_method(string path, string method)
    {
        var routes = Load().Routes.Where(r => Matches(r.UpstreamPathTemplate, path)).ToList();
        var route = routes.FirstOrDefault(r => r.UpstreamHttpMethod.Contains(method, StringComparer.OrdinalIgnoreCase));
        Assert.True(route is not null, $"No gateway route for {method} {path}.");
        Assert.All(route!.DownstreamHostAndPorts, x => Assert.Equal(5057, x.Port));
        Assert.Equal(route.UpstreamPathTemplate, route.DownstreamPathTemplate);
        Assert.Contains("OPTIONS", route.UpstreamHttpMethod, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_lookup_post_never_lands_on_the_single_user_route()
    {
        var single = Assert.Single(Load().Routes, r => r.UpstreamPathTemplate == "/api/platform/organization/seats/{userId}");
        Assert.DoesNotContain("POST", single.UpstreamHttpMethod, StringComparer.OrdinalIgnoreCase);
        var lookup = Assert.Single(Load().Routes, r => r.UpstreamPathTemplate == "/api/platform/organization/seats/lookup");
        Assert.DoesNotContain("GET", lookup.UpstreamHttpMethod, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/api/platform/organization/seats/" + User, true)]
    [InlineData("/api/platform/organization/seats/lookup", true)]
    [InlineData("/api/platform/organization", false)]
    [InlineData("/api/platform/organization/other", false)]
    public void The_seat_path_is_a_tenant_path_and_nothing_wider(string path, bool tenantScoped)
    {
        Assert.Equal(tenantScoped, Invoke("IsTenantScopedOrgPath", path));
        Assert.Equal(!tenantScoped, Invoke("IsAdminPath", path));
    }

    private static bool Invoke(string method, string path)
        => (bool)typeof(TenantResolutionMiddleware).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [new PathString(path)])!;

    private static bool Matches(string template, string path)
    {
        if (string.Equals(template, path, StringComparison.Ordinal)) return true;
        var slash = template.LastIndexOf('/');
        return template.EndsWith('}') && template[(slash + 1)..].StartsWith('{')
               && path.StartsWith(template[..(slash + 1)], StringComparison.Ordinal)
               && !path[(slash + 1)..].Contains('/');
    }

    private static FileConfiguration Load() => JsonSerializer.Deserialize<FileConfiguration>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ocelot.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
}
