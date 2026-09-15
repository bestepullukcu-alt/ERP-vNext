using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// BL-414 — the detail page's single-item read, driven over REAL HTTP through the web tier.
///
/// <para>Same technique as <c>TaskDependencyProxyRouteTests</c>: these requests carry no session, so a URL that
/// resolves to an endpoint answers with the cookie-auth challenge (302 to login), while a URL the proxy does not
/// serve answers 404 from routing before authentication runs. A green Platform suite proves nothing about whether
/// the browser can reach the read; this does.</para>
/// </summary>
public sealed class WorkCenterNextWorkItemProxyRouteTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid ItemId = Guid.Parse("41400000-0000-0000-0000-0000000000d1");

    private readonly WebApplicationFactory<Program> _factory;

    public WorkCenterNextWorkItemProxyRouteTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task One_work_item_by_id_reaches_the_proxy_controller()
    {
        using var client = CreateClient();

        var response = await client.GetAsync($"/WorkCenterNext/api/work-items/{ItemId}");

        AssertReachedController(response, "GET /WorkCenterNext/api/work-items/{id}");
    }

    [Fact]
    public async Task The_list_route_is_still_served_beside_it()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/WorkCenterNext/api/work-items");

        AssertReachedController(response, "GET /WorkCenterNext/api/work-items");
    }

    /// <summary>The id is GUID-constrained, so nothing but an id is ever spliced into the upstream path.</summary>
    [Fact]
    public async Task A_non_guid_id_is_refused_by_routing()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/WorkCenterNext/api/work-items/mine");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>Non-vacuity: a path the proxy does not serve must answer 404, or 302 would mean nothing.</summary>
    [Fact]
    public async Task A_route_the_proxy_does_not_serve_answers_not_found()
    {
        using var client = CreateClient();

        var response = await client.GetAsync($"/WorkCenterNext/api/work-itemz/{ItemId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClient() => _factory
        .WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
        .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static void AssertReachedController(HttpResponseMessage response, string route)
    {
        Assert.False(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"{route} is not served by the Diten.Web proxy ({(int)response.StatusCode}). The detail page would ask "
            + "and the request would die in the web tier before reaching Platform.");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains(
            "login",
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }
}
