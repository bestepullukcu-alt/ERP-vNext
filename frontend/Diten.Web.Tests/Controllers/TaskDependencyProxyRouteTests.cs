using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// BL-028's endpoints, driven over REAL HTTP through the web tier — not against Platform's API.
///
/// <para><b>Why over HTTP.</b> `inquire` existed on Platform, was projected by the provider, and answered 404 in
/// Diten.Web because the proxy had no route for it. Platform's own suite was green the whole time. A test that
/// only calls Platform therefore proves nothing about whether a user can reach the feature; this one starts the
/// actual Diten.Web pipeline and sends the request the browser sends.</para>
///
/// <para><b>What "the route exists" looks like here.</b> These requests carry no session, so the pipeline answers
/// with the cookie-auth challenge — 302 to the login page. That is the signal: an endpoint the proxy does NOT
/// serve is answered 404 by routing BEFORE authentication runs, which the non-vacuity test below pins down. So
/// 302-to-login means the URL resolved to an endpoint, and 404 means the button would be dead. Asserting a
/// successful proxy hop would require a live Platform behind it, which is a different test.</para>
/// </summary>
public sealed class TaskDependencyProxyRouteTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid TaskId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DependencyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly WebApplicationFactory<Program> _factory;

    public TaskDependencyProxyRouteTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Adding_a_dependency_reaches_the_proxy_controller()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/Tasks/api/{TaskId}/dependencies",
            new { dependsOnTaskItemId = DependencyId, dependencyType = "FinishToStart" });

        AssertReachedController(response, $"POST /Tasks/api/{{id}}/dependencies");
    }

    [Fact]
    public async Task Removing_a_dependency_reaches_the_proxy_controller()
    {
        using var client = CreateClient();

        var response = await client.DeleteAsync($"/Tasks/api/{TaskId}/dependencies/{DependencyId}");

        AssertReachedController(response, $"DELETE /Tasks/api/{{id}}/dependencies/{{dependencyId}}");
    }

    /// <summary>
    /// Non-vacuity: 401 has to MEAN something here. A path the proxy does not serve must answer 404, otherwise
    /// the two assertions above would pass for any URL at all.
    /// </summary>
    [Fact]
    public async Task A_route_the_proxy_does_not_serve_answers_not_found()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync($"/Tasks/api/{TaskId}/dependenciez", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>The dependency id segment is GUID-constrained, so a non-GUID is refused by routing.</summary>
    [Fact]
    public async Task A_non_guid_dependency_id_is_refused_by_routing()
    {
        using var client = CreateClient();

        var response = await client.DeleteAsync($"/Tasks/api/{TaskId}/dependencies/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClient() => _factory
        .WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
        .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static void AssertReachedController(HttpResponseMessage response, string route)
    {
        Assert.False(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"{route} is not served by the Diten.Web proxy ({(int)response.StatusCode}). The Task Center would "
            + "render the control and the request would die in the web tier before reaching Platform — exactly how "
            + "`inquire` shipped unreachable.");

        // The endpoint resolved and the application answered with its auth challenge.
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains(
            "login",
            response.Headers.Location?.OriginalString ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }
}
