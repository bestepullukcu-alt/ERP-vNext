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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-AUTH-INVITED-LIFECYCLE-01 — AuthService tags the lifecycle refusals with stable codes (USER_EMAIL_TAKEN,
/// USER_INVITATION_PENDING); the Users proxy must hand the code to the screen on every door the screen uses —
/// create, edit and the kebab actions — so index.js can say it in the reader's language. Driven on the REAL
/// <see cref="UsersController"/> with a gateway handler answering exactly what AuthService answers.
/// </summary>
public sealed class UsersLifecycleErrorCodeProxyTests
{
    private const string Gateway = "http://gateway.test";
    private static readonly Guid UserId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static string Refusal(string code, string message) =>
        $$$"""{"isSuccessful":false,"statusCode":409,"errors":["{{{message}}}"],"errorCodes":[{"code":"{{{code}}}"}]}""";

    [Fact]
    public async Task Create_hands_over_USER_EMAIL_TAKEN()
    {
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.Conflict, Refusal("USER_EMAIL_TAKEN", "Email is already in use.")), Form());

        var json = Body(await controller.Create(new UserEditViewModel { Email = "a@b.test", FirstName = "A", LastName = "B" }));

        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal("USER_EMAIL_TAKEN", json.GetProperty("errorCode").GetString());
        Assert.Equal("Email is already in use.", json.GetProperty("errors")[0].GetString()); // English fallback kept
    }

    [Fact]
    public async Task Enable_hands_over_USER_INVITATION_PENDING()
    {
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.Conflict, Refusal("USER_INVITATION_PENDING", "not accepted")), Form());

        var json = Body(await controller.Enable(UserId));

        Assert.Equal("USER_INVITATION_PENDING", json.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Edit_hands_over_USER_INVITATION_PENDING()
    {
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.Conflict, Refusal("USER_INVITATION_PENDING", "not accepted")), Form());

        var json = Body(await controller.Edit(UserId, new UserEditViewModel { Email = "a@b.test", FirstName = "A", LastName = "B", IsActive = true }));

        Assert.Equal("USER_INVITATION_PENDING", json.GetProperty("errorCode").GetString());
    }

    // BL-459 — the plan's user limit: the code AND its numbers reach the screen (index.js adds "In use: 5 of 5").
    [Fact]
    public async Task Create_hands_over_USER_QUOTA_EXCEEDED_with_its_numbers()
    {
        const string body = """{"isSuccessful":false,"statusCode":409,"errors":["limit"],"errorCodes":[{"code":"USER_QUOTA_EXCEEDED","params":{"max":"5","current":"5"}}]}""";
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.Conflict, body), Form());

        var json = Body(await controller.Create(new UserEditViewModel { Email = "a@b.test", FirstName = "A", LastName = "B" }));

        Assert.Equal("USER_QUOTA_EXCEEDED", json.GetProperty("errorCode").GetString());
        Assert.Equal("5", json.GetProperty("errorParams").GetProperty("max").GetString());
        Assert.Equal("5", json.GetProperty("errorParams").GetProperty("current").GetString());
    }

    [Fact]
    public async Task A_code_without_params_carries_errorParams_null()
    {
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.Conflict, Refusal("USER_EMAIL_TAKEN", "taken")), Form());

        var json = Body(await controller.Create(new UserEditViewModel { Email = "a@b.test", FirstName = "A", LastName = "B" }));

        Assert.Equal(JsonValueKind.Null, json.GetProperty("errorParams").ValueKind);
    }

    [Fact]
    public async Task A_refusal_without_a_code_carries_errorCode_null_and_the_gateway_text()
    {
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.BadRequest, """{"isSuccessful":false,"errors":["Something else."]}"""), Form());

        var json = Body(await controller.Disable(UserId));

        Assert.Equal(JsonValueKind.Null, json.GetProperty("errorCode").ValueKind);
        Assert.Equal("Something else.", json.GetProperty("errors")[0].GetString());
    }

    // WP-USERS-ERROR-CODES-01 — the proxy says whether `errors` is a service's text it only relayed (never shown by
    // index.js) or its own localized sentence (shown as it is).
    [Fact]
    public async Task A_relayed_service_sentence_is_marked_rawText()
    {
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.Conflict, """{"isSuccessful":false,"errors":["User has already completed setup."]}"""), Form());

        var json = Body(await controller.ResendInvite(UserId));

        Assert.True(json.GetProperty("rawText").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("errorCode").ValueKind);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"title":"Unauthorized"}""")]
    [InlineData(HttpStatusCode.BadGateway, "")]
    public async Task The_proxys_own_localized_sentence_is_not_marked_rawText(HttpStatusCode status, string body)
    {
        var controller = ControllerWith(new CapturingGateway(status, body), Form());

        var json = Body(await controller.Disable(UserId));

        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.False(json.GetProperty("rawText").GetBoolean());
    }

    [Fact]
    public async Task Every_new_code_is_handed_over_untouched_on_the_kebab_door()
    {
        foreach (var code in new[] { "USER_NOT_FOUND", "USER_DEACTIVATE_SELF", "USER_SETUP_ALREADY_COMPLETED", "USER_PASSWORD_SETUP_PENDING" })
        {
            var controller = ControllerWith(new CapturingGateway(HttpStatusCode.Conflict, Refusal(code, "english")), Form());

            Assert.Equal(code, Body(await controller.ResetPassword(UserId)).GetProperty("errorCode").GetString());
        }
    }

    // A VALIDATOR's refusal is not an envelope: { title, status, detail, traceId, errorCodes }. Same root errorCodes.
    [Fact]
    public async Task A_validators_code_is_handed_over_from_the_validation_failed_body()
    {
        const string body = """{"title":"Validation failed","status":400,"detail":"Validation failed: \n -- FirstName: Ad boş bırakılamaz. Severity: Error","traceId":"t","errorCodes":[{"code":"USER_FIRST_NAME_REQUIRED","params":null}]}""";
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.BadRequest, body), Form());

        var json = Body(await controller.Edit(UserId, new UserEditViewModel { Email = "a@b.test", FirstName = "A", LastName = "B", IsActive = true }));

        Assert.Equal("USER_FIRST_NAME_REQUIRED", json.GetProperty("errorCode").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("errorParams").ValueKind);
        Assert.True(json.GetProperty("rawText").GetBoolean()); // the server's sentence is relayed text, never shown
    }

    private static JsonElement Body(IActionResult result)
        => JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value)).RootElement;

    private static FormCollection Form(params (string Key, string Value)[] fields)
    {
        var values = new Dictionary<string, StringValues>
        {
            ["Email"] = "ali@acme.test",
            ["FirstName"] = "Ali",
            ["LastName"] = "Veli",
            ["IsActive"] = "true"
        };
        foreach (var (key, value) in fields)
        {
            values[key] = value;
        }

        return new FormCollection(values);
    }

    private static UsersController ControllerWith(CapturingGateway gateway, FormCollection form)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();
        var controller = new UsersController(
            new HttpClient(gateway),
            new NoFactory(),
            configuration,
            new KeyLocalizer(),
            NullLogger<UsersController>.Instance);

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenantId", TenantId.ToString())], "test"))
        };
        http.Request.ContentType = "application/x-www-form-urlencoded";
        http.Request.Form = form;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private sealed class CapturingGateway(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Uri, JsonDocument Json)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri!.ToString(), JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body)));
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class NoFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("the edit/get proxies use the injected client");
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
