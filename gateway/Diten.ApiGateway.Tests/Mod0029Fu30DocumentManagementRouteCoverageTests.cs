using System.Text.Json;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// MOD-0029-FU30 — Gateway Route Integration. Every MOD-0029 Document Control governance controller routes under the
/// <c>/api/v1/document-management</c> family (verified: base route <c>api/v1/document-management</c>, plus the nested
/// families <c>.../repository-downtime-events</c> and <c>.../template-variants</c>), so the shipped ocelot.json's
/// catch-all <c>/api/v1/document-management/{everything}</c> (Ocelot's <c>{everything}</c> matches multi-segment paths)
/// already forwards ALL FU06–FU23 endpoints to the Platform service (5057). These tests pin that coverage so a future
/// narrowing/removal of the catch-all — or a wrong downstream port — fails here instead of as a live 404/405.
/// No route was added: the catch-all is complete. No service is started; the shipped ocelot.json is parsed only.
/// </summary>
public sealed class Mod0029Fu30DocumentManagementRouteCoverageTests
{
    private const string DmCatchAll = "/api/v1/document-management/{everything}";
    private const string DmBase = "/api/v1/document-management";
    private const int PlatformPort = 5057;

    private static FileConfiguration LoadConfiguration()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ocelot.json");
        Assert.True(File.Exists(path), $"ocelot.json not found at {path}.");
        var config = JsonSerializer.Deserialize<FileConfiguration>(
            File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(config);
        return config!;
    }

    // ── 1 / 23 — the catch-all exists, is valid, and carries every verb the governance controllers use ──
    [Fact]
    public void Document_management_catch_all_route_exists_with_all_verbs()
    {
        var config = LoadConfiguration();
        var route = config.Routes.FirstOrDefault(r => r.UpstreamPathTemplate == DmCatchAll);

        Assert.True(route is not null, $"Catch-all '{DmCatchAll}' not found in ocelot.json.");
        foreach (var verb in new[] { "GET", "POST", "PUT", "PATCH", "DELETE" })
        {
            Assert.Contains(verb, route!.UpstreamHttpMethod);
        }
    }

    [Fact]
    public void Document_management_base_route_exists()
    {
        var config = LoadConfiguration();
        Assert.Contains(config.Routes, r => r.UpstreamPathTemplate == DmBase);
    }

    // ── 2 — routes forward document-management to the Platform service, path preserved ──────────────────
    [Fact]
    public void Document_management_routes_forward_to_platform_service_preserving_path()
    {
        var config = LoadConfiguration();
        foreach (var template in new[] { DmBase, DmCatchAll })
        {
            var route = config.Routes.First(r => r.UpstreamPathTemplate == template);
            Assert.Equal(template, route.DownstreamPathTemplate); // path preserved (no rewrite)
            Assert.All(route.DownstreamHostAndPorts, hp => Assert.Equal(PlatformPort, hp.Port));
        }
    }

    // ── 3–19 — every FU06–FU23 governance path group is reachable through the gateway ──────────────────
    public static IEnumerable<object[]> GovernancePaths()
    {
        // Representative real upstream paths (each under /api/v1/document-management/…). GET + POST asserted per group.
        var paths = new[]
        {
            "/api/v1/document-management/document-master-register",                    // FU06 master register
            "/api/v1/document-management/document-master-register/summary",
            "/api/v1/document-management/document-identifiers",                         // FU07 identifiers
            "/api/v1/document-management/document-master-register/x/allocate-uid",
            "/api/v1/document-management/document-master-register/x/lifecycle",         // FU08 lifecycle
            "/api/v1/document-management/document-master-register/x/approval-requirements", // FU09 approval routes
            "/api/v1/document-management/document-master-register/x/release-gates",     // FU10 release gates
            "/api/v1/document-management/document-master-register/x/training-readiness", // FU11 training
            "/api/v1/document-management/document-master-register/x/periodic-review",   // FU12 periodic reviews
            "/api/v1/document-management/document-master-register/x/suspension-cases",  // FU13 suspensions
            "/api/v1/document-management/document-master-register/x/retirement-cases",  // FU13 retirements
            "/api/v1/document-management/document-master-register/x/temporary-instruction", // FU13 temporary instructions
            "/api/v1/document-management/repository-assessments",                       // FU16 repository assessments
            "/api/v1/document-management/document-master-register/x/controlled-copies", // FU17 controlled copies
            "/api/v1/document-management/external-documents",                           // FU14 external documents
            "/api/v1/document-management/retention-policies",                           // FU15 retention
            "/api/v1/document-management/legal-holds",                                  // FU15 legal holds
            "/api/v1/document-management/disposition-requests",                         // FU15 disposition
            "/api/v1/document-management/template-variants/x/localization-profile",     // FU18 variant localization
            "/api/v1/document-management/repository-downtime-events",                   // FU20 downtime
            "/api/v1/document-management/gdocp-corrections",                            // FU21 gdocp corrections
            "/api/v1/document-management/gdocp-correction-policies",
            "/api/v1/document-management/quality-events",                               // FU22 quality events
            "/api/v1/document-management/deviations",                                   // FU22 deviations
            "/api/v1/document-management/capa-actions",                                 // FU22 CAPA
            "/api/v1/document-management/signatures",                                   // FU23 signatures
            "/api/v1/document-management/signature-policies",
            "/api/v1/document-management/signature-requests",
        };

        return paths.Select(p => new object[] { p });
    }

    [Theory]
    [MemberData(nameof(GovernancePaths))]
    public void Governance_path_is_reachable_through_gateway_for_get_and_post(string path)
    {
        var config = LoadConfiguration();
        Assert.True(IsCovered(config, path, "GET"), $"No gateway route covers GET {path}.");
        Assert.True(IsCovered(config, path, "POST"), $"No gateway route covers POST {path}.");
        Assert.Equal(PlatformPort, CoveringPort(config, path, "GET"));
    }

    // ── 20 — no route pins a direct 5057 in a way the client would call; the gateway is the only client-facing hop ──
    [Fact]
    public void Document_management_downstream_is_the_only_5057_surface_and_upstream_is_gateway_relative()
    {
        var config = LoadConfiguration();
        // Upstream (client-facing) templates never expose a port; downstream (server-side) is the Platform 5057 host.
        foreach (var route in DmRoutes(config))
        {
            Assert.DoesNotContain(":5057", route.UpstreamPathTemplate);
            Assert.All(route.DownstreamHostAndPorts, hp => Assert.Equal(PlatformPort, hp.Port));
        }
    }

    // ── 21 — the gateway must not inject a client TenantId / X-Tenant-Id on document-management routes ──
    [Fact]
    public void Document_management_routes_do_not_inject_tenant_headers()
    {
        var config = LoadConfiguration();
        foreach (var route in DmRoutes(config))
        {
            if (route.UpstreamHeaderTransform is { Count: > 0 })
            {
                Assert.DoesNotContain(route.UpstreamHeaderTransform.Keys,
                    k => k.Contains("tenant", StringComparison.OrdinalIgnoreCase));
            }

            if (route.AddHeadersToRequest is { Count: > 0 })
            {
                Assert.DoesNotContain(route.AddHeadersToRequest.Keys,
                    k => k.Contains("tenant", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    // ── 24 — no duplicate/conflicting document-management routes (same upstream template + verb) ────────
    [Fact]
    public void No_duplicate_conflicting_document_management_routes()
    {
        var config = LoadConfiguration();
        var claims = new Dictionary<(string, string), int>();
        foreach (var route in DmRoutes(config))
        {
            foreach (var method in route.UpstreamHttpMethod)
            {
                var key = (route.UpstreamPathTemplate, method);
                claims[key] = claims.TryGetValue(key, out var c) ? c + 1 : 1;
            }
        }

        var conflicts = claims.Where(kv => kv.Value > 1).Select(kv => $"{kv.Key.Item1} [{kv.Key.Item2}] x{kv.Value}").ToList();
        Assert.True(conflicts.Count == 0, "Conflicting document-management routes:\n" + string.Join("\n", conflicts));
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────
    private static IEnumerable<FileRoute> DmRoutes(FileConfiguration config) =>
        config.Routes.Where(r => r.UpstreamPathTemplate is not null
            && r.UpstreamPathTemplate.StartsWith(DmBase, StringComparison.OrdinalIgnoreCase));

    private static bool IsCovered(FileConfiguration config, string path, string method) =>
        config.Routes.Any(r => Matches(r, path, method));

    private static int? CoveringPort(FileConfiguration config, string path, string method) =>
        config.Routes.FirstOrDefault(r => Matches(r, path, method))?.DownstreamHostAndPorts.FirstOrDefault()?.Port;

    private static bool Matches(FileRoute route, string path, string method)
    {
        if (route.UpstreamPathTemplate is null
            || !route.UpstreamHttpMethod.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var template = route.UpstreamPathTemplate;
        if (string.Equals(template, path, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Ocelot {everything} placeholder is a greedy, multi-segment catch-all: "/prefix/{everything}" matches any
        // deeper "/prefix/…". A trailing placeholder anywhere in the template collapses to its literal prefix.
        var idx = template.IndexOf("/{", StringComparison.Ordinal);
        if (idx > 0)
        {
            var prefix = template[..idx];
            if (path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
