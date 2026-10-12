using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Diten.Web;
using Diten.Web.Controllers;
using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Web.Tests.Auth;

/// <summary>
/// WP-USERS-ROLES-CLOSE-FIX1 items 2–3 (BL-496, KR4) — through the REAL <see cref="AuthGateway"/> and account controller:
/// <list type="bullet">
/// <item>a coded refusal of a sign-in / password door reaches the page in the reader's language, in the shape
/// diten-refusal.js reads (<c>{ errors:[sentence], local:true }</c>);</item>
/// <item>an UNCODED refusal (the service's own English) never reaches the page: the door says its own sentence;</item>
/// <item>the set-password page, rendered for real, draws NO form (and carries no link) for a deactivated account's link
/// and for a dead link — each with its own sentence — and the form as before for a usable link.</item>
/// </list>
/// </summary>
public sealed class LoginDoorRefusalTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ClosedPort = "http://127.0.0.1:9";
    private readonly WebApplicationFactory<Program> _factory;

    public LoginDoorRefusalTests(WebApplicationFactory<Program> factory) => _factory = factory;

    public static TheoryData<string> Cultures() => ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task A_coded_sign_in_refusal_reaches_the_page_in_the_readers_language(string culture)
    {
        using var _ = new CultureScope(culture);
        var localizer = Localizer();
        var controller = Controller(Gateway(HttpStatusCode.Unauthorized, Coded("AUTH_INVALID_CREDENTIALS", "Invalid email or password."), localizer), localizer);

        var answer = Assert.IsType<ObjectResult>(await controller.Login(new AccountController.LoginRequest("a@b.test", "x", Guid.NewGuid(), null), CancellationToken.None));

        Assert.Equal(401, answer.StatusCode);
        var (detail, errors, local) = Body(answer);
        Assert.Equal(localizer["Auth.Error.InvalidCredentials"].Value, detail);
        Assert.Equal(new[] { detail }, errors);
        Assert.True(local);
        if (culture != "en") Assert.DoesNotContain("Invalid email or password", detail, StringComparison.Ordinal); // en says the same words
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task An_uncoded_refusal_never_reaches_the_page_in_the_services_english(string culture)
    {
        using var _ = new CultureScope(culture);
        var localizer = Localizer();
        var uncoded = """{"data":null,"statusCode":401,"isSuccessful":false,"errors":["Some English sentence."]}""";

        var login = Assert.IsType<ObjectResult>(await Controller(Gateway(HttpStatusCode.Unauthorized, uncoded, localizer), localizer)
            .Login(new AccountController.LoginRequest("a@b.test", "x", Guid.NewGuid(), null), CancellationToken.None));
        var link = Assert.IsType<ObjectResult>(await Controller(Gateway(HttpStatusCode.BadRequest, uncoded, localizer), localizer)
            .SetPassword(new AccountController.ResetPasswordRequest("a@b.test", "t", "Passw0rd!x", "Passw0rd!x"), CancellationToken.None));

        Assert.Equal(localizer["Auth.Error.LoginFailed"].Value, Body(login).Detail);
        Assert.Equal(localizer["Auth.Error.LinkInvalid"].Value, Body(link).Detail);
        Assert.DoesNotContain("English", Body(login).Detail + Body(link).Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_doors_own_check_of_the_request_is_said_in_the_readers_language()
    {
        using var _ = new CultureScope("tr");
        var localizer = Localizer();
        var controller = Controller(Gateway(HttpStatusCode.OK, "{}", localizer), localizer);

        var answer = Assert.IsType<ObjectResult>(await controller.SetPassword(
            new AccountController.ResetPasswordRequest("a@b.test", "t", "one", "two"), CancellationToken.None));

        Assert.Equal(400, answer.StatusCode);
        Assert.Equal(localizer["Auth.Error.RequestInvalid"].Value, Body(answer).Detail);
    }

    // ── item 3 (KR4): the page, rendered for real ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(409, "Auth.Error.AccountDeactivated")]
    [InlineData(400, "Auth.Error.LinkInvalid")]
    [InlineData(429, "Auth.Error.TooManyRequests")]
    public async Task A_refused_link_draws_no_form_and_carries_no_link_only_its_sentence(int checkStatus, string key)
    {
        var (status, html) = await SetPasswordPageAsync(new LinkCheckGateway(checkStatus), "tr");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.DoesNotContain("id=\"resetPasswordForm\"", html);
        Assert.DoesNotContain("id=\"newPassword\"", html);
        Assert.DoesNotContain("secret-link-token", html);           // the link is not echoed into the page
        Assert.Contains("id=\"linkRefusal\"", html);
        Assert.Contains(TurkishSentence(key), WebUtility.HtmlDecode(html)); // Razor encodes non-ASCII letters
    }

    [Fact]
    public async Task A_dead_link_is_never_called_deactivated()
    {
        var (_, html) = await SetPasswordPageAsync(new LinkCheckGateway(400), "tr");

        Assert.DoesNotContain(TurkishSentence("Auth.Error.AccountDeactivated"), WebUtility.HtmlDecode(html));
        Assert.Contains(TurkishSentence("Auth.Error.LinkInvalid"), WebUtility.HtmlDecode(html));
    }

    [Theory]
    [InlineData(204)]
    [InlineData(503)] // the service unreachable: the form as before — the redemption answers for itself
    public async Task A_usable_link_or_an_unreachable_service_draws_the_form_as_before(int checkStatus)
    {
        var (status, html) = await SetPasswordPageAsync(new LinkCheckGateway(checkStatus), "tr");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("id=\"resetPasswordForm\"", html);
        Assert.DoesNotContain("id=\"linkRefusal\"", html);
        Assert.Contains("diten-refusal.js", html);
    }

    [Fact]
    public async Task A_link_without_token_draws_no_form_and_never_asks_the_service()
    {
        var gateway = new LinkCheckGateway(204);

        var (_, html) = await SetPasswordPageAsync(gateway, "tr", query: "?email=a%40b.test");

        Assert.DoesNotContain("id=\"resetPasswordForm\"", html);
        Assert.Equal(0, gateway.Checks);
    }

    private async Task<(HttpStatusCode Status, string Html)> SetPasswordPageAsync(LinkCheckGateway gateway, string culture,
        string query = "?email=a%40b.test&token=secret-link-token")
    {
        using var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("GatewayUrl", ClosedPort);
                builder.UseSetting("PlatformServiceUrl", ClosedPort);
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IAuthGateway>();
                    services.AddSingleton<IAuthGateway>(gateway);
                });
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/account/set-password{query}&culture={culture}".Replace("?&", "?"));
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static string TurkishSentence(string key)
    {
        using var _ = new CultureScope("tr");
        return Localizer()[key].Value;
    }

    private static (string Detail, string[] Errors, bool Local) Body(ObjectResult result)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        var root = doc.RootElement;
        return (root.GetProperty("detail").GetString()!,
            root.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToArray(),
            root.GetProperty("local").GetBoolean());
    }

    private static string Coded(string code, string english)
        => $$"""{"data":null,"statusCode":401,"isSuccessful":false,"errors":["{{english}}"],"errorCodes":[{"code":"{{code}}"}]}""";

    private static IStringLocalizer<SharedResource> Localizer()
        => new StringLocalizer<SharedResource>(new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance));

    private static AuthGateway Gateway(HttpStatusCode status, string body, IStringLocalizer<SharedResource> localizer)
        => new(new HttpClient(new FixedAnswer(status, body)) { BaseAddress = new Uri("http://gateway.test") },
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, NullLogger<AuthGateway>.Instance, localizer);

    private static AccountController Controller(IAuthGateway gateway, IStringLocalizer<SharedResource> localizer)
        => new(gateway, null!, null!, new StubEnvironment(), localizer)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private sealed class FixedAnswer(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    /// <summary>Answers only the link pre-check, with a fixed status (as the real bridge reports it).</summary>
    private sealed class LinkCheckGateway(int status) : IAuthGateway
    {
        private int _checks;
        public int Checks => Volatile.Read(ref _checks);

        public Task<AuthBridgeResult> CheckTenantPasswordLinkAsync(string email, string token, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _checks);
            return Task.FromResult(new AuthBridgeResult(status is >= 200 and < 300, null, null, null, null, null, StatusCode: status));
        }

        public Task<AuthBridgeResult> LoginTenantAsync(string email, string password, Guid tenantId, bool rememberMe = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthBridgeResult> VerifyTenantMfaAsync(string challengeId, string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthBridgeResult> ResendTenantMfaAsync(string challengeId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthBridgeResult> LoginPlatformAsync(string email, string password, bool rememberMe = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthBridgeResult> ChangePlatformPasswordAsync(string currentPassword, string newPassword, bool rememberMe = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthBridgeResult> ChangeTenantPasswordAsync(string currentPassword, string newPassword, bool rememberMe = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthBridgeResult> ForgotPlatformPasswordAsync(string email, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthBridgeResult> ResetPlatformPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthBridgeResult> ResetTenantPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthBridgeResult> RefreshAsync(string accessToken, string refreshToken, Guid? tenantId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task LogoutAsync(string accessToken, string refreshToken, Guid? tenantId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class StubEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Diten.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _ui = CultureInfo.CurrentUICulture;

        public CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo(name);

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _ui;
        }
    }
}
