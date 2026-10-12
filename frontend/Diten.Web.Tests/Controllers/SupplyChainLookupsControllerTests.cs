using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Diten.Web.Controllers;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// R-2 (PR #134). The legal-entity selector's feed. One route serves all five SupplyChain pages, and it must NOT
/// require a legalEntityId — it is the call a page makes to find out which legal entities exist.
/// </summary>
public sealed class SupplyChainLookupsControllerTests
{
    private const string Payload =
        """{"data":[{"legalEntityId":"11111111-1111-4111-8111-111111111111","code":"LE1","legalName":"First"}]}""";

    private static (SupplyChainLookupsController Controller, CaptureHandler Handler) Create(
        Func<HttpRequestMessage, HttpResponseMessage>? responder = null, bool withToken = true, string? correlation = null)
    {
        var handler = new CaptureHandler(responder ?? (_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(Payload, Encoding.UTF8, "application/json"),
        }));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = "http://localhost:5000" }).Build();
        var controller = new SupplyChainLookupsController(
            new HttpClient(handler), configuration, NullLogger<SupplyChainLookupsController>.Instance);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "lookup-tester")], "test")),
        };
        if (withToken) context.Request.Headers.Cookie = "access_token=test-token";
        if (correlation is not null) context.Request.Headers["X-Correlation-Id"] = correlation;
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return (controller, handler);
    }

    [Fact]
    public async Task Forwards_to_the_gateway_lookup_with_the_session_token_and_no_scope_headers()
    {
        var (controller, handler) = Create();

        var result = await controller.LegalEntities(CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal(Payload, content.Content);
        Assert.Equal("http://localhost:5000/api/legal-entities/lookup", handler.Uri);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("test-token", handler.Authorization);
        // The feed carries no scope: MDM derives the tenant from the JWT, and a legal entity is what is being asked
        // for. Sending X-Legal-Entity-Id here would make the selector depend on its own answer.
        Assert.False(handler.Headers.ContainsKey("X-Tenant-Id"));
        Assert.False(handler.Headers.ContainsKey("X-Legal-Entity-Id"));
    }

    [Fact]
    public async Task Without_a_session_token_it_is_401_and_the_gateway_is_never_called()
    {
        var (controller, handler) = Create(withToken: false);

        var result = await controller.LegalEntities(CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task The_browser_trace_is_forwarded_when_sent_and_never_minted_when_absent()
    {
        var trace = Guid.Parse("aaaaaaaa-9999-4999-8999-aaaaaaaaaaaa");
        var (withTrace, withTraceHandler) = Create(correlation: trace.ToString("D"));
        await withTrace.LegalEntities(CancellationToken.None);
        Assert.Equal(trace.ToString("D"), withTraceHandler.Headers["X-Correlation-Id"]);

        var (without, withoutHandler) = Create();
        await without.LegalEntities(CancellationToken.None);
        Assert.False(withoutHandler.Headers.ContainsKey("X-Correlation-Id"));
    }

    // An upstream failure is reported as an outage. An empty list would read as "this tenant has no legal
    // entities", which is a different claim and would leave the user staring at an empty required field.
    [Fact]
    public async Task Transport_failure_is_503_not_an_empty_list()
    {
        var (controller, _) = Create(_ => throw new HttpRequestException("gateway down"));

        var result = await controller.LegalEntities(CancellationToken.None);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<StatusCodeResult>(result).StatusCode);
    }

    // A non-200 from MDM is passed through as-is, so a 403 (the caller lacking mdm.legal-entities.read) reaches
    // the page as a 403 rather than being dressed as an outage.
    [Theory]
    [InlineData(HttpStatusCode.Forbidden, 403)]
    [InlineData(HttpStatusCode.Unauthorized, 401)]
    [InlineData(HttpStatusCode.InternalServerError, 500)]
    public async Task Upstream_status_is_passed_through(HttpStatusCode upstream, int expected)
    {
        var (controller, _) = Create(_ => new HttpResponseMessage(upstream)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        });

        var result = await controller.LegalEntities(CancellationToken.None);

        Assert.Equal(expected, Assert.IsType<ContentResult>(result).StatusCode);
    }

    private sealed class CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string Uri { get; private set; } = string.Empty;
        public HttpMethod? Method { get; private set; }
        public string? Authorization { get; private set; }
        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CallCount++;
            Uri = request.RequestUri?.ToString() ?? string.Empty;
            Method = request.Method;
            Authorization = request.Headers.Authorization?.Parameter;
            foreach (var header in request.Headers)
            {
                Headers[header.Key] = string.Join(',', header.Value);
            }

            return Task.FromResult(responder(request));
        }
    }
}
