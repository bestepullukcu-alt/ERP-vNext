using System.Text.Json;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-VW-W6 · W6-BE-a (MOD-0168 §15, AC-GW-1) — every pre-order endpoint (orders, context, preview, the lifecycle actions,
/// export, settings, the approval link) reaches the CrmService (5061) through a pass-through route that accepts its method
/// (the W5-BE lesson: an endpoint without its Gateway method is invisible to the Web and mobile).
/// </summary>
public sealed class VwW6PreOrderRouteCoverageTests
{
    private const string Id = "00000000-0000-0000-0000-000000000001";

    [Theory]
    [InlineData("/api/commercial/pre-orders", "GET")]
    [InlineData("/api/commercial/pre-orders", "POST")]
    [InlineData("/api/commercial/pre-orders/context", "GET")]
    [InlineData("/api/commercial/pre-orders/preview", "POST")]
    [InlineData("/api/commercial/pre-orders/export", "GET")]
    [InlineData("/api/commercial/pre-orders/" + Id, "GET")]
    [InlineData("/api/commercial/pre-orders/" + Id, "PUT")]
    [InlineData("/api/commercial/pre-orders/" + Id + "/discard", "POST")]
    [InlineData("/api/commercial/pre-orders/" + Id + "/signature", "POST")]
    [InlineData("/api/commercial/pre-orders/" + Id + "/submit", "POST")]
    [InlineData("/api/commercial/pre-orders/" + Id + "/decision", "POST")]
    [InlineData("/api/commercial/pre-orders/" + Id + "/cancel", "POST")]
    [InlineData("/api/commercial/pre-orders/" + Id + "/copy", "POST")]
    [InlineData("/api/commercial/pre-orders/" + Id + "/retry-transmission", "POST")]
    [InlineData("/api/commercial/pre-orders/" + Id + "/depot-response", "POST")]
    [InlineData("/api/commercial/pre-orders/" + Id + "/document", "GET")]
    [InlineData("/api/commercial/pre-orders/" + Id + "/pharmacy-history", "GET")]
    [InlineData("/api/commercial/pre-order-settings", "GET")]
    [InlineData("/api/commercial/pre-order-settings", "PUT")]
    [InlineData("/api/commercial/pre-order-approval-links/abcDEF123_-", "GET")]
    public void Every_pre_order_endpoint_has_a_crm_route_for_its_method(string path, string method)
    {
        var routes = Load().Routes.Where(r => Matches(r.UpstreamPathTemplate, path)).ToList();
        var route = routes.FirstOrDefault(r => r.UpstreamHttpMethod.Contains(method, StringComparer.OrdinalIgnoreCase));
        Assert.True(route is not null, $"No gateway route for {method} {path} (matching templates: {string.Join(", ", routes.Select(r => r.UpstreamPathTemplate))}).");
        Assert.All(route!.DownstreamHostAndPorts, x => Assert.Equal(5061, x.Port));
        Assert.Equal(route.UpstreamPathTemplate, route.DownstreamPathTemplate); // a pass-through, never re-pathed
        Assert.Contains("OPTIONS", route.UpstreamHttpMethod, StringComparer.OrdinalIgnoreCase); // the browser's preflight
    }

    [Fact]
    public void The_pre_order_roots_are_declared_once_each()
    {
        var templates = Load().Routes.Select(r => r.UpstreamPathTemplate).Where(t => t.StartsWith("/api/commercial/pre-order", StringComparison.Ordinal))
            .OrderBy(t => t, StringComparer.Ordinal).ToList();
        Assert.Equal(new[]
        {
            "/api/commercial/pre-order-approval-links/{everything}", "/api/commercial/pre-order-settings", "/api/commercial/pre-orders",
            "/api/commercial/pre-orders/{everything}"
        }.OrderBy(t => t, StringComparer.Ordinal), templates);
        // the approval link is a read: no write method reaches it through the Gateway
        var link = Load().Routes.Single(r => r.UpstreamPathTemplate == "/api/commercial/pre-order-approval-links/{everything}");
        Assert.Equal(new[] { "GET", "OPTIONS" }, link.UpstreamHttpMethod.OrderBy(m => m, StringComparer.Ordinal));
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
