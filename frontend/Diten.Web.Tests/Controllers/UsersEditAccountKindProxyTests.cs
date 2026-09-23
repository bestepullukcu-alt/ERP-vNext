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
/// WP-AUTH-USER-KIND-UPDATE-01 — the edit form's kind reaches AuthService through the Users proxy, and the form can
/// start from the kind AuthService reports.
///
/// <para>Driven on the REAL <see cref="UsersController"/> with a capturing gateway handler: what is asserted is the
/// body the proxy actually PUTs, which is what AuthService's permission check and writer see. Three form shapes
/// matter and each means something different:</para>
/// <list type="bullet">
/// <item>field ABSENT — Razor drew no select (no auth.users.account-kind.manage) ⇒ <c>accountKind: null</c>, the
/// kind is left alone and the caller needs no classification right;</item>
/// <item>field EMPTY — on edit the empty option is "Unknown", a real choice ⇒ <c>"Unknown"</c>; dropping it to null
/// would silently keep a Human/Service kind the admin just took away;</item>
/// <item>field set — forwarded as the enum NAME.</item>
/// </list>
/// </summary>
public sealed class UsersEditAccountKindProxyTests
{
    private const string Gateway = "http://gateway.test";
    private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TenantId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Theory]
    [InlineData("Human", "Human")]
    [InlineData("service", "service")]
    [InlineData("", "Unknown")]
    [InlineData("   ", "Unknown")]
    public async Task Edit_forwards_the_posted_kind_and_an_empty_choice_as_Unknown(string posted, string expected)
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, """{"isSuccessful":true,"data":{}}""");
        var controller = ControllerWith(gateway, Form(("AccountKind", posted)));

        var result = await controller.Edit(UserId, EditModel());

        Assert.True(Success(result));
        var request = Assert.Single(gateway.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal($"{Gateway}/api/users/{UserId}", request.Uri);
        Assert.Equal(expected, request.Json.RootElement.GetProperty("accountKind").GetString());
    }

    [Fact]
    public async Task Edit_without_the_field_sends_no_kind_so_the_kind_is_left_alone()
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, """{"isSuccessful":true,"data":{}}""");
        var controller = ControllerWith(gateway, Form());

        await controller.Edit(UserId, EditModel());

        var body = Assert.Single(gateway.Requests).Json.RootElement;
        Assert.True(
            !body.TryGetProperty("accountKind", out var kind) || kind.ValueKind == JsonValueKind.Null,
            "a form without the kind select (reader lacks the manage key) must not send a kind at all");
        Assert.Equal("Ali", body.GetProperty("firstName").GetString()); // the rest of the edit still travels
    }

    [Fact]
    public async Task Edit_surfaces_AuthServices_refusal_instead_of_a_success()
    {
        var gateway = new CapturingGateway(HttpStatusCode.Forbidden,
            """{"isSuccessful":false,"statusCode":403,"errors":["Setting the account kind requires the auth.users.account-kind.manage permission."],"errorCodes":[{"code":"PERM_DENIED"}]}""");
        var controller = ControllerWith(gateway, Form(("AccountKind", "Human")));

        var result = await controller.Edit(UserId, EditModel());

        Assert.False(Success(result));
    }

    [Fact]
    public async Task GetById_hands_the_edit_form_the_kind_AuthService_reports()
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK,
            $$$"""{"isSuccessful":true,"data":{"id":"{{{UserId}}}","email":"a@b.test","firstName":"A","lastName":"B","isActive":true,"roles":[],"accountKind":"Service"}}""");
        var controller = ControllerWith(gateway, Form());

        var result = await controller.GetById(UserId);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value));
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Service", doc.RootElement.GetProperty("data").GetProperty("accountKind").GetString());
    }

    // ── wiring ──

    private static UserEditViewModel EditModel() => new() { Email = "ali@acme.test", FirstName = "Ali", LastName = "Veli", IsActive = true };

    private static bool Success(IActionResult result)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value));
        return doc.RootElement.GetProperty("success").GetBoolean();
    }

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
