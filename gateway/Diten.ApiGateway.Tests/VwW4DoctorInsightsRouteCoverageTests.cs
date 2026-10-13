using System.Text.Json;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-VW-W4 · W4-BE-c (§2.7) — every doctor insights endpoint (profile, potentials, indication rows, thresholds,
/// opportunities, aggregates) reaches the CrmService (5061) through a pass-through route that accepts its method.
/// </summary>
public sealed class VwW4DoctorInsightsRouteCoverageTests
{
    private const string Id = "00000000-0000-0000-0000-000000000001";

    [Theory]
    [InlineData("/api/crm/doctor-insights/doctors/" + Id + "/profile", "GET")]
    [InlineData("/api/crm/doctor-insights/doctors/" + Id + "/profile", "PUT")]
    [InlineData("/api/crm/doctor-insights/doctors/" + Id + "/potentials", "GET")]
    [InlineData("/api/crm/doctor-insights/products/" + Id + "/indication-profiles", "GET")]
    [InlineData("/api/crm/doctor-insights/thresholds", "GET")]
    [InlineData("/api/crm/doctor-insights/thresholds/history", "GET")]
    [InlineData("/api/crm/doctor-insights/thresholds/" + Id, "PUT")]
    [InlineData("/api/crm/doctor-insights/thresholds/default", "PUT")]
    [InlineData("/api/crm/doctor-insights/opportunities", "GET")]
    [InlineData("/api/crm/doctor-insights/aggregates", "GET")]
    public void Every_doctor_insights_endpoint_has_a_crm_route_for_its_method(string path, string method)
    {
        var routes = Load().Routes.Where(r => Matches(r.UpstreamPathTemplate, path)).ToList();
        var route = routes.FirstOrDefault(r => r.UpstreamHttpMethod.Contains(method, StringComparer.OrdinalIgnoreCase));
        Assert.True(route is not null, $"No gateway route for {method} {path} (matching templates: {string.Join(", ", routes.Select(r => r.UpstreamPathTemplate))}).");
        Assert.All(route!.DownstreamHostAndPorts, x => Assert.Equal(5061, x.Port));
        Assert.Equal(route.UpstreamPathTemplate, route.DownstreamPathTemplate); // a pass-through, never re-pathed
    }

    [Fact]
    public void The_base_and_the_catch_all_are_declared_once_each()
    {
        var templates = Load().Routes.Select(r => r.UpstreamPathTemplate).ToList();
        Assert.Single(templates, t => t == "/api/crm/doctor-insights");
        Assert.Single(templates, t => t == "/api/crm/doctor-insights/{everything}");
    }

    /// <summary>Exact template, or a <c>/{everything}</c> template whose prefix the path continues.</summary>
    private static bool Matches(string template, string path)
    {
        if (string.Equals(template, path, StringComparison.Ordinal))
        {
            return true;
        }

        const string catchAll = "/{everything}";
        return template.EndsWith(catchAll, StringComparison.Ordinal)
               && path.StartsWith(template[..^catchAll.Length] + "/", StringComparison.Ordinal)
               && path.Length > template.Length - catchAll.Length + 1;
    }

    private static FileConfiguration Load() => JsonSerializer.Deserialize<FileConfiguration>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ocelot.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
}
