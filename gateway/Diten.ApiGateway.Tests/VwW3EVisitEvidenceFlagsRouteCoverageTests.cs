using System.Text.Json;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-VW-W3E · W3E-BE-b (§2.7) — the manager's visit evidence flags (list, summary, detail) reach the CrmService (5061)
/// through a pass-through route; the read only surface accepts GET (no write method is routed).
/// </summary>
public sealed class VwW3EVisitEvidenceFlagsRouteCoverageTests
{
    private const string Id = "00000000-0000-0000-0000-000000000001";

    [Theory]
    [InlineData("/api/crm/visit-evidence-flags")]
    [InlineData("/api/crm/visit-evidence-flags/summary")]
    [InlineData("/api/crm/visit-evidence-flags/" + Id)]
    public void Every_visit_evidence_flags_endpoint_has_a_crm_get_route(string path)
    {
        var routes = Load().Routes.Where(r => Matches(r.UpstreamPathTemplate, path)).ToList();
        var route = routes.FirstOrDefault(r => r.UpstreamHttpMethod.Contains("GET", StringComparer.OrdinalIgnoreCase));
        Assert.True(route is not null, $"No gateway route for GET {path}.");
        Assert.All(route!.DownstreamHostAndPorts, x => Assert.Equal(5061, x.Port));
        Assert.Equal(route.UpstreamPathTemplate, route.DownstreamPathTemplate);
        Assert.DoesNotContain(route.UpstreamHttpMethod, m => m is "POST" or "PUT" or "DELETE" or "PATCH"); // read only (K-W3E-11)
    }

    [Fact]
    public void The_base_and_the_catch_all_are_declared_once_each()
    {
        var templates = Load().Routes.Select(r => r.UpstreamPathTemplate).ToList();
        Assert.Single(templates, t => t == "/api/crm/visit-evidence-flags");
        Assert.Single(templates, t => t == "/api/crm/visit-evidence-flags/{everything}");
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
