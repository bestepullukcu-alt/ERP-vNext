using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// MOD-0178 DL-8 (INT-WMS, pack §15, AC-25): <c>/api/warehouse</c> + <c>/api/warehouse/{everything}</c> → Diten.InventoryService
/// (localhost:5067). The INT-RECALL blocks (<see cref="MvpOneRecallRouteTests"/>) cloned with the path changed and DELETE added (the
/// location-role delete; pack §15: GET / POST / PUT / DELETE / OPTIONS, no PATCH): authentication in the service (no gateway
/// AuthenticationOptions), headers passed through unchanged, no rate limit. Contract direction: every endpoint the InventoryService
/// warehouse controllers serve (read from the production source) and every path + method of <c>warehouse-outbound.openapi.yaml</c> is
/// matched by EXACTLY ONE route; PATCH by none; prefix look-alikes (<c>/api/warehouseX</c>) never reach the warehouse blocks; the
/// neighbours keep their own routes. The blocks follow <c>/api/recall/{everything}</c> (end of the MVP-1 InventoryService group). The
/// forwarded headers are measured through the real gateway in <see cref="Mod0178WarehouseHeaderForwardingTests"/>.
/// </summary>
public sealed class Mod0178WarehouseRouteTests
{
    private static readonly string[] WarehouseMethods = ["GET", "POST", "PUT", "DELETE", "OPTIONS"];

    private static string OcelotText() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ocelot.json"));

    private static FileConfiguration Config() => JsonSerializer.Deserialize<FileConfiguration>(
        OcelotText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    [Theory]
    [InlineData("/api/warehouse")]
    [InlineData("/api/warehouse/{everything}")]
    public void The_route_goes_to_InventoryService_5067_with_the_pack_methods_and_no_gateway_auth(string template)
    {
        var route = Assert.Single(Config().Routes, r => r.UpstreamPathTemplate == template);

        Assert.Equal(template, route.DownstreamPathTemplate);
        Assert.Equal("http", route.DownstreamScheme);
        var target = Assert.Single(route.DownstreamHostAndPorts);
        Assert.Equal("localhost", target.Host);
        Assert.Equal(5067, target.Port);
        Assert.Equal(WarehouseMethods.OrderBy(m => m, StringComparer.Ordinal), route.UpstreamHttpMethod.OrderBy(m => m, StringComparer.Ordinal));
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
        => Assert.Equal(["/api/warehouse", "/api/warehouse/{everything}"], Config().Routes.Select(r => r.UpstreamPathTemplate)
            .Where(t => t.StartsWith("/api/warehouse", StringComparison.Ordinal)).OrderBy(t => t, StringComparer.Ordinal));

    [Fact]
    public void The_new_blocks_are_the_recall_blocks_with_the_path_changed_and_DELETE_added_and_follow_them()
    {
        var routes = JsonNode.Parse(OcelotText())!["Routes"]!.AsArray();
        var templates = routes.Select(r => (string?)r!["UpstreamPathTemplate"]).ToList();
        var after = templates.IndexOf("/api/recall/{everything}");
        Assert.Equal(["/api/warehouse", "/api/warehouse/{everything}"], templates.Skip(after + 1).Take(2));

        foreach (var (recall, path) in new[] { ("/api/recall", "/api/warehouse"), ("/api/recall/{everything}", "/api/warehouse/{everything}") })
        {
            var expected = routes.Single(r => (string?)r!["UpstreamPathTemplate"] == recall)!.DeepClone().AsObject();
            expected["DownstreamPathTemplate"] = path;
            expected["UpstreamPathTemplate"] = path;
            expected["UpstreamHttpMethod"] = new JsonArray("GET", "POST", "PUT", "DELETE", "OPTIONS");
            var actual = routes.Single(r => (string?)r!["UpstreamPathTemplate"] == path)!;
            Assert.True(JsonNode.DeepEquals(expected, actual), $"{path} differs from the {recall} block beyond the path and DELETE");
        }
    }

    [Fact]
    public void Every_endpoint_of_the_InventoryService_warehouse_controllers_is_served_by_exactly_the_warehouse_route()
    {
        var endpoints = ControllerEndpoints();
        // S1: 14 putaway + 6 location-role + 2 frozen outbound (22). DL-4 (pack AC-74, WMS draft §11): + 6 outbound — POST /outbound-shipments,
        // POST /outbound-shipments/allocation-preview, GET /outbound-shipments/search, GET /outbound-shipments/{outboundId}/view,
        // PUT /outbound-shipments/{outboundId}, GET /outbound-shipments/{outboundId}/events (28). DL-5 (pack AC-74, WMS draft §11): + 17 —
        // POST /outbound-shipments/{outboundId}/cancel | /release | /sync-dispatch, GET + POST /outbound-shipments/{outboundId}/packages,
        // PUT /packages/{packageId}, POST /packages/{packageId}/close, GET /pick-tasks, GET /pick-tasks/{taskId}, POST /pick-tasks/bulk-assign,
        // POST /pick-tasks/{taskId}/claim | /confirmations | /confirmations/{seq}/retry | /returned | /resolve-integrity | /close-short,
        // GET /pick-tasks/{taskId}/events (45). DL-7 adds its 3 S3 reads here (48).
        // MOD-0181 DL-1 (Q-15 = A: the count endpoints live under /api/warehouse; XMC-001 Beste 2026-11-09): + 9 — GET + POST /cycle-counts,
        // POST /cycle-counts/bulk-delete, GET /cycle-counts/{countId}, POST /cycle-counts/{countId}/update | /release | /cancel,
        // GET /cycle-counts/{countId}/lines | /events (54). MOD-0181 DL-2 (XMC-001, Beste 2026-11-10): + 3 — POST /cycle-counts/{countId}/lines,
        // POST /cycle-counts/{countId}/lines/{lineId}/count, POST /cycle-counts/{countId}/submit (57). Their own coverage test is DL-6's
        // Mod0181CycleCountRouteTests. MOD-0178 DL-7 (pack AC-74, WMS draft §9): + 3 S3 reads — GET /exceptions, GET /activity,
        // GET /integration/status (60). MOD-0181 DL-3 (XMC-001, owner's OK Beste 2026-11-10 "69 yap"): + 9 — GET /count-variances,
        // GET /count-variances/{lineId}, POST /count-variances/{lineId}/approve | /reject | /retry | /resolve-integrity,
        // POST /cycle-counts/{countId}/recount, GET + PUT /count-settings (69). MOD-0181 DL-4 (XMC-001, owner's OK Beste 2026-11-11 "70 yap"):
        // + 1 — GET /cycle-counts/dashboard (70).
        Assert.Equal(70, endpoints.Count);
        Assert.All(endpoints, ep =>
        {
            var route = Assert.Single(Matching(ep.Path));
            Assert.Equal("/api/warehouse/{everything}", route.UpstreamPathTemplate);
            Assert.Contains(ep.Method, route.UpstreamHttpMethod);
            Assert.Equal(5067, Assert.Single(route.DownstreamHostAndPorts).Port);
        });
        // pack §15: DELETE is meaningful only on location-roles — the route allows it everywhere, the service serves it there only
        Assert.All(endpoints.Where(ep => ep.Method == "DELETE"),
            ep => Assert.StartsWith("/api/warehouse/location-roles/", ep.Path, StringComparison.Ordinal));
        Assert.DoesNotContain(endpoints, ep => ep.Method == "PATCH");
    }

    [Fact]
    public void Every_path_and_method_of_warehouse_outbound_openapi_is_served_by_exactly_the_warehouse_route()
    {
        var (servers, operations) = Contract("warehouse-outbound.openapi.yaml");
        Assert.Equal("/api/warehouse", servers);
        Assert.Equal(2, operations.Count); // listOutboundShipments, getOutboundShipment (frozen 1.0.0; S1 empty list / 404)
        Assert.All(operations, op =>
        {
            var route = Assert.Single(Matching(servers + op.Path));
            Assert.Equal("/api/warehouse/{everything}", route.UpstreamPathTemplate);
            Assert.Contains(op.Method, route.UpstreamHttpMethod);
            Assert.Equal(5067, Assert.Single(route.DownstreamHostAndPorts).Port);
        });
    }

    [Theory]
    [InlineData("/api/warehouse/putaway-tasks", "GET")]
    [InlineData("/api/warehouse/putaway-tasks", "POST")]
    [InlineData("/api/warehouse/putaway-tasks/3f2b5a1e-0000-4000-8000-000000000001", "PUT")]
    [InlineData("/api/warehouse/putaway-tasks/3f2b5a1e-0000-4000-8000-000000000001/confirmations", "POST")]
    [InlineData("/api/warehouse/putaway-tasks/3f2b5a1e-0000-4000-8000-000000000001/confirmations/2/retry", "POST")]
    [InlineData("/api/warehouse/putaway-candidates", "GET")]
    [InlineData("/api/warehouse/location-roles/3f2b5a1e-0000-4000-8000-000000000002", "DELETE")]
    [InlineData("/api/warehouse/location-roles/bulk-delete", "POST")]
    [InlineData("/api/warehouse/outbound-shipments", "GET")]
    // DL-4 (S2 outbound, WMS draft §11)
    [InlineData("/api/warehouse/outbound-shipments", "POST")]
    [InlineData("/api/warehouse/outbound-shipments/allocation-preview", "POST")]
    [InlineData("/api/warehouse/outbound-shipments/search", "GET")]
    [InlineData("/api/warehouse/outbound-shipments/3f2b5a1e-0000-4000-8000-000000000003/view", "GET")]
    [InlineData("/api/warehouse/outbound-shipments/3f2b5a1e-0000-4000-8000-000000000003", "PUT")]
    [InlineData("/api/warehouse/outbound-shipments/3f2b5a1e-0000-4000-8000-000000000003/events", "GET")]
    // DL-5 (S2 pick / pack / ship / cancel, WMS draft §11)
    [InlineData("/api/warehouse/outbound-shipments/3f2b5a1e-0000-4000-8000-000000000003/cancel", "POST")]
    [InlineData("/api/warehouse/outbound-shipments/3f2b5a1e-0000-4000-8000-000000000003/release", "POST")]
    [InlineData("/api/warehouse/outbound-shipments/3f2b5a1e-0000-4000-8000-000000000003/sync-dispatch", "POST")]
    [InlineData("/api/warehouse/outbound-shipments/3f2b5a1e-0000-4000-8000-000000000003/packages", "GET")]
    [InlineData("/api/warehouse/outbound-shipments/3f2b5a1e-0000-4000-8000-000000000003/packages", "POST")]
    [InlineData("/api/warehouse/packages/3f2b5a1e-0000-4000-8000-000000000004", "PUT")]
    [InlineData("/api/warehouse/packages/3f2b5a1e-0000-4000-8000-000000000004/close", "POST")]
    [InlineData("/api/warehouse/pick-tasks", "GET")]
    [InlineData("/api/warehouse/pick-tasks/3f2b5a1e-0000-4000-8000-000000000005", "GET")]
    [InlineData("/api/warehouse/pick-tasks/bulk-assign", "POST")]
    [InlineData("/api/warehouse/pick-tasks/3f2b5a1e-0000-4000-8000-000000000005/claim", "POST")]
    [InlineData("/api/warehouse/pick-tasks/3f2b5a1e-0000-4000-8000-000000000005/confirmations", "POST")]
    [InlineData("/api/warehouse/pick-tasks/3f2b5a1e-0000-4000-8000-000000000005/confirmations/1/retry", "POST")]
    [InlineData("/api/warehouse/pick-tasks/3f2b5a1e-0000-4000-8000-000000000005/confirmations/1/returned", "POST")]
    [InlineData("/api/warehouse/pick-tasks/3f2b5a1e-0000-4000-8000-000000000005/confirmations/1/resolve-integrity", "POST")]
    [InlineData("/api/warehouse/pick-tasks/3f2b5a1e-0000-4000-8000-000000000005/close-short", "POST")]
    [InlineData("/api/warehouse/pick-tasks/3f2b5a1e-0000-4000-8000-000000000005/events", "GET")]
    // DL-7 (S3 monitoring reads, WMS draft §9)
    [InlineData("/api/warehouse/exceptions?kind=DispatchNotSeen", "GET")]
    [InlineData("/api/warehouse/activity?from=2026-11-01T00:00:00Z&to=2026-11-10T00:00:00Z", "GET")]
    [InlineData("/api/warehouse/integration/status", "GET")]
    public void Named_operations_reach_the_warehouse_route_with_their_method(string path, string method)
    {
        var route = Assert.Single(Matching(path));
        Assert.Equal("/api/warehouse/{everything}", route.UpstreamPathTemplate);
        Assert.Contains(method, route.UpstreamHttpMethod);
    }

    [Theory]
    [InlineData("/api/warehouse/putaway-tasks")]
    [InlineData("/api/warehouse/location-roles/3f2b5a1e-0000-4000-8000-000000000002")]
    [InlineData("/api/warehouse")]
    public void Patch_is_matched_by_no_route(string path)
        => Assert.All(Matching(path), r => Assert.DoesNotContain("PATCH", r.UpstreamHttpMethod));

    [Theory]
    [InlineData("/api/warehouseX")]
    [InlineData("/api/warehouseX/putaway-tasks")]
    [InlineData("/api/warehouse-old/putaway-tasks")]
    [InlineData("/api/warehouses/putaway-tasks")]
    public void Prefix_look_alikes_never_reach_the_warehouse_blocks(string path)
        => Assert.DoesNotContain(Matching(path), r => r.UpstreamPathTemplate.StartsWith("/api/warehouse", StringComparison.Ordinal));

    [Theory]
    [InlineData("/api/recall/settings", "/api/recall/{everything}", 5067)]
    [InlineData("/api/recall", "/api/recall", 5067)]
    [InlineData("/api/expiry/policy", "/api/expiry/{everything}", 5067)]
    [InlineData("/api/locations/tree", "/api/locations/{everything}", 5067)]
    [InlineData("/api/inventory/movements", "/api/inventory/{everything}", 5067)]
    [InlineData("/api/personalization/views", "/api/personalization/views", 5057)]
    public void Neighbouring_paths_still_reach_their_own_route_only(string path, string template, int port)
    {
        var route = Assert.Single(Matching(path));
        Assert.Equal(template, route.UpstreamPathTemplate);
        Assert.Equal(port, Assert.Single(route.DownstreamHostAndPorts).Port);
    }

    // ── controller / contract parsing + Ocelot-like matching (the INT-QE / INT-RECALL helpers) ──────────────

    internal static List<FileRoute> Matching(string path)
        => Config().Routes.Where(r =>
        {
            var pattern = Regex.Escape(r.UpstreamPathTemplate).Replace(@"\{everything}", ".+");
            pattern = Regex.Replace(pattern, @"\\\{[^}/]+}", "[^/]+");
            return Regex.IsMatch(path, "^" + pattern + "$");
        }).ToList();

    private static DirectoryInfo RepoRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        return root!;
    }

    /// <summary>The class <c>[Route]</c> + each action's <c>[HttpX("…")]</c> of every InventoryService controller under <c>api/warehouse</c>.</summary>
    private static List<(string Path, string Method)> ControllerEndpoints()
    {
        var controllers = Path.Combine(RepoRoot().FullName, "services", "Diten.InventoryService", "src", "Diten.InventoryService.Api", "Controllers");
        var endpoints = new List<(string Path, string Method)>();
        foreach (var file in Directory.EnumerateFiles(controllers, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var classRoute = Regex.Match(text, @"\[Route\(""([^""]+)""\)\]\s*(?:\[[^\]]*\]\s*)*public\s+(?:sealed\s+)?class");
            if (!classRoute.Success || !classRoute.Groups[1].Value.StartsWith("api/warehouse", StringComparison.Ordinal)) continue;
            foreach (Match action in Regex.Matches(text, @"\[Http(Get|Post|Put|Delete|Patch)(?:\(""([^""]*)""\))?\]"))
            {
                var template = "/" + classRoute.Groups[1].Value + (action.Groups[2].Success && action.Groups[2].Value.Length > 0 ? "/" + action.Groups[2].Value : "");
                endpoints.Add((Regex.Replace(template, @"\{[^}]+}", "x"), action.Groups[1].Value.ToUpperInvariant()));
            }
        }

        return endpoints;
    }

    private static (string Servers, List<(string Path, string Method)> Operations) Contract(string fileName)
    {
        var lines = File.ReadAllLines(Directory.EnumerateFiles(Path.Combine(RepoRoot().FullName, "docs"), fileName, SearchOption.AllDirectories).Single());
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

/// <summary>
/// MOD-0178 DL-8 (AC-25, pack §15) — the warehouse headers measured through the REAL gateway (<see cref="GatewayProgramHost"/>: its own
/// Program.cs and the shipped ocelot.json; the network is the one stand-in). Each S1 method reaches localhost:5067 with its path and query
/// unchanged and with <c>Authorization</c>, <c>X-Legal-Entity-Id</c>, <c>Idempotency-Key</c> and <c>If-Match</c> exactly as the caller sent
/// them; <c>X-Correlation-Id</c> is the id the gateway minted (E-1), the caller's own value travelling apart as <c>X-Client-Correlation-Id</c>.
/// The bearer is not one the gateway can verify: the token belongs to the service, and the gateway passes it on untouched.
/// </summary>
[Collection(GatewayProgramCollection.Name)]
public sealed class Mod0178WarehouseHeaderForwardingTests
{
    [Theory]
    [InlineData("GET", "/api/warehouse/putaway-candidates?warehouseId=3f2b5a1e-0000-4000-8000-0000000000aa&page=2")]
    [InlineData("POST", "/api/warehouse/putaway-tasks/3f2b5a1e-0000-4000-8000-000000000001/confirmations")]
    [InlineData("PUT", "/api/warehouse/putaway-tasks/3f2b5a1e-0000-4000-8000-000000000001")]
    [InlineData("DELETE", "/api/warehouse/location-roles/3f2b5a1e-0000-4000-8000-000000000002")]
    public async Task Through_the_real_gateway_the_warehouse_headers_reach_InventoryService_unchanged(string method, string pathAndQuery)
    {
        await using var gateway = await GatewayProgramHost.StartAsync();
        var client = gateway.Server.CreateClient();

        const string bearer = "Bearer service-validates-this-token";
        const string legalEntity = "3f2b5a1e-0000-4000-8000-0000000000a1";
        const string idempotencyKey = "wms-dl8-idem-0001";
        const string ifMatch = "W/\"3\"";
        using var request = new HttpRequestMessage(new HttpMethod(method), pathAndQuery);
        request.Headers.TryAddWithoutValidation("Authorization", bearer);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", "3f2b5a1e-0000-4000-8000-0000000000b2");
        request.Headers.TryAddWithoutValidation("X-Legal-Entity-Id", legalEntity);
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", "caller-chosen");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        if (method is "POST" or "PUT") request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var sent = Assert.Single(gateway.Downstream.TakeAll());
        Assert.Equal(method, sent.Method.Method);
        Assert.Equal("http://localhost:5067" + pathAndQuery, sent.RequestUri!.ToString());
        Assert.Equal(bearer, Assert.Single(sent.Headers.GetValues("Authorization")));
        Assert.Equal(legalEntity, Assert.Single(sent.Headers.GetValues("X-Legal-Entity-Id")));
        Assert.Equal(idempotencyKey, Assert.Single(sent.Headers.GetValues("Idempotency-Key")));
        Assert.Equal(ifMatch, Assert.Single(sent.Headers.GetValues("If-Match")));
        Assert.Equal(response.Headers.GetValues("X-Correlation-Id").Single(), Assert.Single(sent.Headers.GetValues("X-Correlation-Id")));
        Assert.Equal("caller-chosen", Assert.Single(sent.Headers.GetValues("X-Client-Correlation-Id")));
    }
}
