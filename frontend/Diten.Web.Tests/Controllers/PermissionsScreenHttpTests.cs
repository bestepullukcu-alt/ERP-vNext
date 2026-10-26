using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Xml.Linq;
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
/// WP-USERS-ROLES-SMALL-01 (BL-617, UAS-001) — the permission catalog screen through the REAL web tier: routing, the
/// class-level <c>[Authorize]</c>, the token bridge, the global ShellAccessFilter, the controller and the Razor view with
/// its layout. The Host / Token pattern is copied from <see cref="AccessGovernanceScreenHttpTests"/>, not inherited: that
/// base carries write tests this read-only screen has no action for.
/// <list type="bullet">
/// <item>signed in WITHOUT <c>auth.roles.read</c>: GET → 403 in place (no <c>Location</c>), the access-denied face that
/// names the screen, nothing of the page; <c>lookups</c> → 403 JSON and NO request leaves for the gateway;</item>
/// <item>signed in WITH the key (non-vacuity): GET → 200 with the page; <c>lookups</c> → exactly one gateway request.</item>
/// </list>
/// The gateway is a handler that counts EVERY request (GET included); Platform points at a closed local port.
/// </summary>
public sealed class PermissionsScreenHttpTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    // Test-only signing material for a token this test makes and this test's web host checks; not a credential.
    private const string SigningKey = "permissions-screen-http-test-signing-key-0123456789abcdef";
    private const string Issuer = "permissions-screen-http-test";
    private const string Audience = "permissions-screen-http-test";
    private const string ClosedPort = "http://127.0.0.1:9";
    private const string Url = "/Permissions";
    private const string SubjectA = "7b2c51f0-0000-4000-8000-0000000000a1";

    // Drawn for a holder of the key, never on the denied face: the heading block, _Filter, _DataTable and the script.
    private static readonly string[] ScreenMarkers =
    [
        "<h5 class=\"mb-0\">", "id=\"inlineFilterHost\"", "id=\"filterModule\"", "id=\"dt-permissions\"",
        "id=\"skeleton-loader\"", "Governance/Permissions/index.js"
    ];

    private readonly WebApplicationFactory<Program> _host;
    private readonly CountingGateway _gateway = new();

    public PermissionsScreenHttpTests(WebApplicationFactory<Program> factory)
    {
        _host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("JwtSettings:Secret", SigningKey);
            builder.UseSetting("JwtSettings:Issuer", Issuer);
            builder.UseSetting("JwtSettings:Audience", Audience);
            builder.UseSetting("GatewayUrl", ClosedPort);
            builder.UseSetting("PlatformServiceUrl", ClosedPort);
            // The controllers' HttpClient (transient, Program.cs) — every request they send is counted here.
            builder.ConfigureTestServices(services =>
                services.AddTransient(_ => new HttpClient(_gateway, disposeHandler: false)));
        });
    }

    public void Dispose() => _host.Dispose();

    [Fact]
    public void The_controller_carries_Authorize_at_class_level_and_reads_the_backend_key()
    {
        Assert.NotNull(typeof(PermissionsController).GetCustomAttribute<AuthorizeAttribute>(inherit: false));
        Assert.Equal("auth.roles.read", PermissionsController.ReadPermission);
    }

    [Fact]
    public async Task Signed_in_without_the_key_GET_answers_403_in_place_with_the_access_denied_face_only()
    {
        using var client = Client();
        using var response = await SendAsync(client, Url, Token(SubjectA, "auth.users.read"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.Headers.Location);                       // UAS-001: never a redirect
        Assert.Contains("diten-access-denied", html);
        Assert.True(ResourceValues("PermissionsTitle").Any(html.Contains), "the access-denied face does not name the screen");
        foreach (var marker in ScreenMarkers)
        {
            Assert.DoesNotContain(marker, html);
        }

        foreach (var description in ResourceValues("PageDescription"))
        {
            Assert.DoesNotContain(description, html);
        }

        // No gateway count here: the shell layout makes its own calls (measured: 2) on every page render, whatever
        // the screen. The screen's own server-side call is Lookups, counted in the tests below.
    }

    [Fact]
    public async Task Signed_in_with_the_key_GET_answers_200_with_the_page()
    {
        using var client = Client();
        using var response = await SendAsync(client, Url, Token(SubjectA, PermissionsController.ReadPermission));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("diten-access-denied", html);
        foreach (var marker in ScreenMarkers)
        {
            Assert.Contains(marker, html);
        }

        Assert.True(ResourceValues("PageDescription").Any(html.Contains), "the page drew no description");
    }

    [Fact]
    public async Task Signed_in_without_the_key_lookups_is_refused_403_before_any_gateway_request()
    {
        using var client = Client();
        _gateway.Reset();

        using var response = await SendAsync(client, $"{Url}/lookups", Token(SubjectA, "auth.users.read"), accept: "application/json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.ToString());
        Assert.Contains("\"success\":false", body);
        Assert.Equal(0, _gateway.Requests);
    }

    [Fact]
    public async Task Signed_in_with_the_key_lookups_sends_exactly_one_gateway_request()
    {
        // Non-vacuity for the refusal above: the same token shape with the key reaches the gateway (the token carries
        // tenantId, without which the controller would answer empty and count 0 either way).
        using var client = Client();
        _gateway.Reset();

        using var response = await SendAsync(client, $"{Url}/lookups", Token(SubjectA, PermissionsController.ReadPermission), accept: "application/json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, _gateway.Requests);
        Assert.Contains("\"value\":\"auth\"", body);
    }

    [Fact]
    public async Task Not_signed_in_is_sent_to_the_login_page_by_the_Authorize_challenge()
    {
        using var client = Client();
        using var response = await SendAsync(client, Url, token: null);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.Contains("/account/login", location);
        Assert.Contains("ReturnUrl=", location);
        Assert.Equal(0, _gateway.Requests);
    }

    private HttpClient Client() =>
        _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string url, string? token, string accept = "text/html")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null)
        {
            request.Headers.Add("Cookie", $"access_token={token}");
        }

        request.Headers.Add("Accept", accept);
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

    /// <summary>A screen resource in all seven languages, raw and HTML-encoded (Razor encodes non-ASCII), so the check
    /// does not depend on the culture the test host picks.</summary>
    private static IReadOnlyList<string> ResourceValues(string name)
    {
        var dir = Path.Combine(WebRoot(), "Resources", "Views", "Governance", "Permissions");
        var values = Directory.GetFiles(dir, "PermissionsIndex.*.resx")
            .Select(file => XDocument.Load(file).Root!.Elements("data")
                .Single(data => (string?)data.Attribute("name") == name).Element("value")!.Value)
            .ToList();
        Assert.Equal(7, values.Count);
        return values.Concat(values.Select(value => HtmlEncoder.Default.Encode(value))).Distinct().ToList();
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repository root not found"), "frontend", "Diten.Web");
    }

    /// <summary>Counts EVERY request the controllers send. The catalog read answers one module list; anything else 503.</summary>
    private sealed class CountingGateway : HttpMessageHandler
    {
        private int _requests;
        public int Requests => _requests;
        public void Reset() => Interlocked.Exchange(ref _requests, 0);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requests);
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/permissions")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"isSuccessful\":true,\"statusCode\":200,\"data\":[{\"module\":\"auth\"},{\"module\":\"auth\"},{\"module\":\"mdm\"}]}",
                        Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
