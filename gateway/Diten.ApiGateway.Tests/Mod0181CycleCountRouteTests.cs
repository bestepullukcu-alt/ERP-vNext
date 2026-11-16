using System.Text.RegularExpressions;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// MOD-0181 DL-6 (AC-45, pack F-9; WP-0181-DL6 r2) — every Cycle Counting endpoint reaches Diten.InventoryService (localhost:5067) through the
/// EXISTING <c>/api/warehouse/{everything}</c> route (Q-15 = A: no route of its own, <c>ocelot.json</c> unchanged). The endpoint list is read
/// from the production source — the <c>[Route]</c> of every controller in the <c>Controllers.CycleCounting</c> namespace plus each action's
/// <c>[HttpX("…")]</c> template (an action template starting with <c>/</c> or <c>~/</c> is absolute and replaces the class route, as in
/// ASP.NET Core) — never typed by hand, and it equals the operation list of the frozen WMS-COUNT-BUNDLE 1.1.0
/// (<c>docs/analysis/contracts/wms-count-bundle.openapi.yaml</c>, server <c>/api/warehouse</c>) in count and in method + path, both ways.
/// Each endpoint is matched by exactly one route, the warehouse catch-all, whose downstream is the same <c>/api/warehouse/…</c> path on
/// localhost:5067 with the method allowed; through the REAL gateway (<see cref="Mod0181CycleCountForwardingTests"/>) each endpoint arrives
/// at http://localhost:5067 with its path and method unchanged and the caller's <c>Authorization</c> header as sent. The MOD-0178 file keeps
/// the overall warehouse count (<see cref="Mod0178WarehouseRouteTests"/>); this file measures the 0181 endpoints only.
/// </summary>
public sealed class Mod0181CycleCountRouteTests
{
    internal const string WarehouseRoute = "/api/warehouse/{everything}";

    [Fact]
    public void The_endpoints_are_read_from_the_CycleCounting_controllers_and_equal_the_frozen_WMS_COUNT_BUNDLE_operations()
    {
        var endpoints = CycleCountEndpoints.FromSource();
        var contract = CycleCountEndpoints.FromContract();

        // DL-1…DL-4: CycleCountsController 13 + CountVariancesController 9 (1.0.0, 22); DL-5: CountEvidenceExportsController 3 (1.1.0, 25)
        Assert.Equal(["CountEvidenceExportsController", "CountVariancesController", "CycleCountsController"],
            endpoints.Select(e => e.Controller).Distinct().Order(StringComparer.Ordinal));
        Assert.Equal(contract.Count, endpoints.Count);
        Assert.Equal(25, endpoints.Count);
        Assert.Equal(endpoints.Count, endpoints.Select(e => e.Key).Distinct().Count()); // no endpoint twice

        var fromCode = endpoints.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        var fromContract = contract.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        var notInContract = fromCode.Except(fromContract).Order(StringComparer.Ordinal).ToList();
        var notServed = fromContract.Except(fromCode).Order(StringComparer.Ordinal).ToList();
        Assert.True(notInContract.Count == 0 && notServed.Count == 0,
            $"served but not in WMS-COUNT-BUNDLE 1.1.0: [{string.Join(", ", notInContract)}]; in the contract but not served: [{string.Join(", ", notServed)}]");
    }

    [Fact]
    public void Every_cycle_count_endpoint_is_served_by_exactly_the_warehouse_route_to_InventoryService_5067_with_its_method()
    {
        var endpoints = CycleCountEndpoints.FromSource();
        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, ep =>
        {
            Assert.StartsWith("/api/warehouse/", ep.Path, StringComparison.Ordinal);
            var route = Assert.Single(Mod0178WarehouseRouteTests.Matching(ep.Path));
            Assert.Equal(WarehouseRoute, route.UpstreamPathTemplate);
            Assert.Equal(WarehouseRoute, route.DownstreamPathTemplate);
            Assert.Equal("http", route.DownstreamScheme);
            var target = Assert.Single(route.DownstreamHostAndPorts);
            Assert.Equal(("localhost", 5067), (target.Host, target.Port));
            Assert.Contains(ep.Method, route.UpstreamHttpMethod);
            // Ocelot carries {everything} over as it came: the downstream path is the upstream path
            var everything = ep.Path["/api/warehouse/".Length..];
            Assert.Equal(ep.Path, route.DownstreamPathTemplate.Replace("{everything}", everything, StringComparison.Ordinal));
        });
    }

    [Fact]
    public void Every_operation_of_the_frozen_contract_falls_on_the_warehouse_route_with_its_method()
    {
        var operations = CycleCountEndpoints.FromContract();
        Assert.NotEmpty(operations);
        Assert.All(operations, op =>
        {
            var route = Assert.Single(Mod0178WarehouseRouteTests.Matching(op.Path));
            Assert.Equal(WarehouseRoute, route.UpstreamPathTemplate);
            Assert.Contains(op.Method, route.UpstreamHttpMethod);
            Assert.Equal(5067, Assert.Single(route.DownstreamHostAndPorts).Port);
        });
    }
}

/// <summary>
/// MOD-0181 DL-6 (AC-45) — every Cycle Counting endpoint through the REAL gateway (<see cref="GatewayProgramHost"/>: its own Program.cs and the
/// shipped ocelot.json next to the binaries; the network is the one stand-in): it reaches http://localhost:5067 with its path and method
/// unchanged and the caller's <c>Authorization</c> header exactly as sent (the token belongs to the service; the gateway passes it on).
/// </summary>
[Collection(GatewayProgramCollection.Name)]
public sealed class Mod0181CycleCountForwardingTests
{
    [Fact]
    public async Task Through_the_real_gateway_every_cycle_count_endpoint_reaches_InventoryService_5067_unchanged_with_its_bearer()
    {
        var endpoints = CycleCountEndpoints.FromSource();
        Assert.Equal(CycleCountEndpoints.FromContract().Count, endpoints.Count);

        await using var gateway = await GatewayProgramHost.StartAsync();
        var client = gateway.Server.CreateClient();
        foreach (var ep in endpoints)
        {
            var bearer = "Bearer service-validates-this-token-" + ep.Method.ToLowerInvariant();
            using var request = new HttpRequestMessage(new HttpMethod(ep.Method), ep.Path);
            request.Headers.TryAddWithoutValidation("Authorization", bearer);
            request.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", "3f2b5a1e-0000-4000-8000-0000000000a1");
            if (ep.Method is "POST" or "PUT") request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request);

            Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK, $"{ep.Key}: the gateway answered {(int)response.StatusCode}");
            var sent = Assert.Single(gateway.Downstream.TakeAll());
            Assert.Equal(ep.Method, sent.Method.Method);
            Assert.Equal("http://localhost:5067" + ep.Path, sent.RequestUri!.ToString());
            Assert.Equal(bearer, Assert.Single(sent.Headers.GetValues("Authorization")));
        }
    }
}

/// <summary>One Cycle Counting endpoint: its controller, method, path with sample ids, and the id-free key (<c>POST /api/warehouse/cycle-counts/{}</c>).</summary>
internal sealed record CycleCountEndpoint(string Controller, string Method, string Path, string Key);

/// <summary>Reads the Cycle Counting endpoints from the production controllers and the operations from the frozen WMS-COUNT-BUNDLE contract.</summary>
internal static class CycleCountEndpoints
{
    private const string ControllerNamespace = "Diten.InventoryService.Api.Controllers.CycleCounting";
    private const string ContractVersion = "1.1.0";

    private static readonly Regex Parameter = new(@"\{[^}]+}", RegexOptions.CultureInvariant);

    /// <summary>The <c>[Route]</c> of every controller in <see cref="ControllerNamespace"/> + each action's <c>[HttpX("…")]</c>.</summary>
    public static List<CycleCountEndpoint> FromSource()
    {
        var controllers = Path.Combine(RepoRoot().FullName, "services", "Diten.InventoryService", "src", "Diten.InventoryService.Api", "Controllers");
        var endpoints = new List<CycleCountEndpoint>();
        foreach (var file in Directory.EnumerateFiles(controllers, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (Regex.Match(text, @"^namespace\s+([\w.]+)\s*;", RegexOptions.Multiline).Groups[1].Value != ControllerNamespace) continue;
            var head = Regex.Match(text, @"\[Route\(""([^""]+)""\)\]\s*(?:\[[^\]]*\]\s*)*public\s+(?:sealed\s+)?class\s+(\w+)");
            Assert.True(head.Success, $"{Path.GetFileName(file)}: a {ControllerNamespace} controller without a class [Route]");
            var classRoute = head.Groups[1].Value.Trim('/');
            foreach (Match action in Regex.Matches(text, @"\[Http(Get|Post|Put|Delete|Patch)(?:\(""([^""]*)""\))?\]"))
            {
                var template = action.Groups[2].Success ? action.Groups[2].Value : "";
                var path = template.StartsWith('/') || template.StartsWith("~/", StringComparison.Ordinal)
                    ? "/" + template.TrimStart('~', '/')                                  // absolute: the class route does not apply
                    : "/" + classRoute + (template.Length > 0 ? "/" + template : "");
                endpoints.Add(Endpoint(head.Groups[2].Value, action.Groups[1].Value.ToUpperInvariant(), path));
            }
        }

        return endpoints;
    }

    /// <summary>The server + every path / method of the frozen <c>wms-count-bundle.openapi.yaml</c> (version 1.1.0, FROZEN).</summary>
    public static List<CycleCountEndpoint> FromContract()
    {
        var file = Path.Combine(RepoRoot().FullName, "docs", "analysis", "contracts", "wms-count-bundle.openapi.yaml");
        var lines = File.ReadAllLines(file);
        Assert.Equal(ContractVersion, Regex.Match(lines.Single(l => l.StartsWith("  version:", StringComparison.Ordinal)), @"version:\s*(\S+)").Groups[1].Value);
        Assert.Contains("  x-status: FROZEN", lines);
        var server = Regex.Match(lines.Single(l => l.StartsWith("servers:", StringComparison.Ordinal)), @"url:\s*([^\s}]+)").Groups[1].Value;
        Assert.Equal("/api/warehouse", server);

        var operations = new List<CycleCountEndpoint>();
        string? path = null;
        foreach (var line in lines.SkipWhile(l => l.TrimEnd() != "paths:").Skip(1).TakeWhile(l => l.TrimEnd() != "components:"))
        {
            var p = Regex.Match(line, @"^  (/[^:]+):\s*$");
            if (p.Success) { path = p.Groups[1].Value; continue; }
            var m = Regex.Match(line, @"^    (get|post|put|delete|patch):");
            if (m.Success && path is not null) operations.Add(Endpoint("contract", m.Groups[1].Value.ToUpperInvariant(), server + path));
        }

        return operations;
    }

    private static CycleCountEndpoint Endpoint(string controller, string method, string template)
    {
        var sample = 0;
        var path = Parameter.Replace(template, _ => $"3f2b5a1e-0000-4000-8000-{++sample:D12}");
        return new CycleCountEndpoint(controller, method, path, method + " " + Parameter.Replace(template, "{}"));
    }

    private static DirectoryInfo RepoRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        return root!;
    }
}
