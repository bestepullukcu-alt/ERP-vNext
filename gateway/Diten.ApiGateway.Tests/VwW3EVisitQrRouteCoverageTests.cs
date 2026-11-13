using System.Text.Json;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-VW-W3E · W3E-BE-a (§2.7) — every institution QR endpoint reaches the CrmService (5061) through a pass-through route
/// that accepts its method (the W3-BE-b lesson: an endpoint without its gateway route is invisible to the Web and mobile).
/// </summary>
public sealed class VwW3EVisitQrRouteCoverageTests
{
    private const string Id = "00000000-0000-0000-0000-000000000001";

    [Theory]
    [InlineData("/api/crm/visit-qr/accounts", "GET")]
    [InlineData("/api/crm/visit-qr/accounts/" + Id, "GET")]
    [InlineData("/api/crm/visit-qr/accounts/" + Id + "/history", "GET")]
    [InlineData("/api/crm/visit-qr/accounts/" + Id + "/issue", "POST")]
    [InlineData("/api/crm/visit-qr/accounts/" + Id + "/revoke", "POST")]
    [InlineData("/api/crm/visit-qr/accounts/" + Id + "/payload", "GET")]
    [InlineData("/api/crm/visit-qr/bulk-issue", "POST")]
    [InlineData("/api/crm/visit-qr/bulk-payload", "POST")]
    [InlineData("/api/crm/visit-qr", "GET")]
    public void Every_visit_qr_endpoint_has_a_crm_route_for_its_method(string path, string method)
    {
        var routes = Load().Routes.Where(r => Matches(r.UpstreamPathTemplate, path)).ToList();
        var route = routes.FirstOrDefault(r => r.UpstreamHttpMethod.Contains(method, StringComparer.OrdinalIgnoreCase));
        Assert.True(route is not null, $"No gateway route for {method} {path} (matching templates: {string.Join(", ", routes.Select(r => r.UpstreamPathTemplate))}).");
        Assert.All(route!.DownstreamHostAndPorts, x => Assert.Equal(5061, x.Port));
        Assert.Equal(route.UpstreamPathTemplate, route.DownstreamPathTemplate); // a pass-through, never re-pathed
    }

    [Fact]
    public void The_qr_routes_are_declared_once()
    {
        var templates = Load().Routes.Select(r => r.UpstreamPathTemplate).Where(t => t.StartsWith("/api/crm/visit-qr", StringComparison.Ordinal)).ToList();
        Assert.Equal(new[] { "/api/crm/visit-qr", "/api/crm/visit-qr/{everything}" }, templates);
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
