using System.Reflection;
using System.Text.Json;
using Diten.ApiGateway.Middleware;
using Microsoft.AspNetCore.Http;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-VW-W6 PLT-W6 (MOD-0168 AÇIK-2) — the seat's sibling <c>GET /api/platform/organization/seats/{userId}/line-managers</c>
/// reaches the Platform (5057) through its own pass-through route (GET only), and its path is a TENANT path (the seats
/// prefix already in <c>IsTenantScopedOrgPath</c> covers the sub-path) — never an admin path. W3L / W5 lesson: a new
/// endpoint is pinned here with its method.
/// </summary>
public sealed class VwW6LineManagersRouteTests
{
    private const string Template = "/api/platform/organization/seats/{userId}/line-managers";
    private const string Path = "/api/platform/organization/seats/00000000-0000-0000-0000-000000000001/line-managers";

    [Fact]
    public void The_line_managers_endpoint_has_its_own_get_route_to_the_platform()
    {
        var route = Assert.Single(Load().Routes, r => r.UpstreamPathTemplate == Template);
        Assert.Equal(Template, route.DownstreamPathTemplate);
        Assert.All(route.DownstreamHostAndPorts, x => Assert.Equal(5057, x.Port));
        Assert.Contains("GET", route.UpstreamHttpMethod, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("OPTIONS", route.UpstreamHttpMethod, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(route.UpstreamHttpMethod, m => m is "POST" or "PUT" or "PATCH" or "DELETE");
    }

    [Theory]
    [InlineData(Path, true)]
    [InlineData("/api/platform/organization/seats/00000000-0000-0000-0000-000000000001", true)]
    [InlineData("/api/platform/organization", false)]
    public void The_line_managers_path_is_a_tenant_path_like_the_seat_itself(string path, bool tenantScoped)
    {
        Assert.Equal(tenantScoped, Invoke("IsTenantScopedOrgPath", path));
        Assert.Equal(!tenantScoped, Invoke("IsAdminPath", path));
    }

    private static bool Invoke(string method, string path)
        => (bool)typeof(TenantResolutionMiddleware).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [new PathString(path)])!;

    private static FileConfiguration Load() => JsonSerializer.Deserialize<FileConfiguration>(
        File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "ocelot.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
}
