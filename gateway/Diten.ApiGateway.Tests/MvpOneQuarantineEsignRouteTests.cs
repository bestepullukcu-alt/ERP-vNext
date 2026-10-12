using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-MVP1-INT-QE (MOD-0175 INT + MOD-0022 INT-ESIGN): <c>/api/quarantine</c> + <c>/api/quarantine/{everything}</c> →
/// Diten.InventoryService (localhost:5067; OD-P1 2026-10-12, was 5066) and <c>/api/v1/esign</c> + <c>/api/v1/esign/{everything}</c> → Diten.Platform
/// (localhost:5057). The INT-TRACE blocks (<see cref="Mod0174TraceRouteTests"/>) cloned with only path, port and method list
/// changed: authentication in the service (no gateway AuthenticationOptions), headers passed through unchanged, no rate limit.
/// Contract direction: every path + method of <c>quarantine.openapi.yaml</c> and of <c>esign.openapi.yaml</c> is matched by
/// EXACTLY ONE route — the new ones, or (AuthService re-auth / ticket) the existing <c>/api/tenant-auth/{everything}</c>; the
/// two internal operations of the e-signature contract are matched by NO route (they stay off the gateway).
/// </summary>
public sealed class MvpOneQuarantineEsignRouteTests
{
    private static readonly string[] QuarantineMethods = ["GET", "POST", "PATCH", "PUT", "OPTIONS"];
    private static readonly string[] EsignMethods = ["GET", "POST", "PUT", "OPTIONS"];

    private static string OcelotText() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ocelot.json"));

    private static FileConfiguration Config() => JsonSerializer.Deserialize<FileConfiguration>(
        OcelotText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    public static TheoryData<string, int, string[]> NewRoutes() => new()
    {
        { "/api/quarantine", 5067, QuarantineMethods },
        { "/api/quarantine/{everything}", 5067, QuarantineMethods },
        { "/api/v1/esign", 5057, EsignMethods },
        { "/api/v1/esign/{everything}", 5057, EsignMethods }
    };

    [Theory]
    [MemberData(nameof(NewRoutes))]
    public void The_route_goes_to_its_service_with_the_contract_methods_and_no_gateway_auth(string template, int port, string[] methods)
    {
        var route = Assert.Single(Config().Routes, r => r.UpstreamPathTemplate == template);

        Assert.Equal(template, route.DownstreamPathTemplate);
        Assert.Equal("http", route.DownstreamScheme);
        var target = Assert.Single(route.DownstreamHostAndPorts);
        Assert.Equal("localhost", target.Host);
        Assert.Equal(port, target.Port);
        Assert.Equal(methods.OrderBy(m => m, StringComparer.Ordinal), route.UpstreamHttpMethod.OrderBy(m => m, StringComparer.Ordinal));
        Assert.DoesNotContain("DELETE", route.UpstreamHttpMethod); // neither contract has a DELETE
        Assert.True(string.IsNullOrEmpty(route.AuthenticationOptions?.AuthenticationProviderKey), "authentication belongs to the service");
        Assert.Empty(route.AddHeadersToRequest);
        Assert.Empty(route.UpstreamHeaderTransform);
        Assert.Empty(route.DownstreamHeaderTransform);
        Assert.False(route.RateLimitOptions?.EnableRateLimiting ?? false);
    }

    [Theory]
    [InlineData("/api/quarantine", new[] { "/api/quarantine", "/api/quarantine/{everything}" })]
    [InlineData("/api/v1/esign", new[] { "/api/v1/esign", "/api/v1/esign/{everything}" })]
    public void Only_the_two_new_routes_claim_the_prefix(string prefix, string[] expected)
        => Assert.Equal(expected, Config().Routes.Select(r => r.UpstreamPathTemplate)
            .Where(t => t.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(t => t, StringComparer.Ordinal));

    [Fact]
    public void The_new_blocks_are_the_trace_blocks_with_only_path_port_and_methods_changed_and_follow_them()
    {
        var routes = JsonNode.Parse(OcelotText())!["Routes"]!.AsArray();
        var templates = routes.Select(r => (string?)r!["UpstreamPathTemplate"]).ToList();
        var after = templates.IndexOf("/api/trace/{everything}");
        Assert.Equal(["/api/quarantine", "/api/quarantine/{everything}", "/api/v1/esign", "/api/v1/esign/{everything}"], templates.Skip(after + 1).Take(4));

        foreach (var (trace, path, port, methods) in new[]
                 {
                     ("/api/trace", "/api/quarantine", 5067, QuarantineMethods), ("/api/trace/{everything}", "/api/quarantine/{everything}", 5067, QuarantineMethods),
                     ("/api/trace", "/api/v1/esign", 5057, EsignMethods), ("/api/trace/{everything}", "/api/v1/esign/{everything}", 5057, EsignMethods)
                 })
        {
            var expected = routes.Single(r => (string?)r!["UpstreamPathTemplate"] == trace)!.DeepClone().AsObject();
            expected["DownstreamPathTemplate"] = path;
            expected["UpstreamPathTemplate"] = path;
            expected["DownstreamHostAndPorts"]![0]!["Port"] = port;
            expected["UpstreamHttpMethod"] = new JsonArray(methods.Select(m => (JsonNode)m).ToArray());
            var actual = routes.Single(r => (string?)r!["UpstreamPathTemplate"] == path)!;
            Assert.True(JsonNode.DeepEquals(expected, actual), $"{path} differs from the {trace} block beyond path / port / methods");
        }
    }

    [Fact]
    public void Every_contract_path_and_method_of_quarantine_openapi_is_served_by_exactly_the_quarantine_route()
    {
        var (servers, operations) = Contract("quarantine.openapi.yaml");
        Assert.Equal("/api/quarantine", servers);
        Assert.True(operations.Count >= 24, "contract parse found too few operations: " + operations.Count);
        Assert.All(operations, op =>
        {
            var route = Assert.Single(Matching(servers + op.Path));
            Assert.Equal("/api/quarantine/{everything}", route.UpstreamPathTemplate);
            Assert.Contains(op.Method, route.UpstreamHttpMethod);
        });
    }

    [Fact]
    public void Every_contract_path_and_method_of_esign_openapi_is_served_and_the_internal_ones_are_not()
    {
        var (servers, operations) = Contract("esign.openapi.yaml");
        Assert.Equal("/", servers);
        Assert.True(operations.Count >= 38, "contract parse found too few operations: " + operations.Count);
        var counted = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var op in operations)
        {
            var matched = Matching(op.Path);
            var kind = op.Path.StartsWith("/api/v1/esign/", StringComparison.Ordinal) ? "esign"
                : op.Path.StartsWith("/api/tenant-auth/", StringComparison.Ordinal) ? "tenant-auth"
                : op.Path.StartsWith("/internal/", StringComparison.Ordinal) || op.Path.StartsWith("/api/internal/", StringComparison.Ordinal) ? "internal"
                : throw new Xunit.Sdk.XunitException("unexpected e-signature contract path " + op.Path);
            counted[kind] = counted.GetValueOrDefault(kind) + 1;
            switch (kind)
            {
                case "internal":
                    Assert.True(matched.Count == 0, $"{op.Method} {op.Path} must not be reachable through the gateway");
                    break;
                case "tenant-auth":
                {
                    var route = Assert.Single(matched);
                    Assert.Equal("/api/tenant-auth/{everything}", route.UpstreamPathTemplate);
                    Assert.Equal(5056, Assert.Single(route.DownstreamHostAndPorts).Port);
                    Assert.Contains(op.Method, route.UpstreamHttpMethod);
                    break;
                }
                default:
                {
                    var route = Assert.Single(matched);
                    Assert.Equal("/api/v1/esign/{everything}", route.UpstreamPathTemplate);
                    Assert.Contains(op.Method, route.UpstreamHttpMethod);
                    break;
                }
            }
        }

        Assert.Equal(2, counted["tenant-auth"]);
        Assert.Equal(2, counted["internal"]);
    }

    [Theory]
    [InlineData("/api/v1/workflow/instances", "/api/v1/workflow/{everything}")]
    [InlineData("/api/v1/workflow", "/api/v1/workflow")]
    [InlineData("/api/trace/lots", "/api/trace/{everything}")]
    [InlineData("/api/inventory/movements", "/api/inventory/{everything}")]
    public void Neighbouring_paths_still_reach_their_own_route_only(string path, string template)
        => Assert.Equal(template, Assert.Single(Matching(path)).UpstreamPathTemplate);

    // ── contract parsing + Ocelot-like matching ───────────────────────────────────────────────────────────

    /// <summary>A path matches a template when its literal segments match, <c>{x}</c> takes one segment and a trailing
    /// <c>{everything}</c> takes the (non-empty) rest — Ocelot's placeholder semantics for these templates.</summary>
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
        // Found by name under the docs tree (DocsPathGuard: no hard-coded docs sub-folder). ReadAllLines copes with CRLF.
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
