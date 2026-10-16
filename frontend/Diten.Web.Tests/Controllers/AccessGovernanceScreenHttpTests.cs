using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Diten.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-URA-FIX-01 FIX2 (UAS-001) — User Roles and Role Permissions through the REAL web tier: routing, the class-level
/// <c>[Authorize]</c>, the token bridge that turns the access-token cookie into the user, the global ShellAccessFilter,
/// <c>[ValidateAntiForgeryToken]</c>, the status-code-pages middleware, the controller, and the Razor view with its layout.
/// The direct tests in <see cref="AccessGovernanceScreenAccessTests"/> call the actions and skip all of that.
/// <list type="bullet">
/// <item>signed in WITHOUT the key: GET → 403 with no <c>Location</c> (never a redirect), the access-denied face, nothing
/// of the page; POST assign / revoke WITH a valid antiforgery token → 403 JSON, and no write leaves for the gateway;</item>
/// <item>signed in WITH the key (non-vacuity): GET → 200 with the page; POST → the write reaches the gateway, so the
/// antiforgery token the refusal test sends is a valid one;</item>
/// <item>not signed in (measured, unchanged): GET and POST → 302 to the login page, issued by the cookie challenge of
/// the class-level <c>[Authorize]</c>.</item>
/// </list>
/// Nothing leaves the web tier: the gateway is a counting handler, Platform points at a closed local port.
/// </summary>
public abstract class AccessGovernanceScreenHttpTests : IDisposable
{
    // Test-only signing material for a token this test makes and this test's web host checks; not a credential.
    private const string SigningKey = "access-governance-http-test-signing-key-0123456789abcdef";
    private const string Issuer = "access-governance-http-test";
    private const string Audience = "access-governance-http-test";
    private const string ClosedPort = "http://127.0.0.1:9";

    private readonly WebApplicationFactory<Program> _host;
    private readonly WriteCountingGateway _gateway = new();

    protected AccessGovernanceScreenHttpTests(WebApplicationFactory<Program> factory)
    {
        _host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("JwtSettings:Secret", SigningKey);
            builder.UseSetting("JwtSettings:Issuer", Issuer);
            builder.UseSetting("JwtSettings:Audience", Audience);
            builder.UseSetting("GatewayUrl", ClosedPort);
            builder.UseSetting("PlatformServiceUrl", ClosedPort);
            // The controllers' HttpClient (transient, Program.cs) — every write they send is counted here.
            builder.ConfigureTestServices(services =>
                services.AddTransient(_ => new HttpClient(_gateway, disposeHandler: false)));
        });
    }

    protected abstract Type ControllerType { get; }
    protected abstract string Url { get; }
    protected abstract string Key { get; }
    protected abstract string[] ScreenMarkers { get; }    // drawn for a holder of the key, never on the denied face
    protected abstract (string Name, string Value)[] WriteForm { get; }

    public void Dispose() => _host.Dispose();

    [Fact]
    public void The_controller_carries_Authorize_at_class_level()
    {
        Assert.NotNull(ControllerType.GetCustomAttribute<AuthorizeAttribute>(inherit: false));
    }

    [Fact]
    public async Task Signed_in_without_the_key_GET_answers_403_in_place_with_the_access_denied_face()
    {
        using var client = Client();
        using var response = await SendAsync(client, HttpMethod.Get, Url, Token(SubjectA, "auth.users.read"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.Headers.Location);                       // UAS-001: never a redirect
        Assert.Contains("diten-access-denied", html);
        foreach (var marker in ScreenMarkers)
        {
            Assert.DoesNotContain(marker, html);
        }
    }

    [Fact]
    public async Task Signed_in_with_the_key_GET_answers_200_with_the_page()
    {
        using var client = Client();
        using var response = await SendAsync(client, HttpMethod.Get, Url, Token(SubjectA, Key));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("diten-access-denied", html);
        foreach (var marker in ScreenMarkers)
        {
            Assert.Contains(marker, html);
        }
    }

    [Theory]
    [InlineData("assign")]
    [InlineData("revoke")]
    public async Task Signed_in_without_the_key_a_write_with_a_valid_antiforgery_token_is_refused_403_and_sends_nothing(string action)
    {
        using var client = Client();
        var antiforgery = await AntiforgeryAsync(client);
        _gateway.Reset();

        using var response = await PostAsync(client, $"{Url}/{action}", Token(SubjectA, "auth.users.read"), antiforgery);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.ToString());
        Assert.Contains("\"success\":false", body);
        Assert.Equal(0, _gateway.Writes);
    }

    [Theory]
    [InlineData("assign")]
    [InlineData("revoke")]
    public async Task Signed_in_with_the_key_the_same_write_reaches_the_gateway(string action)
    {
        // Non-vacuity for the refusal above: the same token pair passes antiforgery, so the 403 there is the key's.
        using var client = Client();
        var antiforgery = await AntiforgeryAsync(client);
        _gateway.Reset();

        using var response = await PostAsync(client, $"{Url}/{action}", Token(SubjectA, Key), antiforgery);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, _gateway.Writes);
    }

    [Fact]
    public async Task A_write_without_an_antiforgery_token_is_stopped_before_the_action()
    {
        using var client = Client();
        _gateway.Reset();

        using var response = await PostAsync(client, $"{Url}/assign", Token(SubjectA, Key), antiforgery: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _gateway.Writes);
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("POST", "/assign")]
    public async Task Not_signed_in_is_sent_to_the_login_page_by_the_Authorize_challenge(string method, string suffix)
    {
        // Measured, unchanged by this round: no session → 302 to /account/login. The cookie scheme's challenge writes
        // `ReturnUrl` (capital R); the global ShellAccessFilter writes `returnUrl`. Seeing the first means the
        // class-level [Authorize] answered, before any filter ran.
        using var client = Client();
        using var response = await SendAsync(client, new HttpMethod(method), Url + suffix, token: null);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("/account/login", location);
        Assert.Contains("ReturnUrl=", location);
        Assert.Equal(0, _gateway.Writes);
    }

    private const string SubjectA = "7b2c51f0-0000-4000-8000-0000000000a1";

    private HttpClient Client() =>
        _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    // The antiforgery pair is bound to the user (the `sub` claim): a holder of the key with the SAME subject draws the
    // page, and its form token + cookie are then sent with the key-less token of that subject.
    private async Task<(string Cookie, string FormToken)> AntiforgeryAsync(HttpClient client)
    {
        using var page = await SendAsync(client, HttpMethod.Get, Url, Token(SubjectA, Key));
        var html = await page.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        var formToken = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var cookie = page.Headers.GetValues("Set-Cookie")
            .Select(value => value.Split(';')[0])
            .Single(value => value.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
        Assert.False(string.IsNullOrEmpty(formToken), "the page drew no antiforgery token");
        return (cookie, formToken);
    }

    private Task<HttpResponseMessage> PostAsync(HttpClient client, string url, string token, (string Cookie, string FormToken)? antiforgery)
    {
        var fields = WriteForm.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)).ToList();
        if (antiforgery is { } pair)
        {
            fields.Add(new("__RequestVerificationToken", pair.FormToken));
        }

        return SendAsync(client, HttpMethod.Post, url, token, new FormUrlEncodedContent(fields), antiforgery?.Cookie);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string url, string? token, HttpContent? content = null, string? extraCookie = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        var cookies = new List<string>();
        if (token is not null)
        {
            cookies.Add($"access_token={token}");
        }

        if (extraCookie is not null)
        {
            cookies.Add(extraCookie);
        }

        if (cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", cookies));
        }

        request.Headers.Add("Accept", method == HttpMethod.Get ? "text/html" : "application/json");
        return await client.SendAsync(request);
    }

    private static string Token(string subject, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new("sub", subject),
            new("tenant_id", "7b2c51f0-0000-4000-8000-0000000000b2"),
            new("tenantId", "7b2c51f0-0000-4000-8000-0000000000b2"),
            new("actor_type", "tenant_user")
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));
    }

    /// <summary>Counts the WRITES the controllers send (POST / DELETE) and answers them 204; anything else is refused 503,
    /// as the closed port would.</summary>
    private sealed class WriteCountingGateway : HttpMessageHandler
    {
        private int _writes;
        public int Writes => _writes;
        public void Reset() => Interlocked.Exchange(ref _writes, 0);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post || request.Method == HttpMethod.Delete)
            {
                Interlocked.Increment(ref _writes);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}

public sealed class UserRoleAssignmentsHttpTests(WebApplicationFactory<Program> factory)
    : AccessGovernanceScreenHttpTests(factory), IClassFixture<WebApplicationFactory<Program>>
{
    protected override Type ControllerType => typeof(UserRoleAssignmentsController);
    protected override string Url => "/UserRoleAssignments";
    protected override string Key => UserRoleAssignmentsController.AssignRolePermission;
    protected override string[] ScreenMarkers =>
        ["id=\"uraSearch\"", "id=\"uraUserSelect\"", "uraAntiForgery", "Governance/UserRoleAssignments/index.js"];
    protected override (string Name, string Value)[] WriteForm =>
        [("userId", "7b2c51f0-0000-4000-8000-0000000000c1"), ("roleId", "7b2c51f0-0000-4000-8000-0000000000c2")];
}

public sealed class RoleAssignmentsHttpTests(WebApplicationFactory<Program> factory)
    : AccessGovernanceScreenHttpTests(factory), IClassFixture<WebApplicationFactory<Program>>
{
    protected override Type ControllerType => typeof(RoleAssignmentsController);
    protected override string Url => "/RoleAssignments";
    protected override string Key => RoleAssignmentsController.AssignPermissionPermission;
    protected override string[] ScreenMarkers =>
        ["id=\"raRoleSelect\"", "id=\"raRoot\"", "data-menu-url", "raAntiForgery", "Governance/RoleAssignments/index.js"];
    protected override (string Name, string Value)[] WriteForm =>
        [("roleId", "7b2c51f0-0000-4000-8000-0000000000d1"), ("permissionId", "7b2c51f0-0000-4000-8000-0000000000d2")];
}
