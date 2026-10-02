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
/// WP-USERS-ERROR-CODES-01 — AuthService's "Validation failed" body now carries the password codes
/// ({ title, status, detail, traceId, errorCodes }). The real <see cref="AuthGateway"/> must say them with the keys
/// SharedResource already has, in the reader's language — no new key, never the English <c>detail</c>.
///
/// <para>Each test makes a call the account controller REALLY makes: it refuses blank fields itself, so what reaches
/// AuthService is a filled-in form whose password is weak (the policy) or too long (the validator). The bodies are
/// the ones AuthService answers — pinned over HTTP by PasswordPolicyDoorEndpointTests and
/// PasswordValidatorCodeEndpointTests.</para>
/// </summary>
public sealed class AuthGatewayValidatorCodeLocalizationTests
{
    private const string WeakBody = """{"title":"Validation failed","status":400,"detail":"Validation failed: \n -- Password: Password must be at least 10 characters. Severity: Error\n -- Password: Password must contain at least one uppercase letter. Severity: Error\n -- Password: Password must contain at least one special character. Severity: Error","traceId":"t","errorCodes":[{"code":"password.too_short","params":{"minLength":"10"}},{"code":"password.needs_uppercase","params":null},{"code":"password.needs_special","params":null}]}""";
    private const string TooLongBody = """{"title":"Validation failed","status":400,"detail":"Validation failed: \n -- NewPassword: Password can be at most 128 characters. Severity: Error","traceId":"t","errorCodes":[{"code":"password.too_long","params":{"maxLength":"128"}}]}""";
    // A coded failure next to an UNCODED one (the e-mail format rule has no code by design).
    private const string MixedBody = """{"title":"Validation failed","status":400,"detail":"Validation failed: \n -- Email: A valid email address is required. Severity: Error\n -- NewPassword: Password can be at most 128 characters. Severity: Error","traceId":"t","errorCodes":[{"code":"password.too_long","params":{"maxLength":"128"}}]}""";

    public static TheoryData<string> Cultures() => ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Platform_reset_with_a_weak_password_reads_as_the_existing_sentences(string culture)
    {
        using var _ = new CultureScope(culture);
        var localizer = Localizer();

        var result = await Gateway(WeakBody, localizer).ResetPlatformPasswordAsync("admin@acme.test", "the-emailed-token", "weak");

        AssertWeak(result, localizer, culture);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task Platform_forced_change_with_a_weak_password_reads_as_the_existing_sentences(string culture)
    {
        using var _ = new CultureScope(culture);
        var localizer = Localizer();

        var result = await Gateway(WeakBody, localizer).ChangePlatformPasswordAsync("Current!Passw0rd", "weak", rememberMe: false);

        AssertWeak(result, localizer, culture);
    }

    [Fact]
    public async Task Tenant_set_password_too_long_fills_its_number_from_the_params()
    {
        using var _ = new CultureScope("tr");
        var localizer = Localizer();

        var result = await Gateway(TooLongBody, localizer).ResetTenantPasswordAsync("a@b.test", "the-emailed-token", new string('x', 129));

        Assert.False(result.Success);
        Assert.Equal(string.Format(Sentence(localizer, "Password.Error.TooLong", "tr"), "128"), result.ErrorMessage);
    }

    // Item 9 — a failure WITHOUT a code beside a coded one is not dropped: the general sentence is added.
    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task A_coded_and_an_uncoded_failure_together_read_as_the_sentence_plus_the_general_one(string culture)
    {
        using var _ = new CultureScope(culture);
        var localizer = Localizer();

        var result = await Gateway(MixedBody, localizer).ResetTenantPasswordAsync("not-an-address", "the-emailed-token", new string('x', 129));

        var expected = string.Format(Sentence(localizer, "Password.Error.TooLong", culture), "128") + " " + Sentence(localizer, "ErrorOccurred", culture);
        Assert.Equal(expected, result.ErrorMessage);
        Assert.DoesNotContain("A valid email address is required.", result.ErrorMessage);
    }

    // Item 13 — a code this map does not know, next to one it does, is not dropped either (every failure HAS a code
    // here, so only the map's own gap can add the general sentence).
    [Fact]
    public async Task A_known_code_next_to_an_unknown_one_reads_as_the_sentence_plus_the_general_one()
    {
        using var _ = new CultureScope("tr");
        var localizer = Localizer();
        const string body = """{"title":"Validation failed","status":400,"detail":"Validation failed: \n -- NewPassword: Password can be at most 128 characters. Severity: Error\n -- NewPassword: Something new. Severity: Error","traceId":"t","errorCodes":[{"code":"password.too_long","params":{"maxLength":"128"}},{"code":"password.a_rule_this_map_does_not_know","params":null}]}""";

        var result = await Gateway(body, localizer).ResetTenantPasswordAsync("a@b.test", "the-emailed-token", new string('x', 129));

        Assert.Equal(string.Format(Sentence(localizer, "Password.Error.TooLong", "tr"), "128") + " " + Sentence(localizer, "ErrorOccurred", "tr"), result.ErrorMessage);
    }

    [Fact]
    public async Task A_fully_coded_refusal_gets_no_general_sentence()
    {
        using var _ = new CultureScope("en");
        var localizer = Localizer();

        var result = await Gateway(TooLongBody, localizer).ResetTenantPasswordAsync("a@b.test", "t", new string('x', 129));

        Assert.DoesNotContain(Sentence(localizer, "ErrorOccurred", "en"), result.ErrorMessage);
    }

    private static void AssertWeak(AuthBridgeResult result, IStringLocalizer<SharedResource> localizer, string culture)
    {
        var expected = string.Join(" ",
            string.Format(Sentence(localizer, "Password.Error.TooShort", culture), "10"),
            Sentence(localizer, "Password.Error.NeedsUppercase", culture),
            Sentence(localizer, "Password.Error.NeedsSpecial", culture));

        Assert.False(result.Success);
        Assert.Equal(expected, result.ErrorMessage);
        Assert.Contains("10", result.ErrorMessage);
        Assert.DoesNotContain("{0}", result.ErrorMessage);
        if (culture != "en") Assert.DoesNotContain("Password must be at least", result.ErrorMessage);
    }

    private static string Sentence(IStringLocalizer<SharedResource> localizer, string key, string culture)
    {
        var text = localizer[key];
        Assert.False(text.ResourceNotFound, $"SharedResource.{culture} has no {key} — a new key would be needed.");
        return text.Value;
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
