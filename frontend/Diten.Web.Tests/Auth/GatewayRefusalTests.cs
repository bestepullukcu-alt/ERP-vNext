using System.Net;
using System.Text;
using System.Text.Json;
using Diten.Web;
using Diten.Web.Services.Auth;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.Web.Tests.Auth;

/// <summary>
/// WP-USERS-ERROR-CODES-01, acceptance round 2 (item 13) — <see cref="GatewayRefusal.Read"/>, the one reader of a
/// refused hop: every code is kept, and a refusal that names NO code is always at least one failure — an empty body,
/// an HTML error page or a ProblemDetails without codes must never read as "nothing went wrong".
/// </summary>
public sealed class GatewayRefusalTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html><body>502 Bad Gateway</body></html>")]
    [InlineData("[1,2,3]")]
    [InlineData("\"just a string\"")]
    [InlineData("""{"title":"Server error","status":500,"detail":"An unexpected error occurred.","traceId":"t"}""")]
    [InlineData("""{"title":"Forbidden","status":403}""")]
    [InlineData("""{"data":null,"statusCode":400,"isSuccessful":false,"errors":[],"errorCodes":[]}""")]
    [InlineData("""{"isSuccessful":false,"errorCodes":[{"code":""},{"nope":1},"text"]}""")]
    public void A_refusal_that_names_no_code_is_one_uncoded_failure_never_zero(string? raw)
    {
        var refusal = GatewayRefusal.Read(raw);

        Assert.Empty(refusal.Codes);
        Assert.Equal(1, refusal.FailureCount);
        Assert.True(refusal.HasUncoded);
    }

    [Fact]
    public void An_envelope_with_one_error_and_its_code_is_fully_coded()
    {
        var refusal = GatewayRefusal.Read("""{"isSuccessful":false,"statusCode":409,"errors":["Email is already in use."],"errorCodes":[{"code":"USER_EMAIL_TAKEN"}]}""");

        Assert.Equal(["USER_EMAIL_TAKEN"], refusal.Codes.Select(c => c.Code));
        Assert.Null(refusal.Codes[0].Params);
        Assert.False(refusal.HasUncoded);
    }

    [Fact]
    public void An_envelope_with_more_errors_than_codes_has_an_uncoded_failure()
    {
        var refusal = GatewayRefusal.Read("""{"isSuccessful":false,"errors":["one","two"],"errorCodes":[{"code":"USER_NOT_FOUND"}]}""");

        Assert.Equal(2, refusal.FailureCount);
        Assert.True(refusal.HasUncoded);
    }

    [Fact]
    public void A_validators_body_counts_one_failure_per_Severity_line_and_keeps_every_code_with_its_params()
    {
        const string body = """{"title":"Validation failed","status":400,"detail":"Validation failed: \n -- Email: A valid email address is required. Severity: Error\n -- NewPassword: Password can be at most 128 characters. Severity: Error\n -- Token: Reset token is required. Severity: Error","traceId":"t","errorCodes":[{"code":"password.too_long","params":{"maxLength":"128","ignored":5}},{"code":"password.reset_token_required","params":null}]}""";

        var refusal = GatewayRefusal.Read(body);

        Assert.Equal(["password.too_long", "password.reset_token_required"], refusal.Codes.Select(c => c.Code));
        Assert.Equal("128", Assert.Single(refusal.Codes[0].Params!).Value); // string params only
        Assert.Null(refusal.Codes[1].Params);
        Assert.Equal(3, refusal.FailureCount);
        Assert.True(refusal.HasUncoded); // three failures, two codes
    }

    [Fact]
    public void A_validators_body_whose_every_failure_is_coded_has_nothing_uncoded()
    {
        var refusal = GatewayRefusal.Read("""{"title":"Validation failed","status":400,"detail":"Validation failed: \n -- FirstName: Ad boş bırakılamaz. Severity: Error","traceId":"t","errorCodes":[{"code":"USER_FIRST_NAME_REQUIRED","params":null}]}""");

        Assert.Equal(1, refusal.FailureCount);
        Assert.False(refusal.HasUncoded);
    }

    // ── WP-ROLES-CLOSE-01 FIX4 item 7 — the governance proxies' answer, now in THIS class ─────────────────

    [Fact]
    public async Task A_coded_refusal_hands_over_every_code_and_the_proxys_own_sentence_never_the_service_s()
    {
        var logger = new LevelLogger();
        var answer = Json(await GatewayRefusal.ReadAsync(Refused(HttpStatusCode.Conflict,
            """{"isSuccessful":false,"errors":["Role name is already in use."],"errorCodes":[{"code":"ROLE_NAME_TAKEN"},{"code":"ROLE_NOT_FOUND"}]}"""), new KeyLocalizer(), logger));

        Assert.False(answer.GetProperty("success").GetBoolean());
        Assert.Equal("ROLE_NAME_TAKEN", answer.GetProperty("errorCode").GetString());
        Assert.Equal(["ROLE_NAME_TAKEN", "ROLE_NOT_FOUND"], answer.GetProperty("errorCodes").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal("GatewayError", answer.GetProperty("errors")[0].GetString());
        Assert.True(answer.GetProperty("local").GetBoolean());
        Assert.DoesNotContain("already in use", answer.GetRawText());
        Assert.Contains(logger.Lines, l => l.Contains("Role name is already in use.")); // it goes to the server log
    }

    [Fact]
    public async Task A_code_the_service_names_twice_is_handed_over_once()
    {
        var answer = Json(await GatewayRefusal.ReadAsync(Refused(HttpStatusCode.Conflict,
            """{"errors":["a","b"],"errorCodes":[{"code":"ROLE_NAME_TAKEN"},{"code":"ROLE_NAME_TAKEN"},{"code":"ROLE_NOT_FOUND"}]}"""), new KeyLocalizer(), new LevelLogger()));

        Assert.Equal(["ROLE_NAME_TAKEN", "ROLE_NOT_FOUND"], answer.GetProperty("errorCodes").EnumerateArray().Select(c => c.GetString()));
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, """{"errors":["x"],"errorCodes":[{"code":"ROLE_NAME_TAKEN"}]}""", LogLevel.Information)] // a business refusal
    [InlineData(HttpStatusCode.BadRequest, """{"errors":["raw"]}""", LogLevel.Warning)]                                           // no code
    [InlineData(HttpStatusCode.InternalServerError, """{"errors":["x"],"errorCodes":[{"code":"ROLE_NAME_TAKEN"}]}""", LogLevel.Warning)] // a 5xx
    public async Task A_coded_4xx_is_logged_as_Information_and_anything_else_as_Warning(HttpStatusCode status, string body, LogLevel expected)
    {
        var logger = new LevelLogger();

        await GatewayRefusal.ReadAsync(Refused(status, body), new KeyLocalizer(), logger);

        Assert.Equal([expected], logger.Levels);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Unauthorized")]
    [InlineData(HttpStatusCode.Forbidden, "AccessDenied")]
    [InlineData(HttpStatusCode.BadGateway, "GatewayError")]
    public async Task Without_a_code_the_answer_is_this_applications_sentence_for_the_status(HttpStatusCode status, string key)
    {
        var answer = Json(await GatewayRefusal.ReadAsync(Refused(status, "<html>upstream</html>"), new KeyLocalizer(), new LevelLogger()));

        Assert.Equal(key, answer.GetProperty("errors")[0].GetString());
        Assert.Empty(answer.GetProperty("errorCodes").EnumerateArray());
        Assert.DoesNotContain("upstream", answer.GetRawText());
    }

    [Fact]
    public void A_failed_call_logs_the_exception_and_answers_without_its_text()
    {
        var logger = new LevelLogger();

        var answer = Json(GatewayRefusal.Failure(new HttpRequestException("Connection refused (gateway.internal:5000)"), new KeyLocalizer(), logger, "Roles create"));

        Assert.Equal("GatewayError", answer.GetProperty("errors")[0].GetString());
        Assert.DoesNotContain("gateway.internal", answer.GetRawText());
        Assert.Equal([LogLevel.Error], logger.Levels);
    }

    [Fact]
    public void A_form_refusal_carries_every_broken_rule()
    {
        var answer = Json(GatewayRefusal.Invalid(["ROLE_NAME_REQUIRED", "ROLE_DESCRIPTION_TOO_LONG"], new KeyLocalizer()));

        Assert.Equal(["ROLE_NAME_REQUIRED", "ROLE_DESCRIPTION_TOO_LONG"], answer.GetProperty("errorCodes").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal("ValidationFailed", answer.GetProperty("errors")[0].GetString());
    }

    private static HttpResponseMessage Refused(HttpStatusCode status, string body) => new(status)
    {
        RequestMessage = new HttpRequestMessage(HttpMethod.Post, "http://gateway.test/api/roles"),
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static JsonElement Json(object answer) => JsonDocument.Parse(JsonSerializer.Serialize(answer)).RootElement;

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class LevelLogger : ILogger
    {
        public List<LogLevel> Levels { get; } = [];
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
            Lines.Add(formatter(state, exception));
        }
    }
}
