using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-0176-INT (MOD-0176 INT-EXPIRY, pack §15): <c>/api/expiry</c> + <c>/api/expiry/{everything}</c> → Diten.InventoryService
/// (localhost:5067). The INT-QE quarantine blocks (<see cref="MvpOneQuarantineEsignRouteTests"/>) cloned with only path and method list
/// changed: authentication in the service (no gateway AuthenticationOptions), headers passed through unchanged, no rate limit. Methods =
/// the methods of <c>fefo.openapi.yaml</c> (GET, POST, PUT) + OPTIONS. Contract direction: every path + method of the WHOLE contract (F1
/// served, F2–F5 not yet implemented — the route carries the module) is matched by EXACTLY ONE route; DELETE / PATCH by none; prefix
/// look-alikes (<c>/api/expiryX</c>) never reach the expiry blocks; the neighbours keep their own routes.
/// </summary>
public sealed class MvpOneExpiryRouteTests
{
    private static readonly string[] ExpiryMethods = ["GET", "POST", "PUT", "OPTIONS"];

    private static string OcelotText() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ocelot.json"));

    private static FileConfiguration Config() => JsonSerializer.Deserialize<FileConfiguration>(
        OcelotText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    [Theory]
    [InlineData("/api/expiry")]
    [InlineData("/api/expiry/{everything}")]
    public void The_route_goes_to_InventoryService_5067_with_the_contract_methods_and_no_gateway_auth(string template)
    {
        var route = Assert.Single(Config().Routes, r => r.UpstreamPathTemplate == template);

        Assert.Equal(template, route.DownstreamPathTemplate);
        Assert.Equal("http", route.DownstreamScheme);
        var target = Assert.Single(route.DownstreamHostAndPorts);
        Assert.Equal("localhost", target.Host);
        Assert.Equal(5067, target.Port);
        Assert.Equal(ExpiryMethods.OrderBy(m => m, StringComparer.Ordinal), route.UpstreamHttpMethod.OrderBy(m => m, StringComparer.Ordinal));
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
        => Assert.Equal(["/api/expiry", "/api/expiry/{everything}"], Config().Routes.Select(r => r.UpstreamPathTemplate)
            .Where(t => t.StartsWith("/api/expiry", StringComparison.Ordinal)).OrderBy(t => t, StringComparer.Ordinal));

    [Fact]
    public void The_new_blocks_are_the_quarantine_blocks_with_only_path_and_methods_changed_and_close_the_MVP_1_group()
    {
        var routes = JsonNode.Parse(OcelotText())!["Routes"]!.AsArray();
        var templates = routes.Select(r => (string?)r!["UpstreamPathTemplate"]).ToList();
        var after = templates.IndexOf("/api/v1/esign/{everything}");
        Assert.Equal(["/api/expiry", "/api/expiry/{everything}"], templates.Skip(after + 1).Take(2));

        foreach (var (quarantine, path) in new[] { ("/api/quarantine", "/api/expiry"), ("/api/quarantine/{everything}", "/api/expiry/{everything}") })
        {
            var expected = routes.Single(r => (string?)r!["UpstreamPathTemplate"] == quarantine)!.DeepClone().AsObject();
            expected["DownstreamPathTemplate"] = path;
            expected["UpstreamPathTemplate"] = path;
            expected["UpstreamHttpMethod"] = new JsonArray(ExpiryMethods.Select(m => (JsonNode)m).ToArray());
            var actual = routes.Single(r => (string?)r!["UpstreamPathTemplate"] == path)!;
            Assert.True(JsonNode.DeepEquals(expected, actual), $"{path} differs from the {quarantine} block beyond path / methods");
        }
    }

    [Fact]
    public void Every_path_and_method_of_fefo_openapi_is_served_by_exactly_the_expiry_route()
    {
        var (servers, operations) = Contract("fefo.openapi.yaml");
        Assert.Equal("/api/expiry", servers);
        Assert.Equal(21, operations.Count); // 11 F1 + 10 not yet implemented (x-implemented-by-slice) — the route carries all
        Assert.Equal(["GET", "POST", "PUT"], operations.Select(o => o.Method).Distinct().OrderBy(m => m, StringComparer.Ordinal));
        Assert.All(operations, op =>
        {
            var route = Assert.Single(Matching(servers + op.Path));
            Assert.Equal("/api/expiry/{everything}", route.UpstreamPathTemplate);
            Assert.Contains(op.Method, route.UpstreamHttpMethod);
            Assert.Equal(5067, Assert.Single(route.DownstreamHostAndPorts).Port);
        });
    }

    [Theory]
    [InlineData("/api/expiry/policy", "PUT")] // updatePolicy — the only PUT of the contract
    [InlineData("/api/expiry/policy", "GET")]
    [InlineData("/api/expiry/exceptions/3f2b5a1e-0000-4000-8000-000000000001/pending-resolution", "POST")]
    [InlineData("/api/expiry/fefo-order", "GET")]
    public void Named_operations_reach_the_expiry_route_with_their_method(string path, string method)
    {
        var route = Assert.Single(Matching(path));
        Assert.Equal("/api/expiry/{everything}", route.UpstreamPathTemplate);
        Assert.Contains(method, route.UpstreamHttpMethod);
    }

    /// <summary>F3: the Platform e-signature subject provider calls <c>GET /api/expiry/esign-subjects/{subjectType}/{subjectId}</c> through this route.</summary>
    [Theory]
    [InlineData("/api/expiry/esign-subjects/expiry.exception/3f2b5a1e-0000-4000-8000-000000000001")]
    [InlineData("/api/expiry/esign-subjects/expiry.date-override/3f2b5a1e-0000-4000-8000-000000000002")]
    public void The_esign_subject_provider_path_reaches_InventoryService(string path)
    {
        var route = Assert.Single(Matching(path));
        Assert.Equal("/api/expiry/{everything}", route.UpstreamPathTemplate);
        Assert.Contains("GET", route.UpstreamHttpMethod);
        Assert.Equal(5067, Assert.Single(route.DownstreamHostAndPorts).Port);
    }

    [Theory]
    [InlineData("/api/expiry/policy")]
    [InlineData("/api/expiry/exceptions")]
    [InlineData("/api/expiry")]
    public void Methods_outside_the_contract_are_matched_by_no_route(string path)
        => Assert.All(Matching(path), r =>
        {
            Assert.DoesNotContain("DELETE", r.UpstreamHttpMethod);
            Assert.DoesNotContain("PATCH", r.UpstreamHttpMethod);
        });

    [Theory]
    [InlineData("/api/expiryX")]
    [InlineData("/api/expiryX/policy")]
    [InlineData("/api/expiry-old/policy")]
    [InlineData("/api/expirypolicy")]
    public void Prefix_look_alikes_never_reach_the_expiry_blocks(string path)
        => Assert.DoesNotContain(Matching(path), r => r.UpstreamPathTemplate.StartsWith("/api/expiry", StringComparison.Ordinal));

    [Theory]
    [InlineData("/api/quarantine/holds", "/api/quarantine/{everything}", 5067)]
    [InlineData("/api/quarantine", "/api/quarantine", 5067)]
    [InlineData("/api/trace/lots", "/api/trace/{everything}", 5067)]
    [InlineData("/api/inventory/movements", "/api/inventory/{everything}", 5067)]
    [InlineData("/api/v1/esign/policy", "/api/v1/esign/{everything}", 5057)]
    [InlineData("/api/v1/workflow/instances", "/api/v1/workflow/{everything}", 0)]
    public void Neighbouring_paths_still_reach_their_own_route_only(string path, string template, int port)
    {
        var route = Assert.Single(Matching(path));
        Assert.Equal(template, route.UpstreamPathTemplate);
        if (port != 0) Assert.Equal(port, Assert.Single(route.DownstreamHostAndPorts).Port);
    }

    // ── contract parsing + Ocelot-like matching (the INT-QE helpers) ─────────────────────────────────────

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
