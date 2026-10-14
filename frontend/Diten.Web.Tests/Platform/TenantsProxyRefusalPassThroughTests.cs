using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Diten.Web.Controllers;
using Diten.Web.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.Web.Tests.Platform;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 FIX2 (A5) — the Platform answers a change it could not record with 503 and a problem body
/// carrying <c>reason_code</c> (AUDIT_RECORD_UNAVAILABLE), and a subscription conflict with 409 and its own code. The
/// tenant screen says those from the code, so the Web proxy in between must hand the status, the body and the media
/// type through untouched. Measured on the REAL TenantsController, with a principal from the REAL ShellAccessFilter.
/// </summary>
public sealed class TenantsProxyRefusalPassThroughTests
{
    private const string Secret = "proxy-refusal-pass-through-tests-signing-secret-long-enough";
    private const string Issuer = "DitenAuth";
    private const string Audience = "DitenClients";
    private static readonly Guid TenantId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid SubscriptionId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    public static TheoryData<string, int, string> Refusals() => new()
    {
        { "assign", 503, """{"title":"Service Unavailable","status":503,"detail":"x","reason_code":"AUDIT_RECORD_UNAVAILABLE"}""" },
        { "activate", 409, """{"isSuccessful":false,"errors":["x"],"statusCode":409,"reason_code":"SUBSCRIPTION_STALE"}""" },
        { "disable-module", 503, """{"title":"Service Unavailable","status":503,"detail":"x","reason_code":"AUDIT_RECORD_UNAVAILABLE"}""" },
        { "assign", 409, """{"isSuccessful":false,"errors":["x"],"statusCode":409,"reason_code":"SUBSCRIPTION_ALREADY_CURRENT"}""" },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_coded_refusal_reaches_the_screen_with_its_status_its_code_and_its_media_type(string route, int status, string body)
    {
        var handler = new FixedAnswer((HttpStatusCode)status, body, status == 503 ? "application/problem+json" : "application/json");
        var controller = Controller(handler);

        var result = route switch
        {
            "assign" => await controller.AssignCommercialSubscriptionProxy(TenantId),
            "activate" => await controller.CommercialSubscriptionActionProxy(TenantId, SubscriptionId, "activate"),
            _ => await controller.DisableModuleEntitlementProxy(TenantId)
        };

        Assert.True(handler.Called);
        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(status, content.StatusCode);
        Assert.Equal(body, content.Content);
        Assert.StartsWith(status == 503 ? "application/problem+json" : "application/json", content.ContentType);
    }

    private static TenantsController Controller(FixedAnswer handler)
    {
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            Issuer, Audience, [new Claim("actor_type", "platform_admin")],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256)));

        var http = new DefaultHttpContext();
        http.Request.Path = "/Platform/Tenants/api";
        http.Request.Cookies = new Cookies(token);
        new ShellAccessFilter(Configuration(), NullLogger<ShellAccessFilter>.Instance).OnAuthorization(
            new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>()));
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

        return new TenantsController(new HttpClient(handler), Configuration())
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private static IConfiguration Configuration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = Secret,
                ["JwtSettings:Issuer"] = Issuer,
                ["JwtSettings:Audience"] = Audience,
                ["GatewayUrl"] = "http://gateway.test"
            })
            .Build();

    private sealed class FixedAnswer(HttpStatusCode status, string body, string mediaType) : HttpMessageHandler
    {
        public bool Called { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Called = true;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) });
        }
    }

    private sealed class Cookies(string token) : IRequestCookieCollection
    {
        private readonly Dictionary<string, string> _values = new() { ["access_token"] = token };
        public string? this[string key] => _values.TryGetValue(key, out var value) ? value : null;
        public int Count => _values.Count;
        public ICollection<string> Keys => _values.Keys;
        public bool ContainsKey(string key) => _values.ContainsKey(key);
        public bool TryGetValue(string key, out string? value)
        {
            var found = _values.TryGetValue(key, out var raw);
            value = raw;
            return found;
        }
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _values.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
