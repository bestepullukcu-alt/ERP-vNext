using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-0177-INT (MOD-0177 INT-RECALL, pack §1 INT-RECALL, §15): <c>/api/recall</c> + <c>/api/recall/{everything}</c> → Diten.InventoryService
/// (localhost:5067). The INT-EXPIRY blocks (<see cref="MvpOneExpiryRouteTests"/>, themselves the INT-QE quarantine blocks) cloned with only the
/// path changed: authentication in the service (no gateway AuthenticationOptions), headers passed through unchanged, no rate limit. Methods =
/// the methods of <c>recall.openapi.yaml</c> (GET, POST, PUT) + OPTIONS. Contract direction: every path + method of the WHOLE contract (R1
/// served, R2–R7 not yet implemented — the route carries the module) is matched by EXACTLY ONE route; DELETE / PATCH by none; prefix
/// look-alikes (<c>/api/recallX</c>) never reach the recall blocks; the neighbours keep their own routes. The blocks follow
/// <c>/api/expiry/{everything}</c> (end of the MVP-1 group; the INT-QE order test pins the four blocks after trace — D-INT-1).
/// </summary>
public sealed class MvpOneRecallRouteTests
{
    private static readonly string[] RecallMethods = ["GET", "POST", "PUT", "OPTIONS"];

    private static string OcelotText() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ocelot.json"));

    private static FileConfiguration Config() => JsonSerializer.Deserialize<FileConfiguration>(
        OcelotText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    [Theory]
    [InlineData("/api/recall")]
    [InlineData("/api/recall/{everything}")]
    public void The_route_goes_to_InventoryService_5067_with_the_contract_methods_and_no_gateway_auth(string template)
    {
        var route = Assert.Single(Config().Routes, r => r.UpstreamPathTemplate == template);

        Assert.Equal(template, route.DownstreamPathTemplate);
        Assert.Equal("http", route.DownstreamScheme);
        var target = Assert.Single(route.DownstreamHostAndPorts);
        Assert.Equal("localhost", target.Host);
        Assert.Equal(5067, target.Port);
        Assert.Equal(RecallMethods.OrderBy(m => m, StringComparer.Ordinal), route.UpstreamHttpMethod.OrderBy(m => m, StringComparer.Ordinal));
        Assert.DoesNotContain("DELETE", route.UpstreamHttpMethod);
        Assert.DoesNotContain("PATCH", route.UpstreamHttpMethod);
#pragma warning disable CS0618 // the single-key property is still the one the INT-QE blocks would set
        Assert.True(string.IsNullOrEmpty(route.AuthenticationOptions?.AuthenticationProviderKey), "authentication belongs to the service");
#pragma warning restore CS0618
        Assert.Empty(route.AuthenticationOptions?.AuthenticationProviderKeys ?? []);
        Assert.Empty(route.AddHeadersToRequest);
        Assert.Empty(route.UpstreamHeaderTransform);
        Assert.Empty(route.DownstreamHeaderTransform);
        Assert.False(route.RateLimitOptions?.EnableRateLimiting ?? false);
    }

    [Fact]
    public void Only_the_two_new_routes_claim_the_prefix()
        => Assert.Equal(["/api/recall", "/api/recall/{everything}"], Config().Routes.Select(r => r.UpstreamPathTemplate)
            .Where(t => t.StartsWith("/api/recall", StringComparison.Ordinal)).OrderBy(t => t, StringComparer.Ordinal));

    [Fact]
    public void The_new_blocks_are_the_expiry_blocks_with_only_the_path_changed_and_follow_them()
    {
        var routes = JsonNode.Parse(OcelotText())!["Routes"]!.AsArray();
        var templates = routes.Select(r => (string?)r!["UpstreamPathTemplate"]).ToList();
        var after = templates.IndexOf("/api/expiry/{everything}");
        Assert.Equal(["/api/recall", "/api/recall/{everything}"], templates.Skip(after + 1).Take(2));

        foreach (var (expiry, path) in new[] { ("/api/expiry", "/api/recall"), ("/api/expiry/{everything}", "/api/recall/{everything}") })
        {
            var expected = routes.Single(r => (string?)r!["UpstreamPathTemplate"] == expiry)!.DeepClone().AsObject();
            expected["DownstreamPathTemplate"] = path;
            expected["UpstreamPathTemplate"] = path;
            var actual = routes.Single(r => (string?)r!["UpstreamPathTemplate"] == path)!;
            Assert.True(JsonNode.DeepEquals(expected, actual), $"{path} differs from the {expiry} block beyond the path");
        }
    }

    [Fact]
    public void Every_path_and_method_of_recall_openapi_is_served_by_exactly_the_recall_route()
    {
        var (servers, operations) = Contract("recall.openapi.yaml");
        Assert.Equal("/api/recall", servers);
        Assert.Equal(50, operations.Count); // 22 R1 + 28 not yet implemented (x-implemented-by-slice) — the route carries all
        Assert.Equal(["GET", "POST", "PUT"], operations.Select(o => o.Method).Distinct().OrderBy(m => m, StringComparer.Ordinal));
        Assert.All(operations, op =>
        {
            var route = Assert.Single(Matching(servers + op.Path));
            Assert.Equal("/api/recall/{everything}", route.UpstreamPathTemplate);
            Assert.Contains(op.Method, route.UpstreamHttpMethod);
            Assert.Equal(5067, Assert.Single(route.DownstreamHostAndPorts).Port);
        });
    }

    [Theory]
    [InlineData("/api/recall/settings", "PUT")] // updateSettings
    [InlineData("/api/recall/drill-plans/3f2b5a1e-0000-4000-8000-000000000001", "PUT")] // updateDrillPlan (R5)
    [InlineData("/api/recall/settings", "GET")]
    [InlineData("/api/recall/cases", "POST")]
    [InlineData("/api/recall/cases/3f2b5a1e-0000-4000-8000-000000000001/pending-action", "POST")]
    [InlineData("/api/recall/cases/3f2b5a1e-0000-4000-8000-000000000001/scope/approve", "POST")]
    [InlineData("/api/recall/trace", "GET")] // runTrace (R2)
    public void Named_operations_reach_the_recall_route_with_their_method(string path, string method)
    {
        var route = Assert.Single(Matching(path));
        Assert.Equal("/api/recall/{everything}", route.UpstreamPathTemplate);
        Assert.Contains(method, route.UpstreamHttpMethod);
    }

    /// <summary>R4: the Platform e-signature subject provider calls <c>GET /api/recall/esign-subjects/{subjectType}/{subjectId}</c> through this route.</summary>
    [Theory]
    [InlineData("/api/recall/esign-subjects/recall.case/3f2b5a1e-0000-4000-8000-000000000001")]
    [InlineData("/api/recall/esign-subjects/recall.drill-run/3f2b5a1e-0000-4000-8000-000000000002")]
    [InlineData("/api/recall/esign-subjects/recall.settings-change/3f2b5a1e-0000-4000-8000-000000000003")]
    public void The_esign_subject_provider_path_reaches_InventoryService(string path)
    {
        var route = Assert.Single(Matching(path));
        Assert.Equal("/api/recall/{everything}", route.UpstreamPathTemplate);
        Assert.Contains("GET", route.UpstreamHttpMethod);
        Assert.Equal(5067, Assert.Single(route.DownstreamHostAndPorts).Port);
    }

    /// <summary>getLotRecallStatus — the 0174 lot badge, the 0173 movement band and the 0176 / 0178 consumers call it through this route.</summary>
    [Fact]
    public void The_lot_recall_status_path_reaches_InventoryService()
    {
        var route = Assert.Single(Matching("/api/recall/lots/3f2b5a1e-0000-4000-8000-000000000001/status"));
        Assert.Equal("/api/recall/{everything}", route.UpstreamPathTemplate);
        Assert.Contains("GET", route.UpstreamHttpMethod);
        Assert.Equal(5067, Assert.Single(route.DownstreamHostAndPorts).Port);
    }

    [Theory]
    [InlineData("/api/recall/settings")]
    [InlineData("/api/recall/cases")]
    [InlineData("/api/recall")]
    public void Methods_outside_the_contract_are_matched_by_no_route(string path)
        => Assert.All(Matching(path), r =>
        {
            Assert.DoesNotContain("DELETE", r.UpstreamHttpMethod);
            Assert.DoesNotContain("PATCH", r.UpstreamHttpMethod);
        });

    [Theory]
    [InlineData("/api/recallX")]
    [InlineData("/api/recallX/settings")]
    [InlineData("/api/recall-old/cases")]
    [InlineData("/api/recallcases")]
    public void Prefix_look_alikes_never_reach_the_recall_blocks(string path)
        => Assert.DoesNotContain(Matching(path), r => r.UpstreamPathTemplate.StartsWith("/api/recall", StringComparison.Ordinal));

    [Theory]
    [InlineData("/api/expiry/policy", "/api/expiry/{everything}", 5067)]
    [InlineData("/api/expiry", "/api/expiry", 5067)]
    [InlineData("/api/quarantine/holds", "/api/quarantine/{everything}", 5067)]
    [InlineData("/api/trace/lots", "/api/trace/{everything}", 5067)]
    [InlineData("/api/inventory/movements", "/api/inventory/{everything}", 5067)]
    [InlineData("/api/v1/esign/policy", "/api/v1/esign/{everything}", 5057)]
    [InlineData("/api/bom/items", "/api/bom/{everything}", 5070)]
    public void Neighbouring_paths_still_reach_their_own_route_only(string path, string template, int port)
    {
        var route = Assert.Single(Matching(path));
        Assert.Equal(template, route.UpstreamPathTemplate);
        Assert.Equal(port, Assert.Single(route.DownstreamHostAndPorts).Port);
    }

    // ── contract parsing + Ocelot-like matching (the INT-QE / INT-EXPIRY helpers) ────────────────────────

    private static List<FileRoute> Matching(string path)
        => Config().Routes.Where(r =>
        {
            var pattern = Regex.Escape(r.UpstreamPathTemplate).Replace(@"\{everything}", ".+");
            pattern = Regex.Replace(pattern, @"\\\{[^}/]+}", "[^/]+");
            return Regex.IsMatch(path, "^" + pattern + "$");
        }).ToList();

    private static (string Servers, List<(string Path, string Method)> Operations) Contract(string fileName)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var lines = File.ReadAllLines(Directory.EnumerateFiles(Path.Combine(root!.FullName, "docs"), fileName, SearchOption.AllDirectories).Single());
        var servers = Regex.Match(lines.Single(l => l.StartsWith("servers:", StringComparison.Ordinal)), @"url:\s*([^\s}]+)").Groups[1].Value;
        var operations = new List<(string Path, string Method)>();
        string? path = null;
        foreach (var line in lines.SkipWhile(l => l.TrimEnd() != "paths:").Skip(1).TakeWhile(l => l.TrimEnd() != "components:"))
        {
            var p = Regex.Match(line, @"^  (/[^:]+):\s*$");
            if (p.Success) { path = Regex.Replace(p.Groups[1].Value, @"\{[^}]+}", "x"); continue; }
            var m = Regex.Match(line, @"^    (get|post|patch|put|delete):");
            if (m.Success && path is not null) operations.Add((path, m.Groups[1].Value.ToUpperInvariant()));
        }

        return (servers, operations);
    }
}
