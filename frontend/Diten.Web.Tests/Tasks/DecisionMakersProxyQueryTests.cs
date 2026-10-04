using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Diten.Web.Controllers;
using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Tasks;

/// <summary>
/// BL-512 — the web proxy hands the people search upstream with ONLY its two parameters (search, ids), each
/// URL-encoded; whatever else a caller puts on the query string stays in the web tier.
/// </summary>
public sealed class DecisionMakersProxyQueryTests
{
    [Fact]
    public void Only_search_and_ids_travel_upstream_and_both_are_encoded()
    {
        Assert.Equal("?search=%C4%B0lker%20%26%20Co", TasksController.DecisionMakersQuery("İlker & Co", null));
        Assert.Equal("?ids=a%2Cb", TasksController.DecisionMakersQuery(null, "a,b"));
        Assert.Equal("?search=x&ids=y", TasksController.DecisionMakersQuery("x", "y"));
        Assert.Equal(string.Empty, TasksController.DecisionMakersQuery(null, null));
    }

    // BL-512 FIX1 — measured on a real request through the real controller, not by reflection: whatever else the
    // browser put on the query string never travels upstream.
    [Fact]
    public async Task A_real_request_sends_only_search_and_ids_upstream()
    {
        var gateway = new CapturingGateway();
        var controller = new TasksController(gateway,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.bl512.test" }).Build(),
            NullLogger<TasksController>.Instance);
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = $"{AuthTokenCookies.AccessTokenCookie}={new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: [new Claim("tenant_id", Guid.NewGuid().ToString())], expires: DateTime.UtcNow.AddMinutes(30)))}";
        http.Request.QueryString = new QueryString("?search=%C4%B0l&purpose=all&take=500");
        controller.ControllerContext = new ControllerContext { HttpContext = http };

        var result = await controller.ApiDecisionMakers("İl", null);

        Assert.Equal(200, Assert.IsType<ContentResult>(result).StatusCode);
        Assert.Equal("http://gateway.bl512.test/api/v1/tasks/lookups/decision-makers?search=%C4%B0l", Assert.Single(gateway.Urls));
    }

    private sealed class CapturingGateway : HttpMessageHandler, IHttpClientFactory
    {
        public List<string> Urls { get; } = [];

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Urls.Add(request.RequestUri!.OriginalString);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        }
    }

    [Fact]
    public void The_proxy_action_binds_only_the_two_parameters()
    {
        var action = typeof(TasksController).GetMethod(nameof(TasksController.ApiDecisionMakers))!;
        Assert.Equal(new[] { "search", "ids" }, action.GetParameters().Select(p => p.Name));
    }
}
