using System.Text.Json;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-VW-W4 · W4-BE-b2 (§2.7) — every competitor products endpoint (list, lookup, record, usage, merge candidates, direct
/// writes, the rep proposal and the decision workflow) reaches the MdmService (5059) through a pass-through route that
/// accepts its method; there is no DELETE.
/// </summary>
public sealed class VwW4CompetitorProductsRouteCoverageTests
{
    private const string Id = "00000000-0000-0000-0000-000000000001";

    [Theory]
    [InlineData("/api/mdm/competitor-products", "GET")]
    [InlineData("/api/mdm/competitor-products", "POST")]
    [InlineData("/api/mdm/competitor-products/lookup", "GET")]
    [InlineData("/api/mdm/competitor-products/merge-candidates", "GET")]
    [InlineData("/api/mdm/competitor-products/" + Id, "GET")]
    [InlineData("/api/mdm/competitor-products/" + Id, "PUT")]
    [InlineData("/api/mdm/competitor-products/" + Id + "/usage", "GET")]
    [InlineData("/api/mdm/competitor-products/" + Id + "/deactivate", "POST")]
    [InlineData("/api/mdm/competitor-products/" + Id + "/merge", "POST")]
    [InlineData("/api/mdm/competitor-products/proposals", "GET")]
    [InlineData("/api/mdm/competitor-products/proposals", "POST")]
    [InlineData("/api/mdm/competitor-products/proposals/" + Id, "GET")]
    [InlineData("/api/mdm/competitor-products/proposals/" + Id + "/decision", "PUT")]
    [InlineData("/api/mdm/competitor-products/proposals/" + Id + "/decision/submit", "POST")]
    [InlineData("/api/mdm/competitor-products/proposals/" + Id + "/decision/withdraw", "POST")]
    [InlineData("/api/mdm/competitor-products/proposals/" + Id + "/decision/approve-reject", "POST")]
    public void Every_competitor_products_endpoint_has_an_mdm_route_for_its_method(string path, string method)
    {
        var routes = Load().Routes.Where(r => Matches(r.UpstreamPathTemplate, path)).ToList();
        var route = routes.FirstOrDefault(r => r.UpstreamHttpMethod.Contains(method, StringComparer.OrdinalIgnoreCase));
        Assert.True(route is not null, $"No gateway route for {method} {path} (matching templates: {string.Join(", ", routes.Select(r => r.UpstreamPathTemplate))}).");
        Assert.All(route!.DownstreamHostAndPorts, x => Assert.Equal(5059, x.Port));
        Assert.Equal(route.UpstreamPathTemplate, route.DownstreamPathTemplate); // a pass-through, never re-pathed
    }

    [Fact]
    public void The_base_and_the_catch_all_are_declared_once_each()
    {
        var templates = Load().Routes.Select(r => r.UpstreamPathTemplate).ToList();
        Assert.Single(templates, t => t == "/api/mdm/competitor-products");
        Assert.Single(templates, t => t == "/api/mdm/competitor-products/{everything}");
        // no DELETE anywhere on the surface (a record ends inactive / merged / rejected)
        Assert.DoesNotContain(Load().Routes.Where(r => r.UpstreamPathTemplate.StartsWith("/api/mdm/competitor-products", StringComparison.Ordinal)),
            r => r.UpstreamHttpMethod.Contains("DELETE", StringComparer.OrdinalIgnoreCase));
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
