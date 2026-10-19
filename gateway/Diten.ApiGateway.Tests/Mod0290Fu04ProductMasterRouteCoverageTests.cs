using System.Text.Json;
using System.Text.RegularExpressions;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// MOD-0290-FU04 S4 FIX1 (the stock team's R-02, a go-live prerequisite): the product-master-bundle v1.1 read contract
/// is served at <c>/api/product-master/*</c> by MDM (5059) — the stock movement form's product picker calls it through
/// the gateway with the user's token. One catch-all, the MDM routes' own shape (GET for the reads, POST for bulk-read
/// and validate, OPTIONS; no gateway authentication options — MDM validates the token, as for every MDM route). No route
/// opens MDM's internal surface (<c>/api/internal/**</c>), which stays server-to-server.
/// </summary>
public sealed class Mod0290Fu04ProductMasterRouteCoverageTests
{
    private const string Template = "/api/product-master/{everything}";

    [Theory]
    [InlineData("/api/product-master/products/00000000-0000-0000-0000-000000000001", "GET")]
    [InlineData("/api/product-master/items/00000000-0000-0000-0000-000000000001", "GET")]
    [InlineData("/api/product-master/skus/00000000-0000-0000-0000-000000000001", "GET")]
    [InlineData("/api/product-master/skus/00000000-0000-0000-0000-000000000001/product", "GET")]
    [InlineData("/api/product-master/skus/00000000-0000-0000-0000-000000000001/uom", "GET")]
    [InlineData("/api/product-master/stockable-items", "GET")]
    [InlineData("/api/product-master/skus/bulk-read", "POST")]
    [InlineData("/api/product-master/validate", "POST")]
    public void The_product_master_route_reaches_mdm_for_every_contract_operation(string path, string method)
    {
        var route = Assert.Single(Load().Routes, r => r.UpstreamPathTemplate == Template);

        Assert.True(Matches(route.UpstreamPathTemplate, path), path);
        Assert.Contains(method, route.UpstreamHttpMethod);
        Assert.True(new HashSet<string>(["GET", "POST", "OPTIONS"], StringComparer.Ordinal).SetEquals(route.UpstreamHttpMethod),
            $"Unexpected methods: {string.Join(",", route.UpstreamHttpMethod)}");
        Assert.Equal(Template, route.DownstreamPathTemplate);
        Assert.Equal("http", route.DownstreamScheme);
        var host = Assert.Single(route.DownstreamHostAndPorts);
        Assert.Equal("localhost", host.Host);
        Assert.Equal(5059, host.Port);
    }

    // The same gateway settings as the MDM routes beside it (measured on the LSKU catch-all): nothing loosened, nothing
    // added — no authentication provider, no claims transform, no header transform.
    [Fact]
    public void The_product_master_route_carries_the_mdm_routes_settings()
    {
        var routes = Load().Routes;
        var route = Assert.Single(routes, r => r.UpstreamPathTemplate == Template);
        var sibling = Assert.Single(routes, r => r.UpstreamPathTemplate == "/api/lskus/{everything}");

        Assert.Equal(sibling.DownstreamScheme, route.DownstreamScheme);
        Assert.Equal(sibling.AuthenticationOptions?.AuthenticationProviderKeys ?? [],
            route.AuthenticationOptions?.AuthenticationProviderKeys ?? []);
        Assert.Equal(sibling.AuthenticationOptions?.AllowedScopes ?? [], route.AuthenticationOptions?.AllowedScopes ?? []);
        Assert.Equal(sibling.RouteClaimsRequirement.Count, route.RouteClaimsRequirement.Count);
        Assert.Equal(sibling.AddHeadersToRequest.Count, route.AddHeadersToRequest.Count);
        Assert.Equal(sibling.AddClaimsToRequest.Count, route.AddClaimsToRequest.Count);
        Assert.Equal(sibling.UpstreamHeaderTransform.Count, route.UpstreamHeaderTransform.Count);
        Assert.Equal(sibling.DownstreamHeaderTransform.Count, route.DownstreamHeaderTransform.Count);
    }

    // The internal contract surface stays off the gateway: no upstream template matches it, no route forwards into it.
    [Theory]
    [InlineData("/api/internal/v1/product-master/validate")]
    [InlineData("/api/internal/v1/product-master/skus/bulk-read")]
    [InlineData("/api/internal/anything")]
    public void No_route_opens_the_internal_surface(string path)
    {
        foreach (var route in Load().Routes)
        {
            Assert.False(Matches(route.UpstreamPathTemplate, path),
                $"{route.UpstreamPathTemplate} opens {path} on the gateway");
            Assert.DoesNotContain("/api/internal", route.DownstreamPathTemplate, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static FileConfiguration Load() =>
        JsonSerializer.Deserialize<FileConfiguration>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ocelot.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    // Ocelot's template rule as far as these routes use it: a {placeholder} as the last segment takes the rest of the path
    // (slashes included); one in the middle takes one segment. Case-insensitive, as Ocelot matches by default.
    private static bool Matches(string template, string path)
    {
        var pattern = Regex.Replace(Regex.Escape(template), @"\\\{[^/]+?\}", "[^/]+");
        if (Regex.IsMatch(template, @"\{[^/]+\}$"))
            pattern = Regex.Replace(Regex.Escape(template), @"\\\{[^/]+?\}$", ".+");
        pattern = Regex.Replace(pattern, @"\\\{[^/]+?\}", "[^/]+");
        return Regex.IsMatch(path, "^" + pattern + "/?$", RegexOptions.IgnoreCase);
    }
}
