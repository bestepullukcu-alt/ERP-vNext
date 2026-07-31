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
    // mdm(5059), hcm(5060), ppm(5061), esbp/delivery-execution/uploads(5004). Adding a new backend is a deliberate, reviewed change to
    // this set — an unrecognized port is far more likely a typo than a new service.
    private static readonly HashSet<int> KnownDownstreamPorts = new() { 5004, 5056, 5057, 5058, 5059, 5060, 5061 };

    private static readonly HashSet<string> PpmMethods = new(StringComparer.Ordinal)
    {
        "GET", "POST", "PUT", "DELETE", "OPTIONS"
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

        AssertNoRouteMethodCollisions(config);
    }

    private static void AssertNoRouteMethodCollisions(FileConfiguration config)
    {
        // Ocelot cannot disambiguate two routes that both match the same UpstreamPathTemplate + HTTP method —
        // this exact class of bug happened this session (a GET-only template needed a PUT added).
        var claimants = new Dictionary<(string Template, string Method), List<string>>(
            new RouteMethodClaimComparer());

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
    public void PpmRoutes_HaveExactBaseAndCatchAllContract()
    {
        var config = LoadConfiguration();

        AssertPpmContract(config);

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ocelot.json")));
        var rawPpmRoutes = document.RootElement.GetProperty("Routes")
            .EnumerateArray()
            .Where(route => route.GetProperty("UpstreamPathTemplate").GetString() is { } upstream &&
                            upstream.StartsWith("/api/v1/ppm", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.All(rawPpmRoutes, route => Assert.Equal("http", route.GetProperty("UpstreamScheme").GetString()));
    }

    private static void AssertPpmContract(FileConfiguration config)
    {
        var ppmRoutes = config.Routes
            .Where(route => route.UpstreamPathTemplate.StartsWith("/api/v1/ppm", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Equal(2, ppmRoutes.Count);
        AssertPpmRoute(ppmRoutes, "/api/v1/ppm");
        AssertPpmRoute(ppmRoutes, "/api/v1/ppm/{everything}");
    }

    [Fact]
    public void PpmContractMutation_WrongPortIsRejected()
    {
        var config = LoadConfiguration();
        var route = config.Routes.Single(candidate => candidate.UpstreamPathTemplate == "/api/v1/ppm");
        route.DownstreamHostAndPorts[0].Port = 5999;

        Assert.NotNull(Record.Exception(() => AssertPpmContract(config)));
    }

    [Fact]
    public void PpmContractMutation_MissingCatchAllIsRejected()
    {
        var config = LoadConfiguration();
        config.Routes.RemoveAll(candidate => candidate.UpstreamPathTemplate == "/api/v1/ppm/{everything}");

        Assert.NotNull(Record.Exception(() => AssertPpmContract(config)));
    }

    [Fact]
    public void PpmContractMutation_MissingPostAndDeleteAreRejected()
    {
        var config = LoadConfiguration();
        foreach (var route in config.Routes.Where(candidate => candidate.UpstreamPathTemplate.StartsWith("/api/v1/ppm", StringComparison.Ordinal)))
        {
            route.UpstreamHttpMethod.Remove("POST");
            route.UpstreamHttpMethod.Remove("DELETE");
        }

        Assert.NotNull(Record.Exception(() => AssertPpmContract(config)));
    }

    [Fact]
    public void RouteCollisionMutation_DuplicateUpstreamAndMethodIsRejected()
    {
        var config = LoadConfiguration();
        var original = config.Routes.Single(candidate => candidate.UpstreamPathTemplate == "/api/v1/ppm");
        config.Routes.Add(new FileRoute
        {
            UpstreamPathTemplate = original.UpstreamPathTemplate,
            DownstreamPathTemplate = "/api/v1/other",
            UpstreamHttpMethod = new HashSet<string> { "get" }
        });

        Assert.NotNull(Record.Exception(() => AssertNoRouteMethodCollisions(config)));
    }

    [Fact]
    public void ExistingNonPpmRoutingSurface_RemainsPresent()
    {
        var config = LoadConfiguration();
        var nonPpmRoutes = config.Routes
            .Where(route => !route.UpstreamPathTemplate.StartsWith("/api/v1/ppm", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Before MOD-0117, the shipped configuration already contained roughly 84 routes. This lower bound
        // prevents a PPM-only configuration (or accidental wholesale deletion) from satisfying the focused tests.
        Assert.True(nonPpmRoutes.Count >= 80, $"Expected the existing routing surface, found only {nonPpmRoutes.Count} non-PPM routes.");
    }

    private static void AssertPpmRoute(IReadOnlyCollection<FileRoute> ppmRoutes, string template)
    {
        var matchingRoutes = ppmRoutes
            .Where(route => string.Equals(route.UpstreamPathTemplate, template, StringComparison.Ordinal))
            .ToList();

        Assert.Single(matchingRoutes);
        var route = matchingRoutes[0];

        Assert.Equal(template, route.DownstreamPathTemplate);
        Assert.Equal("http", route.DownstreamScheme);

        var destination = Assert.Single(route.DownstreamHostAndPorts);
        Assert.Equal("localhost", destination.Host);
        Assert.Equal(5061, destination.Port);

        Assert.Equal(PpmMethods.Count, route.UpstreamHttpMethod.Count);
        Assert.True(
            PpmMethods.SetEquals(route.UpstreamHttpMethod),
            $"Route '{template}' must expose exactly: {string.Join(", ", PpmMethods.OrderBy(method => method))}; " +
            $"actual: {string.Join(", ", route.UpstreamHttpMethod.OrderBy(method => method))}.");
    }

    private sealed class RouteMethodClaimComparer : IEqualityComparer<(string Template, string Method)>
    {
        public bool Equals((string Template, string Method) x, (string Template, string Method) y) =>
            string.Equals(x.Template, y.Template, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Method, y.Method, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Template, string Method) value) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Template),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Method));
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
