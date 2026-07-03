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
    // mdm(5059), esbp/delivery-execution/uploads(5004). Adding a new backend is a deliberate, reviewed change to
    // this set — an unrecognized port is far more likely a typo than a new service.
    private static readonly HashSet<int> KnownDownstreamPorts = new() { 5004, 5056, 5057, 5058, 5059 };

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
}
