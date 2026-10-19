using System.Text.Json;
using Ocelot.Configuration.File;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-VW-W3 · W3-BE-c (§2.4) — every CRM endpoint the W3 authoring screens call reaches the CrmService (5061) through a
/// route that accepts its method. W3-BE-b shipped the objection library without its gateway routes (live E4 found it);
/// this pins the routes of the library, the evidence policy reads and the time-boxed exceptions.
/// </summary>
public sealed class VwW3AuthoringRouteCoverageTests
{
    private const string Id = "00000000-0000-0000-0000-000000000001";

    [Theory]
    // the objection library (W3-BE-b + W3-BE-c)
    [InlineData("/api/crm/objection-responses", "GET")]
    [InlineData("/api/crm/objection-responses", "POST")]
    [InlineData("/api/crm/objection-responses/" + Id, "GET")]
    [InlineData("/api/crm/objection-responses/" + Id, "PUT")]
    [InlineData("/api/crm/objection-responses/" + Id + "/usages", "GET")]
    [InlineData("/api/crm/objection-responses/" + Id + "/submit", "POST")]
    [InlineData("/api/crm/objection-responses/" + Id + "/withdraw", "POST")]
    [InlineData("/api/crm/objection-responses/" + Id + "/decision", "POST")]
    [InlineData("/api/crm/objection-responses/" + Id + "/copy", "POST")]
    [InlineData("/api/crm/objection-responses/" + Id + "/retire", "POST")]
    // the claim picker's product filter
    [InlineData("/api/crm/content-composition/claims", "GET")]
    // the evidence policy and its reads
    [InlineData("/api/crm/visit-evidence-policy", "GET")]
    [InlineData("/api/crm/visit-evidence-policy", "PUT")]
    [InlineData("/api/crm/visit-evidence-policy/history", "GET")]
    [InlineData("/api/crm/visit-evidence-policy/effective", "GET")]
    [InlineData("/api/crm/visit-evidence-policy/reps", "GET")]
    // the time-boxed exceptions
    [InlineData("/api/crm/visit-evidence-exceptions", "GET")]
    [InlineData("/api/crm/visit-evidence-exceptions", "POST")]
    [InlineData("/api/crm/visit-evidence-exceptions/" + Id, "GET")]
    [InlineData("/api/crm/visit-evidence-exceptions/" + Id, "PUT")]
    [InlineData("/api/crm/visit-evidence-exceptions/" + Id + "/submit", "POST")]
    [InlineData("/api/crm/visit-evidence-exceptions/" + Id + "/withdraw", "POST")]
    [InlineData("/api/crm/visit-evidence-exceptions/" + Id + "/decision", "POST")]
    [InlineData("/api/crm/visit-evidence-exceptions/" + Id + "/cancel", "POST")]
    [InlineData("/api/crm/visit-evidence-exceptions/" + Id + "/end-early", "POST")]
    public void Every_w3_authoring_endpoint_has_a_crm_route_for_its_method(string path, string method)
    {
        var routes = Load().Routes.Where(r => Matches(r.UpstreamPathTemplate, path)).ToList();
        var route = routes.FirstOrDefault(r => r.UpstreamHttpMethod.Contains(method, StringComparer.OrdinalIgnoreCase));
        Assert.True(route is not null, $"No gateway route for {method} {path} (matching templates: {string.Join(", ", routes.Select(r => r.UpstreamPathTemplate))}).");
        Assert.All(route!.DownstreamHostAndPorts, x => Assert.Equal(5061, x.Port));
        Assert.Equal(route.UpstreamPathTemplate, route.DownstreamPathTemplate); // a pass-through, never re-pathed
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
