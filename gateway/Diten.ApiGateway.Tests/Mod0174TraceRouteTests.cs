using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-0174-INT (INT-TRACE, MOD-0174 pack §15): <c>/api/trace</c> and <c>/api/trace/{everything}</c> → Diten.InventoryService
/// (localhost:5066), GET / POST / PATCH / OPTIONS, authentication in the service (no gateway AuthenticationOptions), headers
/// passed through unchanged — the INT-1 / INT-2 (<c>/api/inventory</c>, <c>/api/locations</c>) pattern, block for block.
/// The contract direction is measured too: every path + method of <c>trace.openapi.yaml</c> is served by these routes.
/// </summary>
public sealed class Mod0174TraceRouteTests
{
    private static readonly string[] Methods = ["GET", "POST", "PATCH", "OPTIONS"];

    private static FileConfiguration Config() => JsonSerializer.Deserialize<FileConfiguration>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ocelot.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    [Theory]
    [InlineData("/api/trace")]
    [InlineData("/api/trace/{everything}")]
    public void The_trace_route_goes_to_InventoryService_5066_with_the_contract_methods_and_no_gateway_auth(string template)
    {
        var route = Assert.Single(Config().Routes, r => r.UpstreamPathTemplate == template);

        Assert.Equal(template, route.DownstreamPathTemplate);
        Assert.Equal("http", route.DownstreamScheme);
        var target = Assert.Single(route.DownstreamHostAndPorts);
        Assert.Equal("localhost", target.Host);
        Assert.Equal(5066, target.Port);
        Assert.Equal(Methods.OrderBy(m => m, StringComparer.Ordinal), route.UpstreamHttpMethod.OrderBy(m => m, StringComparer.Ordinal));
        Assert.DoesNotContain("DELETE", route.UpstreamHttpMethod);
        Assert.DoesNotContain("PUT", route.UpstreamHttpMethod);
        Assert.True(string.IsNullOrEmpty(route.AuthenticationOptions?.AuthenticationProviderKey), "authentication belongs to the service");
        Assert.Empty(route.AddHeadersToRequest);
        Assert.Empty(route.UpstreamHeaderTransform);
        Assert.Empty(route.DownstreamHeaderTransform);
        Assert.False(route.RateLimitOptions?.EnableRateLimiting ?? false);
    }

    [Fact]
    public void Only_these_two_routes_claim_the_trace_path()
        => Assert.Equal(["/api/trace", "/api/trace/{everything}"],
            Config().Routes.Select(r => r.UpstreamPathTemplate).Where(t => t.StartsWith("/api/trace", StringComparison.Ordinal)).OrderBy(t => t));

    [Fact]
    public void The_trace_blocks_are_the_locations_blocks_with_only_the_path_changed()
    {
        var routes = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ocelot.json")))!["Routes"]!.AsArray();
        foreach (var (locations, trace) in new[] { ("/api/locations", "/api/trace"), ("/api/locations/{everything}", "/api/trace/{everything}") })
        {
            var template = routes.Single(r => (string?)r!["UpstreamPathTemplate"] == locations)!.ToJsonString();
            var actual = routes.Single(r => (string?)r!["UpstreamPathTemplate"] == trace)!.ToJsonString();
            Assert.Equal(template.Replace(locations, trace, StringComparison.Ordinal), actual);
        }
    }

    [Fact]
    public void Every_contract_path_and_method_of_trace_openapi_is_served()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var lines = File.ReadAllLines(Path.Combine(root!.FullName, "docs", "analysis", "contracts", "trace.openapi.yaml"));
        Assert.Contains("servers: [{ url: /api/trace }]", lines);

        var everything = Config().Routes.Single(r => r.UpstreamPathTemplate == "/api/trace/{everything}");
        var operations = new List<(string Path, string Method)>();
        string? path = null;
        foreach (var line in lines.SkipWhile(l => l != "paths:").Skip(1).TakeWhile(l => l != "components:"))
        {
            var p = Regex.Match(line, @"^  (/[^:]+):\s*$");
            if (p.Success) { path = p.Groups[1].Value; continue; }
            var m = Regex.Match(line, @"^    (get|post|patch|put|delete):\s*$");
            if (m.Success && path is not null) operations.Add((path, m.Groups[1].Value.ToUpperInvariant()));
        }

        Assert.True(operations.Count >= 16, "contract parse found too few operations: " + operations.Count);
        Assert.All(operations, op =>
        {
            Assert.StartsWith("/", op.Path); // every contract path is below /api/trace → served by /api/trace/{everything}
            Assert.Contains(op.Method, everything.UpstreamHttpMethod);
        });
    }
}
