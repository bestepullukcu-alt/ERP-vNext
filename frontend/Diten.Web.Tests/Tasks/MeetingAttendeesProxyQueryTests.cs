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
/// BL-531 — the Meetings attendee lookup proxy forwards ONLY `search` and `ids`, URL-encoded, through the very query
/// builder the task approver picker uses (BL-512). Driven on the real MeetingsController; only the gateway is played.
/// </summary>
public sealed class MeetingAttendeesProxyQueryTests
{
    [Fact]
    public async Task Only_search_and_ids_travel_upstream_encoded_and_nothing_else_the_browser_sent()
    {
        var gateway = new CapturingGateway();
        var controller = new MeetingsController(gateway, Config(), NullLogger<MeetingsController>.Instance);
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = $"{AuthTokenCookies.AccessTokenCookie}={Token()}";
        http.Request.QueryString = new QueryString("?search=%C4%B0lker%20%26%20Co&ids=a,b&purpose=all&take=500");
        controller.ControllerContext = new ControllerContext { HttpContext = http };

        var result = await controller.ApiLookupAttendees("İlker & Co", "a,b");

        Assert.Equal(200, Assert.IsType<ContentResult>(result).StatusCode);
        Assert.Equal("http://gateway.bl531.test/api/v1/meetings/lookups/attendees?search=%C4%B0lker%20%26%20Co&ids=a%2Cb",
            Assert.Single(gateway.Urls));
    }

    [Fact]
    public void The_proxy_action_binds_only_the_two_parameters()
    {
        var action = typeof(MeetingsController).GetMethod(nameof(MeetingsController.ApiLookupAttendees))!;
        Assert.Equal(new[] { "search", "ids" }, action.GetParameters().Select(p => p.Name));
    }

    private static IConfiguration Config() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.bl531.test" }).Build();

    private static string Token() =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: [new Claim("tenant_id", Guid.NewGuid().ToString())],
            expires: DateTime.UtcNow.AddMinutes(30)));

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
}
