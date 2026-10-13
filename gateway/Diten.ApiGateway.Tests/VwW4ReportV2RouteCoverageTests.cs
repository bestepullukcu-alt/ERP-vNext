using System.Text.Json;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-VW-W4 · W4-BE-a (§2.5) — every report v2 endpoint (pre-fill, draft, submit, amend, read, competitor usage) reaches
/// the CrmService (5061) through the existing <c>/api/crm/visit-report</c> pass-through routes with its method.
/// </summary>
public sealed class VwW4ReportV2RouteCoverageTests
{
    private const string Id = "00000000-0000-0000-0000-000000000001";

    [Theory]
    [InlineData("/api/crm/visit-report/v2/prefill", "GET")]
    [InlineData("/api/crm/visit-report/v2/draft", "PUT")]
    [InlineData("/api/crm/visit-report/v2/submit", "POST")]
    [InlineData("/api/crm/visit-report/" + Id + "/amend", "POST")]
    [InlineData("/api/crm/visit-report/" + Id, "GET")]
    [InlineData("/api/crm/visit-report/v2/competitor-usage", "GET")]
    public void Every_report_v2_endpoint_has_a_crm_route_for_its_method(string path, string method)
    {
        var routes = Load().Routes.Where(r => Matches(r.UpstreamPathTemplate, path)).ToList();
        var route = routes.FirstOrDefault(r => r.UpstreamHttpMethod.Contains(method, StringComparer.OrdinalIgnoreCase));
        Assert.True(route is not null, $"No gateway route for {method} {path} (matching templates: {string.Join(", ", routes.Select(r => r.UpstreamPathTemplate))}).");
        Assert.All(route!.DownstreamHostAndPorts, x => Assert.Equal(5061, x.Port));
        Assert.Equal(route.UpstreamPathTemplate, route.DownstreamPathTemplate);
    }

    private static bool Matches(string template, string path)
    {
        if (string.Equals(template, path, StringComparison.Ordinal)) return true;
        const string catchAll = "/{everything}";
        return template.EndsWith(catchAll, StringComparison.Ordinal)
               && path.StartsWith(template[..^catchAll.Length] + "/", StringComparison.Ordinal)
               && path.Length > template.Length - catchAll.Length + 1;
    }

    private static FileConfiguration Load() => JsonSerializer.Deserialize<FileConfiguration>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ocelot.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
}
