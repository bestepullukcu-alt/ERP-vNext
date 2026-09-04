using System.Text.Json;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

// FEAT-GATEWAY-OCELOT-TESTS — ocelot.json is the gateway's entire routing surface (84 routes, hand-edited) and had
// zero test coverage: a typo'd port, a missing HTTP method, or a template collision only surfaces as a live 404/405.
// These tests deserialize the SHIPPED ocelot.json into Ocelot's own FileConfiguration model and assert structural
// integrity — no service is started, no HTTP call is made.
public sealed class OcelotConfigurationTests
{
    // Known downstream services as of this test's authoring: auth(5056), platform(5057), dev-enablement(5058),
    // mdm(5059), hcm(5060), pvg(5011), crm(5061), ppm(5062), esbp/delivery-execution/uploads(5004). Adding a new backend is a deliberate, reviewed change to
    // this set — an unrecognized port is far more likely a typo than a new service.
    private static readonly HashSet<int> KnownDownstreamPorts = new() { 5004, 5011, 5056, 5057, 5058, 5059, 5060, 5061, 5062 };

    private static readonly HashSet<string> PpmMethods = new(StringComparer.Ordinal)
    {
        "GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"
    };

    private static FileConfiguration LoadConfiguration()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "ocelot.json");
        Assert.True(File.Exists(path), $"ocelot.json not found at {path} — check the CopyToOutputDirectory link in Diten.ApiGateway.Tests.csproj.");

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config = JsonSerializer.Deserialize<FileConfiguration>(json, options);

        Assert.NotNull(config);
        return config!;
    }

    [Fact]
    public void OcelotJson_DeserializesIntoNonEmptyRouteList()
    {
        var config = LoadConfiguration();

        Assert.NotNull(config.Routes);
        // 84 routes at authoring time; a lower bound catches wholesale config loss without being brittle
        // against legitimate route additions.
        Assert.True(
            config.Routes.Count >= 80,
            $"Expected roughly 84 routes, found {config.Routes.Count}. ocelot.json may have failed to load or lost routes.");
    }

    [Fact]
    public void EveryRoute_HasRequiredFieldsPopulated()
    {
        var config = LoadConfiguration();
        var violations = new List<string>();

        foreach (var route in config.Routes)
        {
            var routeLabel = string.IsNullOrWhiteSpace(route.UpstreamPathTemplate)
                ? $"(downstream: {route.DownstreamPathTemplate})"
                : route.UpstreamPathTemplate;

            if (string.IsNullOrWhiteSpace(route.UpstreamPathTemplate))
            {
                violations.Add($"{routeLabel}: UpstreamPathTemplate is empty");
            }

            if (string.IsNullOrWhiteSpace(route.DownstreamPathTemplate))
            {
                violations.Add($"{routeLabel}: DownstreamPathTemplate is empty");
            }

            if (route.DownstreamHostAndPorts is null || route.DownstreamHostAndPorts.Count == 0)
            {
                violations.Add($"{routeLabel}: DownstreamHostAndPorts has no entries");
            }
            else
            {
                foreach (var hostAndPort in route.DownstreamHostAndPorts)
                {
                    if (string.IsNullOrWhiteSpace(hostAndPort.Host))
                    {
                        violations.Add($"{routeLabel}: a DownstreamHostAndPorts entry has an empty Host");
                    }

                    if (hostAndPort.Port <= 0)
                    {
                        violations.Add($"{routeLabel}: a DownstreamHostAndPorts entry has Port={hostAndPort.Port}");
                    }
                }
            }

            if (route.UpstreamHttpMethod is null || route.UpstreamHttpMethod.Count == 0)
            {
                violations.Add($"{routeLabel}: UpstreamHttpMethod has no entries");
            }
        }

        Assert.True(violations.Count == 0, "Route field violations:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void EveryRoute_DownstreamPortIsInKnownServiceSet()
    {
        var config = LoadConfiguration();
        var violations = new List<string>();

        foreach (var route in config.Routes)
        {
            foreach (var hostAndPort in route.DownstreamHostAndPorts)
            {
                if (!KnownDownstreamPorts.Contains(hostAndPort.Port))
                {
                    violations.Add($"{route.UpstreamPathTemplate}: unrecognized downstream port {hostAndPort.Port} (host {hostAndPort.Host})");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Unrecognized downstream port(s) — likely a typo, unless a new service was deliberately added " +
            "(update KnownDownstreamPorts if so):\n" + string.Join("\n", violations));
    }

    [Fact]
    public void NoTwoRoutes_ShareTheSameUpstreamTemplateAndHttpMethod()
    {
        var config = LoadConfiguration();

        // Ocelot cannot disambiguate two routes that both match the same UpstreamPathTemplate + HTTP method —
        // this exact class of bug happened this session (a GET-only template needed a PUT added).
        var claimants = new Dictionary<(string Template, string Method), List<string>>();

        foreach (var route in config.Routes)
        {
            foreach (var method in route.UpstreamHttpMethod)
            {
                var key = (route.UpstreamPathTemplate, method);
                if (!claimants.TryGetValue(key, out var owners))
                {
                    owners = new List<string>();
                    claimants[key] = owners;
                }

                owners.Add(route.DownstreamPathTemplate);
            }
        }

        var conflicts = claimants
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => $"{kv.Key.Template} [{kv.Key.Method}] claimed by: {string.Join(", ", kv.Value)}")
            .ToList();

        Assert.True(conflicts.Count == 0, "Ambiguous route method claims:\n" + string.Join("\n", conflicts));
    }

    [Fact]
    public void PpmRoutes_HaveExactBaseAndCatchAllContractOnCanonicalPort()
    {
        var config = LoadConfiguration();
        var ppmRoutes = config.Routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/v1/ppm", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(2, ppmRoutes.Length);
        AssertPpmRoute(ppmRoutes, "/api/v1/ppm");
        AssertPpmRoute(ppmRoutes, "/api/v1/ppm/{everything}");
    }

    [Fact]
    public void PpmRouteMutation_WrongPortOrMissingMethodIsRejected()
    {
        var config = LoadConfiguration();
        var ppmRoutes = config.Routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/v1/ppm", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        ppmRoutes[0].DownstreamHostAndPorts[0].Port = 5061;
        Assert.ThrowsAny<Exception>(() => AssertPpmRoute(ppmRoutes, "/api/v1/ppm"));

        ppmRoutes[0].DownstreamHostAndPorts[0].Port = 5062;
        ppmRoutes[0].UpstreamHttpMethod.Remove("OPTIONS");
        Assert.ThrowsAny<Exception>(() => AssertPpmRoute(ppmRoutes, "/api/v1/ppm"));
    }

    private static void AssertPpmRoute(IReadOnlyCollection<FileRoute> ppmRoutes, string template)
    {
        var route = Assert.Single(ppmRoutes, candidate =>
            string.Equals(candidate.UpstreamPathTemplate, template, StringComparison.Ordinal));

        Assert.Equal(template, route.DownstreamPathTemplate);
        Assert.Equal("http", route.DownstreamScheme);
        Assert.Single(route.DownstreamHostAndPorts);
        Assert.Equal("localhost", route.DownstreamHostAndPorts[0].Host);
        Assert.Equal(5062, route.DownstreamHostAndPorts[0].Port);
        Assert.True(PpmMethods.SetEquals(route.UpstreamHttpMethod),
            $"Unexpected PPM methods for {template}: {string.Join(",", route.UpstreamHttpMethod)}");
    }

    public static IEnumerable<object[]> CriticalRoutes()
    {
        // Live-verified flows from this session. Method presence only — downstream wiring is out of scope.
        yield return new object[] { "/api/platform/navigation/{everything}", "PUT" };
        yield return new object[] { "/api/platform/navigation/{everything}", "GET" };
        yield return new object[] { "/api/admin/tenants", "GET" };
        yield return new object[] { "/api/admin/tenants/{everything}", "GET" };
        yield return new object[] { "/api/tenant-auth/{everything}", "POST" }; // covers tenant-auth login
        yield return new object[] { "/api/roles/{everything}", "GET" };
        yield return new object[] { "/api/permissions/{everything}", "GET" }; // internal permission sync surface
        yield return new object[] { "/api/pv-case-intake-triage", "GET" };
        yield return new object[] { "/api/pv-case-intake-triage", "POST" };
        yield return new object[] { "/api/pv-case-intake-triage/{intakeDraftId}", "GET" };
        yield return new object[] { "/api/pv-case-intake-triage/{intakeDraftId}", "PUT" };
        yield return new object[] { "/api/pv-case-intake-triage/{intakeDraftId}/triage", "POST" };
        yield return new object[] { "/api/pv-case-intake-triage/{intakeDraftId}/route", "POST" };
    }

    [Theory]
    [MemberData(nameof(CriticalRoutes))]
    public void CriticalRoute_ExistsWithExpectedMethod(string upstreamTemplate, string requiredMethod)
    {
        var config = LoadConfiguration();

        var route = config.Routes.FirstOrDefault(r => r.UpstreamPathTemplate == upstreamTemplate);
        Assert.True(route is not null, $"Expected route '{upstreamTemplate}' not found in ocelot.json.");
        Assert.Contains(requiredMethod, route!.UpstreamHttpMethod);
    }

    [Fact]
    public void PvgCaseIntakeTriage_RouteFamilyMapsOnlyApprovedTemplates()
    {
        var config = LoadConfiguration();

        var pvgRoutes = config.Routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/pv-case-intake-triage", StringComparison.Ordinal))
            .ToArray();

        var expectedRoutes = new Dictionary<string, (string DownstreamTemplate, string[] Methods)>
        {
            ["/api/pv-case-intake-triage"] = ("/api/v1/pv-case-intake-triage", new[] { "GET", "POST" }),
            ["/api/pv-case-intake-triage/{intakeDraftId}"] = ("/api/v1/pv-case-intake-triage/{intakeDraftId}", new[] { "GET", "PUT" }),
            ["/api/pv-case-intake-triage/{intakeDraftId}/triage"] = ("/api/v1/pv-case-intake-triage/{intakeDraftId}/triage", new[] { "POST" }),
            ["/api/pv-case-intake-triage/{intakeDraftId}/route"] = ("/api/v1/pv-case-intake-triage/{intakeDraftId}/route", new[] { "POST" })
        };

        Assert.Equal(expectedRoutes.Count, pvgRoutes.Length);

        foreach (var route in pvgRoutes)
        {
            Assert.True(
                expectedRoutes.TryGetValue(route.UpstreamPathTemplate, out var expectedRoute),
                $"Unexpected PVG route template: {route.UpstreamPathTemplate}");
            Assert.Equal(expectedRoute.DownstreamTemplate, route.DownstreamPathTemplate);
            Assert.Equal(
                expectedRoute.Methods.OrderBy(method => method, StringComparer.Ordinal),
                route.UpstreamHttpMethod.OrderBy(method => method, StringComparer.Ordinal));
            Assert.Single(route.DownstreamHostAndPorts);
            Assert.Equal("localhost", route.DownstreamHostAndPorts[0].Host);
            Assert.Equal(5011, route.DownstreamHostAndPorts[0].Port);
            Assert.DoesNotContain("PATCH", route.UpstreamHttpMethod);
            Assert.DoesNotContain("DELETE", route.UpstreamHttpMethod);
            Assert.DoesNotContain("OPTIONS", route.UpstreamHttpMethod);
            Assert.DoesNotContain("{everything}", route.UpstreamPathTemplate, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("{everything}", route.DownstreamPathTemplate, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("delete", route.UpstreamPathTemplate, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("bulk-delete", route.UpstreamPathTemplate, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("archive", route.UpstreamPathTemplate, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("void", route.UpstreamPathTemplate, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("export", route.UpstreamPathTemplate, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("meddra", route.UpstreamPathTemplate, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ai", route.UpstreamPathTemplate, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PvgCaseIntakeTriage_ForbiddenRoutesAreAbsent()
    {
        var config = LoadConfiguration();

        var pvgRoutes = config.Routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/pv-case-intake-triage", StringComparison.Ordinal))
            .ToArray();

        var forbiddenMethods = new[] { "PATCH", "DELETE", "OPTIONS" };
        var forbiddenPathSegments = new[] { "{everything}", "export", "archive", "void", "bulk", "bulk-delete", "meddra", "ai" };

        var violations = new List<string>();
        foreach (var route in pvgRoutes)
        {
            foreach (var method in forbiddenMethods)
            {
                if (route.UpstreamHttpMethod.Contains(method))
                {
                    violations.Add($"{route.UpstreamPathTemplate}: forbidden method {method}");
                }
            }

            foreach (var segment in forbiddenPathSegments)
            {
                if (route.UpstreamPathTemplate.Contains(segment, StringComparison.OrdinalIgnoreCase) ||
                    route.DownstreamPathTemplate.Contains(segment, StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"{route.UpstreamPathTemplate}: forbidden path segment {segment}");
                }
            }
        }

        Assert.True(violations.Count == 0, "Forbidden PVG Gateway routes:\n" + string.Join("\n", violations));
    }
}
