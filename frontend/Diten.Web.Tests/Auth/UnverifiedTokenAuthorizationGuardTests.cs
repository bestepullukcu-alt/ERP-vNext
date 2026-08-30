using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
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
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.Web.Tests.Auth;

/// <summary>
/// TenantsController decided who you are by DECODING the access-token cookie — <c>ReadJwtToken</c>, which
/// parses a JWT and never checks its signature. Anyone who could put a string in a cookie could write
/// <c>actor_type: platform_admin</c> into it and be believed, because the only thing the controller verified
/// was <c>exp</c>, a number inside the very payload it had not authenticated.
///
/// <para>The verified claims were already sitting next to it: <c>ShellAccessFilter</c> is a GLOBAL MVC
/// authorization filter (Program.cs) that validates issuer, audience, lifetime AND signing key, and assigns
/// the resulting principal to <c>HttpContext.User</c>. Every decision here now reads that principal.</para>
///
/// <para>⚠ THE SEPARATION THAT MATTERS. Reading the raw cookie is still correct for CARRYING the token
/// downstream as a Bearer header — the gateway validates it there. What was wrong was BELIEVING its contents.
/// Carrier: kept. Decider: moved to <c>User</c>. Same split the gateway made in e28aa858.</para>
/// </summary>
public sealed class UnverifiedTokenAuthorizationGuardTests
{
    private const string Secret = "unverified-token-guard-tests-signing-secret-long-enough-for-hs256";
    private const string Issuer = "DitenAuth";
    private const string Audience = "DitenClients";
    private const string QuotaViewPermission = "platform.tenants.quotas.view";
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    // ── The measurement: what claim types does ShellAccessFilter's principal actually carry? ──────────────

    [Fact]
    public void MEASUREMENT_the_claim_types_on_the_verified_principal()
    {
        /*
         * Not a guess. The controller's two predicates look for exactly two claim types: "permission" and
         * ClaimTypes.Role. Moving the decision from the raw token to User is only safe if those types survive
         * validation — and JwtSecurityTokenHandler.ValidateToken MAPS inbound claim types by default (unlike
         * ReadJwtToken, which does not map at all). This runs the real filter and records the result.
         */
        var principal = PrincipalFromShellFilter(TokenFor(
            "platform_admin",
            new Claim("permission", QuotaViewPermission),
            new Claim("role", "PlatformAdmin")));

        var wireTypes = string.Join(",", principal.Claims
            .Where(claim => claim.Type is "actor_type" or "permission" or "role")
            .Select(claim => claim.Type)
            .OrderBy(type => type, StringComparer.Ordinal));

        // "actor_type" and "permission" are not in the default inbound map, so they arrive untouched.
        Assert.Equal("actor_type,permission", wireTypes);

        // "role" IS in the default inbound map: it becomes ClaimTypes.Role, which is what
        // ClaimsContainPlatformRole has always looked for. The raw "role" type is gone from the principal.
        Assert.Equal("PlatformAdmin", principal.FindFirst(ClaimTypes.Role)?.Value);
        Assert.Null(principal.FindFirst("role"));

        Assert.Equal("platform_admin", principal.FindFirst("actor_type")?.Value);
        Assert.Equal(QuotaViewPermission, principal.FindFirst("permission")?.Value);
    }

    // ── (a) verified platform actor → allowed ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_platform_admin_on_the_VERIFIED_principal_is_allowed_through()
    {
        var token = TokenFor("platform_admin");
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"ok\":true}");
        var controller = ControllerWith(PrincipalFromShellFilter(token), token, handler);

        var result = await controller.DeleteProxy(TenantId);

        Assert.True(handler.Called);
        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(result).StatusCode);
    }

    [Fact]
    public async Task The_cookie_is_still_the_CARRIER_even_though_it_is_no_longer_the_DECIDER()
    {
        // The half of TryGetPlatformAccessToken that must NOT change: the raw token string still goes
        // downstream as Bearer. Deleting the cookie read would break every proxy on this controller.
        var token = TokenFor("platform_admin");
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"ok\":true}");
        var controller = ControllerWith(PrincipalFromShellFilter(token), token, handler);

        await controller.DeleteProxy(TenantId);

        Assert.Equal(new AuthenticationHeaderValue("Bearer", token).ToString(), handler.Authorization?.ToString());
    }

    // ── (b) verified non-platform actor → refused ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_tenant_user_on_the_VERIFIED_principal_is_refused()
    {
        var token = TokenFor("tenant_user");
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"ok\":true}");
        var controller = ControllerWith(PrincipalFromShellFilter(token), token, handler);

        var result = await controller.DeleteProxy(TenantId);

        Assert.False(handler.Called);
        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task The_VERIFIED_principal_wins_over_a_cookie_that_claims_more_than_it()
    {
        /*
         * ⚠ RED BEFORE THE FIX. The signed session says tenant_user; the cookie the controller used to decode
         * says platform_admin. Old code read the cookie and let it through. The token here is genuinely
         * signed, so this is not even the tampering case — it is simply the controller trusting the wrong one
         * of two sources, which is what made tampering pay.
         */
        var controller = ControllerWith(
            PrincipalFromShellFilter(TokenFor("tenant_user")),
            TokenFor("platform_admin"),
            out var handler);

        var result = await controller.DeleteProxy(TenantId);

        Assert.False(handler.Called);
        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    // ── (c) THE GUARD: anonymous User, valid-looking cookie ──────────────────────────────────────────────

    [Fact]
    public async Task An_ANONYMOUS_user_is_refused_even_when_the_cookie_carries_a_platform_admin_token()
    {
        /*
         * ⚠ THE GUARD THIS ROUND EXISTS FOR — RED BEFORE THE FIX. ShellAccessFilter clears the cookies on the
         * RESPONSE and blanks HttpContext.User when validation fails, but HttpRequest.Cookies still holds what
         * the browser sent for the rest of THIS request. So "User is anonymous while the cookie still parses"
         * is the exact shape of a rejected token, and the old code read the rejected token and said yes.
         *
         * MUTATION GUARD: restore the ReadJwtToken fallback in TryGetPlatformAccessToken and this goes red.
         */
        var controller = ControllerWith(
            new ClaimsPrincipal(new ClaimsIdentity()),
            TokenFor("platform_admin"),
            out var handler);

        var result = await controller.DeleteProxy(TenantId);

        Assert.False(handler.Called);
        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    // ── (d) the permission gate ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_quota_permission_claim_on_the_VERIFIED_principal_opens_the_gate()
    {
        var token = TokenFor("platform_admin", new Claim("permission", QuotaViewPermission));
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"data\":[]}");
        var controller = ControllerWith(PrincipalFromShellFilter(token), token, handler);

        var result = await controller.QuotaStatusProxy(TenantId, CancellationToken.None);

        Assert.True(handler.Called);
        Assert.False(result is ObjectResult { StatusCode: StatusCodes.Status403Forbidden });
    }

    [Fact]
    public async Task A_platform_role_claim_opens_the_gate_through_the_MAPPED_claim_type()
    {
        // The mapping measured above, asserted as behaviour: the wire claim is "role", the principal carries
        // ClaimTypes.Role, and ClaimsContainPlatformRole must still find it.
        var token = TokenFor("platform_admin", new Claim("role", "SuperAdmin"));
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"data\":[]}");
        var controller = ControllerWith(PrincipalFromShellFilter(token), token, handler);

        var result = await controller.QuotaStatusProxy(TenantId, CancellationToken.None);

        Assert.True(handler.Called);
        Assert.False(result is ObjectResult { StatusCode: StatusCodes.Status403Forbidden });
    }

    [Fact]
    public async Task A_verified_principal_without_the_permission_or_a_platform_role_is_refused()
    {
        var token = TokenFor("platform_admin", new Claim("permission", "platform.tenants.view"));
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"data\":[]}");
        var controller = ControllerWith(PrincipalFromShellFilter(token), token, handler);

        var result = await controller.QuotaStatusProxy(TenantId, CancellationToken.None);

        Assert.False(handler.Called);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task A_permission_that_exists_ONLY_in_the_cookie_does_not_open_the_gate()
    {
        /*
         * ⚠ RED BEFORE THE FIX. HasPermission checked User first and then fell back to decoding the cookie —
         * so a principal with no permission at all was overruled by an unverified payload that claimed one.
         */
        var controller = ControllerWith(
            new ClaimsPrincipal(new ClaimsIdentity()),
            TokenFor("platform_admin", new Claim("permission", QuotaViewPermission)),
            out var handler);

        var result = await controller.QuotaStatusProxy(TenantId, CancellationToken.None);

        Assert.False(handler.Called);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────────────────

    private static string TokenFor(string actorType, params Claim[] extra)
    {
        var claims = new List<Claim> { new("actor_type", actorType) };
        claims.AddRange(extra);

        var token = new JwtSecurityToken(
            Issuer,
            Audience,
            claims,
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(30),
            new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
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

    /// <summary>
    /// Runs the REAL ShellAccessFilter over a request carrying <paramref name="token"/> and returns the
    /// principal it leaves on HttpContext.User. Nothing here re-implements the validation — if the filter
    /// ever changes how it maps claims, these tests change with it.
    /// </summary>
    private static ClaimsPrincipal PrincipalFromShellFilter(string token)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = "/Platform/Tenants";
        http.Request.Cookies = new FakeCookies(new Dictionary<string, string> { ["access_token"] = token });

        new ShellAccessFilter(Configuration()).OnAuthorization(
            new AuthorizationFilterContext(
                new ActionContext(http, new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>()));

        return http.User;
    }

    private static TenantsController ControllerWith(ClaimsPrincipal user, string? cookieToken, out RecordingHandler handler)
    {
        handler = new RecordingHandler(HttpStatusCode.OK, "{\"ok\":true}");
        return ControllerWith(user, cookieToken, handler);
    }

    private static TenantsController ControllerWith(ClaimsPrincipal user, string? cookieToken, RecordingHandler handler)
    {
        var http = new DefaultHttpContext { User = user };
        http.Request.Path = "/Platform/Tenants/api";
        if (cookieToken is not null)
        {
            http.Request.Cookies = new FakeCookies(new Dictionary<string, string> { ["access_token"] = cookieToken });
        }

        return new TenantsController(new HttpClient(handler), Configuration())
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public RecordingHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public bool Called { get; private set; }

        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Called = true;
            Authorization ??= request.Headers.Authorization;

            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class FakeCookies : IRequestCookieCollection
    {
        private readonly IDictionary<string, string> _values;

        public FakeCookies(IDictionary<string, string> values) => _values = values;

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
