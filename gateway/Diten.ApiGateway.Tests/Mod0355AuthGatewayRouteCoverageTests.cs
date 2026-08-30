using System.Text.Json;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

public sealed class Mod0355AuthGatewayRouteCoverageTests
{
    private const string UpstreamBase = "/api/management-governance/process-modeling";
    private const string DownstreamBase = "/internal/local-test/v1/process-modeling";
    private static readonly HashSet<string> ExactMethods = new(StringComparer.Ordinal)
    {
        "GET", "POST", "PUT", "PATCH", "OPTIONS", "DELETE"
    };

    private static FileConfiguration LoadConfiguration()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ocelot.json");
        Assert.True(File.Exists(path), $"ocelot.json not found at {path}.");
        return Assert.IsType<FileConfiguration>(JsonSerializer.Deserialize<FileConfiguration>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
    }

    [Fact]
    public void Mod0355_has_exact_additive_base_and_catch_all_routes()
    {
        var routes = Mod0355Routes(LoadConfiguration());

        Assert.Equal(2, routes.Length);
        AssertRoute(routes, UpstreamBase, DownstreamBase);
        AssertRoute(routes, $"{UpstreamBase}/{{everything}}", $"{DownstreamBase}/{{everything}}");
    }

    [Fact]
    public void Mod0355_routes_are_ordered_base_then_catch_all_before_pvg()
    {
        var routes = LoadConfiguration().Routes;
        var baseIndex = routes.FindIndex(route => route.UpstreamPathTemplate == UpstreamBase);
        var catchAllIndex = routes.FindIndex(route => route.UpstreamPathTemplate == $"{UpstreamBase}/{{everything}}");
        var pvgIndex = routes.FindIndex(route => route.UpstreamPathTemplate == "/api/pv-case-intake-triage");

        Assert.True(baseIndex >= 0);
        Assert.Equal(baseIndex + 1, catchAllIndex);
        Assert.True(catchAllIndex < pvgIndex);
    }

    [Theory]
    [InlineData("GET", "models")]
    [InlineData("POST", "models")]
    [InlineData("PUT", "models/11111111-1111-4111-8111-111111111111")]
    [InlineData("POST", "model-versions/11111111-1111-4111-8111-111111111111/request-review")]
    [InlineData("GET", "improvements")]
    [InlineData("POST", "improvements")]
    [InlineData("PUT", "improvements/11111111-1111-4111-8111-111111111111")]
    [InlineData("POST", "improvements/11111111-1111-4111-8111-111111111111/outcomes")]
    public void Catch_all_covers_representative_model_and_improvement_calls(string method, string suffix)
    {
        var route = Assert.Single(Mod0355Routes(LoadConfiguration()), candidate =>
            candidate.UpstreamPathTemplate == $"{UpstreamBase}/{{everything}}");

        Assert.Contains(method, route.UpstreamHttpMethod);
        Assert.DoesNotContain("5017", $"{UpstreamBase}/{suffix}", StringComparison.Ordinal);
    }

    [Fact]
    public void Mod0355_routes_do_not_inject_or_transform_authority_headers()
    {
        foreach (var route in Mod0355Routes(LoadConfiguration()))
        {
            Assert.True(route.AddHeadersToRequest is null || route.AddHeadersToRequest.Count == 0);
            Assert.True(route.UpstreamHeaderTransform is null || route.UpstreamHeaderTransform.Count == 0);
            Assert.DoesNotContain("workcenter", route.UpstreamPathTemplate, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("workcenter", route.DownstreamPathTemplate, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Mod0355_addition_does_not_claim_pvg_port_or_path()
    {
        var routes = Mod0355Routes(LoadConfiguration());

        Assert.All(routes, route => Assert.All(route.DownstreamHostAndPorts, endpoint => Assert.Equal(5017, endpoint.Port)));
        Assert.All(routes, route => Assert.DoesNotContain("pv-case", route.UpstreamPathTemplate, StringComparison.OrdinalIgnoreCase));
        Assert.All(routes, route => Assert.DoesNotContain("pv-case", route.DownstreamPathTemplate, StringComparison.OrdinalIgnoreCase));
    }

    private static FileRoute[] Mod0355Routes(FileConfiguration configuration) => configuration.Routes
        .Where(route => route.UpstreamPathTemplate.StartsWith(UpstreamBase, StringComparison.Ordinal))
        .ToArray();

    private static void AssertRoute(
        IReadOnlyCollection<FileRoute> routes,
        string upstreamTemplate,
        string downstreamTemplate)
    {
        var route = Assert.Single(routes, candidate => candidate.UpstreamPathTemplate == upstreamTemplate);
        Assert.Equal(downstreamTemplate, route.DownstreamPathTemplate);
        Assert.Equal("http", route.DownstreamScheme);
        var endpoint = Assert.Single(route.DownstreamHostAndPorts);
        Assert.Equal("127.0.0.1", endpoint.Host);
        Assert.Equal(5017, endpoint.Port);
        Assert.True(ExactMethods.SetEquals(route.UpstreamHttpMethod));
        Assert.DoesNotContain("5017", route.UpstreamPathTemplate, StringComparison.Ordinal);
    }
}
