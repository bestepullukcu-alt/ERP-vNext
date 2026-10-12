using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Diten.Web.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-UI-CALENDAR-VIEW-01 (A) — <c>GET /WorkCenterNext/api/calendar</c> forwards to the gateway's
/// <c>/api/v1/work/calendar</c> and hands the upstream status and body back VERBATIM, success and coded refusal
/// alike. The page turns <c>WORK_CALENDAR_RANGE_INVALID</c> into a sentence in seven languages; a proxy that
/// re-shaped the body would leave it reading "an error occurred".
/// </summary>
public sealed class WorkCenterNextCalendarProxyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Gateway = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private readonly WebApplicationFactory<Program> _factory;

    public WorkCenterNextCalendarProxyTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task A_feed_is_forwarded_to_the_gateway_route_and_returned_verbatim()
    {
        const string feed = """{"data":{"timeZoneId":"Europe/Istanbul","tasks":[],"meetings":[],"days":[],"unplannedCount":3,"planPassedCount":1,"pendingInviteCount":2},"isSuccessful":true,"statusCode":200}""";
        var gateway = new CapturingGateway(HttpStatusCode.OK, feed);
        var controller = ControllerWith(gateway);

        var result = Assert.IsType<ContentResult>(await controller.Calendar("2026-10-05", "2026-10-11"));

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(feed, result.Content);
        Assert.Equal($"{Gateway}/api/v1/work/calendar?from=2026-10-05&to=2026-10-11", gateway.LastUri);
        Assert.Equal(HttpMethod.Get, gateway.LastMethod);
        Assert.Equal(TenantId.ToString("D"), gateway.LastTenantHeader);
        Assert.StartsWith("Bearer ", gateway.LastAuthorization);
    }

    [Fact]
    public async Task A_coded_refusal_keeps_its_status_and_body_untouched()
    {
        const string refusal = """{"isSuccessful":false,"statusCode":400,"errors":["range"],"reason_code":"WORK_CALENDAR_RANGE_INVALID"}""";
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.BadRequest, refusal));

        var result = Assert.IsType<ContentResult>(await controller.Calendar("2026-10-01", "2026-12-31"));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(refusal, result.Content);
    }

    [Fact]
    public async Task A_missing_date_is_not_invented_and_the_value_is_escaped()
    {
        var gateway = new CapturingGateway(HttpStatusCode.BadRequest, "{}");
        var controller = ControllerWith(gateway);

        await controller.Calendar("2026-10-05&x=1", null);

        Assert.Equal($"{Gateway}/api/v1/work/calendar?from=2026-10-05%26x%3D1", gateway.LastUri);
    }

    [Fact]
    public async Task Without_a_session_token_nothing_is_sent_upstream()
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, "{}");
        var controller = ControllerWith(gateway, withToken: false);

        var result = await controller.Calendar("2026-10-05", "2026-10-11");

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Null(gateway.LastUri);
    }

    [Fact]
    public async Task The_route_is_served_by_the_web_tier()
    {
        using var client = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/WorkCenterNext/api/calendar?from=2026-10-05&to=2026-10-11");

        // Anonymous: the [Authorize] challenge answers — which proves the route exists (an unknown one is 404).
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("login", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static WorkCenterNextController ControllerWith(CapturingGateway gateway, bool withToken = true)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway })
            .Build();
        var controller = new WorkCenterNextController(
            new SingleClientFactory(gateway), configuration, new StubEnvironment(),
            NullLogger<WorkCenterNextController>.Instance);

        var httpContext = new DefaultHttpContext();
        if (withToken)
        {
            httpContext.Request.Headers.Cookie = $"access_token={Token()}";
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static string Token()
    {
        var jwt = new JwtSecurityToken(
            claims: [new Claim("sub", Guid.NewGuid().ToString()), new Claim("tenant_id", TenantId.ToString())],
            expires: DateTime.UtcNow.AddMinutes(10));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private sealed class CapturingGateway(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? LastUri { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastTenantHeader { get; private set; }
        public string LastAuthorization { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri?.OriginalString;
            LastMethod = request.Method;
            LastTenantHeader = request.Headers.TryGetValues("X-Tenant-Id", out var values) ? values.First() : null;
            LastAuthorization = request.Headers.Authorization?.ToString() ?? string.Empty;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Diten.Web";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Test";
    }
}
