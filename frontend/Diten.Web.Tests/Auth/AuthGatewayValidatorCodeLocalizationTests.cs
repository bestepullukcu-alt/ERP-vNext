using System.Globalization;
using System.Net;
using System.Text;
using Diten.Web;
using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Web.Tests.Auth;

/// <summary>
/// WP-USERS-ERROR-CODES-01 (CT decision B) — AuthService's password VALIDATORS now put their codes on the wire
/// ({ title, status, detail, traceId, errorCodes }). The real <see cref="AuthGateway"/> must say them with the keys
/// SharedResource already has, in the reader's language — no new key, and never the English <c>detail</c>.
/// The body below is what AuthService answers (PasswordValidatorCodeEndpointTests pins it over HTTP).
/// </summary>
public sealed class AuthGatewayValidatorCodeLocalizationTests
{
    private const string Body = """{"title":"Validation failed","status":400,"detail":"Validation failed: \n -- Email: Email is required. Severity: Error\n -- Token: Reset token is required. Severity: Error\n -- NewPassword: New password is required. Severity: Error","traceId":"t","errorCodes":[{"code":"password.reset_email_required","params":null},{"code":"password.reset_token_required","params":null},{"code":"password.new_required","params":null}]}""";
    private const string TooLongBody = """{"title":"Validation failed","status":400,"detail":"Validation failed: \n -- NewPassword: Password can be at most 128 characters. Severity: Error","traceId":"t","errorCodes":[{"code":"password.too_long","params":{"maxLength":"128"}}]}""";

    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("zh")]
    [InlineData("ar")]
    [InlineData("ru")]
    public async Task The_validators_codes_read_as_the_existing_SharedResource_sentences(string culture)
    {
        using var _ = new CultureScope(culture);
        var localizer = Localizer();
        var expected = string.Join(" ", new[] { "Password.Error.ResetEmailRequired", "Password.Error.ResetTokenRequired", "Password.Error.NewRequired" }
            .Select(key =>
            {
                var text = localizer[key];
                Assert.False(text.ResourceNotFound, $"SharedResource.{culture} has no {key} — a new key would be needed.");
                return text.Value;
            }));

        var result = await Gateway(Body, localizer).ResetTenantPasswordAsync("", "", "");

        Assert.False(result.Success);
        Assert.Equal(expected, result.ErrorMessage);
        if (culture != "en") Assert.DoesNotContain("Reset token is required.", result.ErrorMessage);
    }

    [Fact]
    public async Task The_too_long_code_fills_its_number_from_the_params()
    {
        using var _ = new CultureScope("tr");

        var result = await Gateway(TooLongBody, Localizer()).ResetTenantPasswordAsync("a@b.test", "t", new string('x', 129));

        Assert.Contains("128", result.ErrorMessage);
        Assert.DoesNotContain("{0}", result.ErrorMessage);
        Assert.DoesNotContain("Password can be at most", result.ErrorMessage);
    }

    private static IStringLocalizer<SharedResource> Localizer()
        => new StringLocalizer<SharedResource>(new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance));

    private static AuthGateway Gateway(string body, IStringLocalizer<SharedResource> localizer)
        => new(new HttpClient(new FixedAnswer(body)) { BaseAddress = new Uri("http://gateway.test") },
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, NullLogger<AuthGateway>.Instance, localizer);

    private sealed class FixedAnswer(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
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
