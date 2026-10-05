using System.Globalization;
using System.Net;
using System.Text;
using Diten.Web;
using Diten.Web.Controllers;
using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Web.Tests.Auth;

/// <summary>
/// BL-529 FIX3 — the refusals BL-529 added reach the person in their language: the REAL <see cref="AuthGateway"/> turns
/// AuthService's code (AUTH_TOO_MANY_REQUESTS, AUTH_PASSWORD_CHANGED_MEANWHILE, AUTH_ACCOUNT_DEACTIVATED) into the
/// SharedResource sentence, and the account controller passes a rate-limited "forgot password" on as 429 — never the
/// "sent" it used to say whatever happened.
/// </summary>
public sealed class PasswordDoorRefusalBridgeTests
{
    public static TheoryData<string> Cultures() => ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task A_rate_limited_forgot_password_reads_in_the_readers_language(string culture)
    {
        using var _ = new CultureScope(culture);
        var localizer = Localizer();

        var result = await Gateway(HttpStatusCode.TooManyRequests, Coded("AUTH_TOO_MANY_REQUESTS", "Too many requests. Try again later."), localizer)
            .ForgotPlatformPasswordAsync("a@b.test");

        Assert.False(result.Success);
        Assert.Equal(429, result.StatusCode);
        Assert.Equal(localizer["Auth.Error.TooManyRequests"].Value, result.ErrorMessage);
    }

    [Theory]
    [InlineData("AUTH_PASSWORD_CHANGED_MEANWHILE", "Auth.Error.PasswordChangedMeanwhile", HttpStatusCode.Conflict)]
    [InlineData("AUTH_ACCOUNT_DEACTIVATED", "Auth.Error.AccountDeactivated", HttpStatusCode.Conflict)]
    public async Task The_other_new_refusals_read_by_their_code(string code, string key, HttpStatusCode status)
    {
        using var _ = new CultureScope("tr");
        var localizer = Localizer();

        var result = await Gateway(status, Coded(code, "English sentence that must not show."), localizer)
            .ResetTenantPasswordAsync("a@b.test", "t", "Passw0rd!x");

        Assert.Equal(localizer[key].Value, result.ErrorMessage);
        Assert.DoesNotContain("English sentence", result.ErrorMessage);
        Assert.Equal((int)status, result.StatusCode);
    }

    [Fact]
    public async Task The_account_controller_passes_a_rate_limited_forgot_password_on_as_429()
    {
        var controller = new AccountController(new RefusingGateway(), null!, null!, new StubEnvironment())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var answer = await controller.PlatformForgotPassword(new AccountController.ForgotPasswordRequest("a@b.test"), CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(answer);
        Assert.Equal(429, status.StatusCode);
        Assert.Contains("too many", System.Text.Json.JsonSerializer.Serialize(status.Value), StringComparison.OrdinalIgnoreCase);
    }

    private static string Coded(string code, string english)
        => $$"""{"data":null,"statusCode":429,"isSuccessful":false,"errors":["{{english}}"],"errorCodes":[{"code":"{{code}}"}]}""";

    private static IStringLocalizer<SharedResource> Localizer()
        => new StringLocalizer<SharedResource>(new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance));

    private static AuthGateway Gateway(HttpStatusCode status, string body, IStringLocalizer<SharedResource> localizer)
        => new(new HttpClient(new FixedAnswer(status, body)) { BaseAddress = new Uri("http://gateway.test") },
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, NullLogger<AuthGateway>.Instance, localizer);

    private sealed class FixedAnswer(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    private sealed class RefusingGateway : IAuthGateway
    {
        public Task<AuthBridgeResult> ForgotPlatformPasswordAsync(string email, CancellationToken ct = default)
            => Task.FromResult(new AuthBridgeResult(false, null, null, null, null, "Too many requests. Please wait.", StatusCode: 429));

        public Task<AuthBridgeResult> LoginTenantAsync(string email, string password, Guid tenantId, bool rememberMe = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthBridgeResult> VerifyTenantMfaAsync(string challengeId, string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthBridgeResult> ResendTenantMfaAsync(string challengeId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthBridgeResult> LoginPlatformAsync(string email, string password, bool rememberMe = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthBridgeResult> ChangePlatformPasswordAsync(string currentPassword, string newPassword, bool rememberMe = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AuthBridgeResult> ChangeTenantPasswordAsync(string currentPassword, string newPassword, bool rememberMe = false, CancellationToken ct = default) => throw new NotSupportedException();
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
