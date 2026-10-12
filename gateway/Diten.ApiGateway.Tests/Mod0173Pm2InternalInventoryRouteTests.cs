using System.Text.Json;
using System.Text.RegularExpressions;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-0173-PM2 (INVENTORY-BUNDLE 1.1.0, MOD-0290 R-12): <c>getStockPresence</c> lives at
/// <c>/api/internal/inventory/stock-presence</c> on InventoryService and is called service-to-service; the Gateway must NOT
/// route it — no route matches, so Ocelot answers 404. <c>ocelot.json</c> is not changed by this WP: the test reads the real file
/// (no catch-all route exists that could claim the path — measured here, it would fail the first test).
/// The contract direction: every INVENTORY-BUNDLE path under the default server <c>/api/inventory</c> is still routed by
/// <c>/api/inventory/{everything}</c>, and the one path declared on its own server <c>/api/internal/inventory</c> by none.
/// </summary>
public sealed class Mod0173Pm2InternalInventoryRouteTests
{
    private static FileConfiguration Config() => JsonSerializer.Deserialize<FileConfiguration>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ocelot.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    /// <summary>Ocelot placeholder semantics for these templates: <c>{x}</c> = one segment, a trailing <c>{everything}</c> = the rest.</summary>
    private static List<FileRoute> Matching(string path)
        => Config().Routes.Where(r =>
        {
            var pattern = Regex.Escape(r.UpstreamPathTemplate).Replace(@"\{everything}", ".+");
            pattern = Regex.Replace(pattern, @"\\\{[^}/]+}", "[^/]+");
            return Regex.IsMatch(path, "^" + pattern + "$", RegexOptions.IgnoreCase);
        }).ToList();

    [Theory]
    [InlineData("/api/internal/inventory/stock-presence")]
    [InlineData("/API/Internal/Inventory/Stock-Presence")]
    [InlineData("/api/internal/inventory")]
    [InlineData("/api/internal/inventory/anything/else")]
    public void No_gateway_route_reaches_the_internal_inventory_paths(string path)
        => Assert.Empty(Matching(path).Select(r => r.UpstreamPathTemplate));

    [Fact]
    public void No_route_claims_the_internal_inventory_prefix_and_no_catch_all_exists()
    {
        var templates = Config().Routes.Select(r => r.UpstreamPathTemplate).ToList();
        Assert.True(templates.Count > 100, "ocelot.json parse found too few routes: " + templates.Count);
        Assert.DoesNotContain(templates, t => t.StartsWith("/api/internal", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(templates, t => Regex.IsMatch(t, @"^/(api/)?\{[^}]+\}$"));
    }

    [Fact]
    public void The_inventory_contract_paths_split_exactly_between_the_inventory_route_and_no_route()
    {
        var (servers, operations) = InventoryContract();
        Assert.Equal("/api/inventory", servers.Default);
        Assert.True(operations.Count >= 7, "contract parse found too few operations: " + operations.Count);

        var routed = 0;
        var internalOnes = 0;
        foreach (var (path, method) in operations)
        {
            if (servers.PerPath.TryGetValue(path, out var own))
            {
                Assert.Equal("/api/internal/inventory", own);
                Assert.Empty(Matching(own + path).Select(r => r.UpstreamPathTemplate));
                internalOnes++;
                continue;
            }

            var route = Assert.Single(Matching(servers.Default + path));
            Assert.Equal("/api/inventory/{everything}", route.UpstreamPathTemplate);
            Assert.Contains(method, route.UpstreamHttpMethod);
            routed++;
        }

        Assert.Equal(1, internalOnes); // getStockPresence
        Assert.Equal(6, routed); // the six 1.0.x operations
    }

    private static ((string Default, Dictionary<string, string> PerPath) Servers, List<(string Path, string Method)> Operations) InventoryContract()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var lines = File.ReadAllLines(Directory.EnumerateFiles(Path.Combine(root!.FullName, "docs"), "inventory-bundle.openapi.yaml", SearchOption.AllDirectories)
            .Single(p => !p.Contains("Fixtures", StringComparison.Ordinal)));
        var serverIndex = Array.FindIndex(lines, l => l.StartsWith("servers:", StringComparison.Ordinal));
        var defaultServer = Regex.Match(lines[serverIndex + 1], @"url:\s*(\S+)").Groups[1].Value;

        var perPath = new Dictionary<string, string>(StringComparer.Ordinal);
        var operations = new List<(string Path, string Method)>();
        string? path = null;
        var inServers = false;
        foreach (var line in lines.SkipWhile(l => l.TrimEnd() != "paths:").Skip(1).TakeWhile(l => l.TrimEnd() != "components:"))
        {
            var p = Regex.Match(line, @"^  (/[^:]+):\s*$");
            if (p.Success) { path = Regex.Replace(p.Groups[1].Value, @"\{[^}]+}", "x"); inServers = false; continue; }
            if (Regex.IsMatch(line, @"^    servers:\s*$")) { inServers = true; continue; }
            var url = Regex.Match(line, @"^      - url:\s*(\S+)");
            if (inServers && url.Success && path is not null) { perPath[path] = url.Groups[1].Value; continue; }
            var m = Regex.Match(line, @"^    (get|post|patch|put|delete):");
            if (m.Success && path is not null) { inServers = false; operations.Add((path, m.Groups[1].Value.ToUpperInvariant())); }
        }

        return ((defaultServer, perPath), operations);
    }
}
