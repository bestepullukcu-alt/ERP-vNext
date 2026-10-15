using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web;
using Diten.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-USERS-ERROR-CODES-01, acceptance round 2 (item 8) — the three same-origin API proxies of the Users controller
/// (<c>/Users/api/lookup</c>, <c>…/account-assertion</c>, <c>…/account-kind</c>). A SUCCESS still passes through
/// verbatim; a REFUSAL keeps its upstream HTTP status but no longer its body: the codes and this proxy's own localized
/// sentence travel, the service's text (on a 5xx, an exception message) goes to the server log — and the log names the
/// path, not the query string, because the lookup's query is what the reader typed. Driven on the real controller.
/// </summary>
public sealed class UsersApiProxyRefusalTests
{
    private static readonly Guid UserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private const string Leak = "MongoConnectionException: auth-db.internal:27017 timed out";

    public static TheoryData<string> Doors() => ["lookup", "assertion", "kind"];

    private static Task<IActionResult> Call(UsersController controller, string door) => door switch
    {
        "lookup" => controller.ApiLookup("jane doe", 20),
        "assertion" => controller.ApiAccountAssertion(UserId),
        _ => controller.ApiSetAccountKind(UserId)
    };

    [Theory]
    [MemberData(nameof(Doors))]
    public async Task A_success_passes_through_verbatim(string door)
    {
        const string body = """{"data":[{"id":"1","displayName":"Jane"}],"statusCode":200,"isSuccessful":true}""";
        var (controller, _) = Controller(new Answer(HttpStatusCode.OK, body));

        var result = Assert.IsType<ContentResult>(await Call(controller, door));

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(body, result.Content);
    }

    [Theory]
    [MemberData(nameof(Doors))]
    public async Task A_5xx_keeps_its_status_and_none_of_the_services_text(string door)
    {
        var (controller, _) = Controller(new Answer(HttpStatusCode.InternalServerError, $$"""{"title":"Server error","status":500,"detail":"{{Leak}}","traceId":"t"}"""));

        var (status, json) = Refused(await Call(controller, door));

        Assert.Equal(500, status);
        Assert.DoesNotContain("auth-db.internal", json.GetRawText());
        Assert.DoesNotContain("MongoConnectionException", json.GetRawText());
        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.True(json.GetProperty("uncoded").GetBoolean());
        Assert.Equal(500, json.GetProperty("status").GetInt32());
        Assert.False(json.TryGetProperty("errors", out _));
    }

    [Theory]
    [MemberData(nameof(Doors))]
    public async Task A_coded_refusal_keeps_its_status_and_hands_the_code_over(string door)
    {
        var (controller, _) = Controller(new Answer(HttpStatusCode.NotFound, """{"data":null,"statusCode":404,"isSuccessful":false,"errors":["User not found."],"errorCodes":[{"code":"USER_NOT_FOUND"}]}"""));

        var (status, json) = Refused(await Call(controller, door));

        Assert.Equal(404, status);
        Assert.Equal("USER_NOT_FOUND", json.GetProperty("errorCodes")[0].GetProperty("code").GetString());
        Assert.DoesNotContain("User not found.", json.GetRawText());
    }

    [Theory]
    [MemberData(nameof(Doors))]
    public async Task Without_a_session_the_answer_is_401_with_the_proxys_own_sentence_not_a_fixed_English_one(string door)
    {
        var (controller, _) = Controller(new Answer(HttpStatusCode.OK, "{}"), signedIn: false);

        var (status, json) = Refused(await Call(controller, door));

        Assert.Equal(401, status);
        Assert.Equal("«Unauthorized»", json.GetProperty("ownMessages")[0].GetString()); // the localizer's value, not a literal
        Assert.DoesNotContain("\"message\"", json.GetRawText());
    }

    [Theory]
    [MemberData(nameof(Doors))]
    public async Task An_unreachable_gateway_is_503_with_the_proxys_own_sentence_and_no_exception_text(string door)
    {
        var (controller, logs) = Controller(new Answer(new HttpRequestException("Connection refused (auth.internal:5056)")));

        var (status, json) = Refused(await Call(controller, door));

        Assert.Equal(503, status);
        Assert.Equal("«GatewayError»", json.GetProperty("ownMessages")[0].GetString());
        Assert.DoesNotContain("auth.internal", json.GetRawText());
        Assert.DoesNotContain("Users dependency unavailable", json.GetRawText());
        Assert.NotEmpty(logs.Lines);
    }

    [Fact]
    public async Task The_log_of_a_failed_lookup_names_the_path_and_not_what_the_reader_searched_for()
    {
        var (controller, logs) = Controller(new Answer(new HttpRequestException("down")));

        await controller.ApiLookup("jane doe", 20);

        var line = Assert.Single(logs.Lines);
        Assert.Contains("/api/users/lookup", line);
        Assert.DoesNotContain("jane", line);
        Assert.DoesNotContain("search=", line);
    }

    // ── harness ─────────────────────────────────────────────────────────────────────────────────────────

    private static (int Status, JsonElement Json) Refused(IActionResult result)
    {
        var json = Assert.IsType<JsonResult>(result);
        return (json.StatusCode ?? 200, JsonDocument.Parse(JsonSerializer.Serialize(json.Value)).RootElement);
    }

    private static (UsersController Controller, LogLines Logs) Controller(Answer answer, bool signedIn = true)
    {
        var logs = new LogLines();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build();
        var controller = new UsersController(new HttpClient(answer), new Factory(answer), configuration, new MarkedLocalizer(), logs);

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenantId", TenantId.ToString())], "test"))
        };
        if (signedIn)
        {
            http.Request.Headers.Cookie = "access_token=a-test-token";
        }

        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{"kind":"Human"}"""));
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (controller, logs);
    }

    private sealed class Answer : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body = string.Empty;
        private readonly Exception? _throws;

        public Answer(HttpStatusCode status, string body) => (_status, _body) = (status, body);
        public Answer(Exception throws) => _throws = throws;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _throws is not null
                ? throw _throws
                : Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") });
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class MarkedLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, $"«{name}»");
        public LocalizedString this[string name, params object[] arguments] => new(name, $"«{name}»");
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class LogLines : ILogger<UsersController>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add(formatter(state, exception));
    }
}
