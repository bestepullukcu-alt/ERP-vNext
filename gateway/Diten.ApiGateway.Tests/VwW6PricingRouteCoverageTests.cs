using System.Text.Json;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-VW-W6 · W6-BE-b (MOD-0156 §15, AC-GW-1) — every pricing endpoint (price lists, imports, rules + trial, the dictionary,
/// FX rates, the history) reaches the CrmService (5061) through a pass-through route that accepts its method (the W3L / W5
/// lesson: a new endpoint without its method on the Gateway is invisible to the Web and mobile).
/// </summary>
public sealed class VwW6PricingRouteCoverageTests
{
    private const string Id = "00000000-0000-0000-0000-000000000001";

    [Theory]
    [InlineData("/api/commercial/price-lists", "GET")]
    [InlineData("/api/commercial/price-lists", "POST")]
    [InlineData("/api/commercial/price-lists/" + Id, "GET")]
    [InlineData("/api/commercial/price-lists/" + Id, "PUT")]
    [InlineData("/api/commercial/price-lists/" + Id + "/items", "GET")]
    [InlineData("/api/commercial/price-lists/" + Id + "/versions", "POST")]
    [InlineData("/api/commercial/price-lists/" + Id + "/discard", "POST")]
    [InlineData("/api/commercial/price-lists/" + Id + "/submit", "POST")]
    [InlineData("/api/commercial/price-lists/" + Id + "/decision", "POST")]
    [InlineData("/api/commercial/price-lists/export", "GET")]
    [InlineData("/api/commercial/price-list-imports", "POST")]
    [InlineData("/api/commercial/price-list-imports/" + Id, "GET")]
    [InlineData("/api/commercial/price-list-imports/" + Id + "/convert", "POST")]
    [InlineData("/api/commercial/price-list-imports/" + Id + "/discard", "POST")]
    [InlineData("/api/commercial/pricing-rules", "GET")]
    [InlineData("/api/commercial/pricing-rules", "POST")]
    [InlineData("/api/commercial/pricing-rules/" + Id, "GET")]
    [InlineData("/api/commercial/pricing-rules/" + Id, "PUT")]
    [InlineData("/api/commercial/pricing-rules/" + Id + "/revisions", "POST")]
    [InlineData("/api/commercial/pricing-rules/" + Id + "/discard", "POST")]
    [InlineData("/api/commercial/pricing-rules/" + Id + "/submit", "POST")]
    [InlineData("/api/commercial/pricing-rules/" + Id + "/decision", "POST")]
    [InlineData("/api/commercial/pricing-rules/trial", "POST")]
    [InlineData("/api/commercial/pricing-contract", "GET")]
    [InlineData("/api/commercial/fx-rates", "GET")]
    [InlineData("/api/commercial/fx-rates", "POST")]
    [InlineData("/api/commercial/fx-rates/" + Id, "PUT")]
    [InlineData("/api/commercial/fx-rates/import", "POST")]
    [InlineData("/api/commercial/pricing-history", "GET")]
    public void Every_pricing_endpoint_has_a_crm_route_for_its_method(string path, string method)
    {
        var routes = Load().Routes.Where(r => Matches(r.UpstreamPathTemplate, path)).ToList();
        var route = routes.FirstOrDefault(r => r.UpstreamHttpMethod.Contains(method, StringComparer.OrdinalIgnoreCase));
        Assert.True(route is not null, $"No gateway route for {method} {path} (matching templates: {string.Join(", ", routes.Select(r => r.UpstreamPathTemplate))}).");
        Assert.All(route!.DownstreamHostAndPorts, x => Assert.Equal(5061, x.Port));
        Assert.Equal(route.UpstreamPathTemplate, route.DownstreamPathTemplate); // a pass-through, never re-pathed
        Assert.Contains("OPTIONS", route.UpstreamHttpMethod, StringComparer.OrdinalIgnoreCase); // the browser's preflight
    }

    [Fact]
    public void Each_pricing_root_is_declared_once_as_itself_and_its_catch_all()
    {
        var templates = Load().Routes.Select(r => r.UpstreamPathTemplate).Where(t => t.StartsWith("/api/commercial/", StringComparison.Ordinal))
            .OrderBy(t => t, StringComparer.Ordinal).ToList();
        string[] roots = ["price-lists", "price-list-imports", "pricing-rules", "pricing-contract", "fx-rates", "pricing-history"];
        Assert.Equal(roots.SelectMany(r => new[] { "/api/commercial/" + r, "/api/commercial/" + r + "/{everything}" }).OrderBy(t => t, StringComparer.Ordinal),
            templates);
    }

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
