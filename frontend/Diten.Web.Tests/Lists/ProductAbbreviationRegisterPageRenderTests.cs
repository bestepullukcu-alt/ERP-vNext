using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.Web.Tests.Lists;

/// <summary>
/// WP-MDM-ABB-LIST-01 — the ABB register page through the REAL web tier: routing, the token bridge that turns the
/// access-token cookie into the user, the controller's 403, the Razor view with its layout, the list shell. Nothing is
/// called beyond the web tier (the gateway and Platform addresses point at a closed local port, which every shell
/// lookup treats as "unavailable").
/// <list type="bullet">
/// <item>UAS-001: a signed-in user without <c>mdm.product-abbreviations.read</c> gets the access-denied surface inside
/// the shell and NOTHING of the register — no filter, no skeleton, no table, no script payload.</item>
/// <item>A reader gets the shared list shell: the server-mode v2 table, the shared shaped skeleton, and no selection
/// column or bulk bar (an abbreviation is never changed in bulk).</item>
/// </list>
/// </summary>
public sealed class ProductAbbreviationRegisterPageRenderTests : IClassFixture<WebApplicationFactory<Program>>
{
    // Test-only signing material for a token this test makes and this test's web host checks; not a credential.
    private const string SigningKey = "abb-list-render-test-signing-key-0123456789abcdef";
    private const string Issuer = "abb-list-render-test";
    private const string Audience = "abb-list-render-test";
    private const string ClosedPort = "http://127.0.0.1:9";

    private readonly WebApplicationFactory<Program> _factory;

    public ProductAbbreviationRegisterPageRenderTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task A_user_without_read_permission_sees_the_access_denied_surface_and_nothing_of_the_register()
    {
        var (status, html) = await GetPageAsync("mdm.global-products.read");

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Contains("diten-access-denied", html);
        Assert.DoesNotContain("dt-product-abbreviation-register", html);
        Assert.DoesNotContain("data-table-skeleton", html);
        Assert.DoesNotContain("inlineFilterHost", html);
        Assert.DoesNotContain("product-abbreviation-register-l10n", html);
        Assert.DoesNotContain("ProductAbbreviationRegister/index.js", html);
    }

    [Fact]
    public async Task A_reader_gets_the_shared_server_mode_list_shell_without_a_selection_column()
    {
        var (status, html) = await GetPageAsync("mdm.product-abbreviations.read");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.DoesNotContain("diten-access-denied", html);
        Assert.Contains(
            "<table id=\"dt-product-abbreviation-register\" data-dt-standard=\"v2\" data-dt-data-mode=\"server\" "
            + "class=\"datatables-product-abbreviation-register table border-top\">",
            html);
        Assert.Contains("data-table-skeleton", html);
        Assert.DoesNotContain("dt-checkboxes-select-all", html);
        Assert.DoesNotContain("bulkActionBar", html);
        Assert.Contains("filterGlobalProductId", html);
        Assert.Contains("ProductAbbreviationRegister/index.js", html);
    }

    private async Task<(HttpStatusCode Status, string Html)> GetPageAsync(params string[] permissions)
    {
        using var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("JwtSettings:Secret", SigningKey);
                builder.UseSetting("JwtSettings:Issuer", Issuer);
                builder.UseSetting("JwtSettings:Audience", Audience);
                builder.UseSetting("GatewayUrl", ClosedPort);
                builder.UseSetting("PlatformServiceUrl", ClosedPort);
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var claims = new List<Claim>
        {
            new("sub", Guid.NewGuid().ToString("D")),
            new("tenant_id", Guid.NewGuid().ToString("D")),
            new("actor_type", "tenant_user")
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/MDM/ProductAbbreviationRegister");
        request.Headers.Add("Cookie", $"access_token={token}");
        request.Headers.Add("Accept", "text/html");
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
