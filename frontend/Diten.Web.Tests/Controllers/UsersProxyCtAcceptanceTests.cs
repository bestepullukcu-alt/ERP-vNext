using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web;
using Diten.Web.Controllers;
using Diten.Web.Models.Governance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-USERS-ERROR-CODES-01 — CONTROL TOWER acceptance of fix round 2. Two rules of the Users proxy that no test held
/// (the acceptance sabotage stayed green on both):
/// <list type="bullet">
/// <item>WITHOUT a session no door of the screen reads as success — the form, the edit and every kebab action answer
/// with the proxy's own "not signed in" sentence, and the gateway is never asked.</item>
/// <item>The service's text of a refused action is not thrown away: it never reaches the browser, and it DOES reach
/// the server log (the only place somebody can read why the service refused).</item>
/// </list>
/// Driven on the real <see cref="UsersController"/>.
/// </summary>
public sealed class UsersProxyCtAcceptanceTests
{
    private static readonly Guid UserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    public static TheoryData<string> Doors() => ["create", "edit", "disable", "enable", "resend-invite", "reset-password"];

    private static Task<IActionResult> Call(UsersController controller, string door) => door switch
    {
        "create" => controller.Create(new UserEditViewModel { Email = "a@b.test", FirstName = "A", LastName = "B" }),
        "edit" => controller.Edit(UserId, new UserEditViewModel { Email = "a@b.test", FirstName = "A", LastName = "B", IsActive = true }),
        "disable" => controller.Disable(UserId),
        "enable" => controller.Enable(UserId),
        "resend-invite" => controller.ResendInvite(UserId),
        _ => controller.ResetPassword(UserId)
    };

    [Theory]
    [MemberData(nameof(Doors))]
    public async Task Without_a_session_no_door_reads_as_success_and_the_gateway_is_never_asked(string door)
    {
        var gateway = new Gateway(HttpStatusCode.OK, """{"isSuccessful":true,"data":{}}""");
        var (controller, _) = Controller(gateway, signedIn: false);

        var json = Body(await Call(controller, door));

        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal("«Unauthorized»", json.GetProperty("ownMessages")[0].GetString()); // the proxy's own localized sentence
        Assert.Equal(0, json.GetProperty("errorCodes").GetArrayLength());
        Assert.Equal(0, gateway.Calls);
    }

    [Theory]
    [MemberData(nameof(Doors))]
    public async Task With_a_session_the_same_door_asks_the_gateway(string door)
    {
        // The control: the refusal above is the missing session, not a door that never calls.
        var gateway = new Gateway(HttpStatusCode.OK, """{"isSuccessful":true,"data":{}}""");
        var (controller, _) = Controller(gateway, signedIn: true);

        await Call(controller, door);

        Assert.Equal(1, gateway.Calls);
    }

    [Theory]
    [MemberData(nameof(Doors))]
    public async Task The_services_text_of_a_refusal_goes_to_the_server_log_and_only_there(string door)
    {
        const string serviceText = "Seat limit reached for plan STARTER-5.";
        var body = $$$"""{"isSuccessful":false,"statusCode":409,"errors":["{{{serviceText}}}"],"errorCodes":[{"code":"USER_QUOTA_EXCEEDED","params":{"max":"5","current":"5"}}]}""";
        var (controller, logs) = Controller(new Gateway(HttpStatusCode.Conflict, body), signedIn: true);

        var json = Body(await Call(controller, door));

        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.DoesNotContain(serviceText, json.GetRawText());
        Assert.Contains(logs.Lines, line => line.Contains(serviceText, StringComparison.Ordinal));
    }

    private static JsonElement Body(IActionResult result)
        => JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value)).RootElement;

    private static (UsersController Controller, LogLines Logs) Controller(Gateway gateway, bool signedIn)
    {
        var logs = new LogLines();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build();
        var controller = new UsersController(new HttpClient(gateway), new NoFactory(), configuration, new MarkedLocalizer(), logs);

        // No session = no tenant claim and no token cookie: exactly what AddAuthHeaders refuses.
        var http = new DefaultHttpContext
        {
            User = signedIn
                ? new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenantId", TenantId.ToString())], "test"))
                : new ClaimsPrincipal(new ClaimsIdentity())
        };
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["Email"] = "a@b.test",
            ["FirstName"] = "A",
            ["LastName"] = "B",
            ["IsActive"] = "true"
        });
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return (controller, logs);
    }

    private sealed class Gateway(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class NoFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("these doors use the injected client");
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
