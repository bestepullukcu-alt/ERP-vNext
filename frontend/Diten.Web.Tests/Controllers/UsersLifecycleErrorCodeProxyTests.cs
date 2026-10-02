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
        Assert.Equal(0, json.GetProperty("ownMessages").GetArrayLength()); // the service's English sentence stays in the server log
        Assert.DoesNotContain("Email is already in use.", json.GetRawText());
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
    public async Task A_refusal_without_a_code_says_so_and_carries_no_service_text()
    {
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.BadRequest, """{"isSuccessful":false,"errors":["Something else."]}"""), Form());

        var json = Body(await controller.Disable(UserId));

        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("errorCode").ValueKind);
        Assert.Equal(0, json.GetProperty("errorCodes").GetArrayLength());
        Assert.True(json.GetProperty("uncoded").GetBoolean());
        Assert.Equal(400, json.GetProperty("status").GetInt32());
        Assert.Equal(0, json.GetProperty("ownMessages").GetArrayLength());
        Assert.DoesNotContain("Something else.", json.GetRawText());
    }

    // ── WP-USERS-ERROR-CODES-01, acceptance round: nothing the proxy did not write reaches the browser ──────────

    [Theory]
    [InlineData("""{"isSuccessful":false,"statusCode":409,"errors":["User has already completed setup."],"errorCodes":[{"code":"USER_SETUP_ALREADY_COMPLETED"}]}""", "User has already completed setup.")]
    [InlineData("""{"title":"Validation failed","status":400,"detail":"Validation failed: \n -- FirstName: Ad boş bırakılamaz. Severity: Error","traceId":"t","errorCodes":[{"code":"USER_FIRST_NAME_REQUIRED","params":null}]}""", "Ad boş bırakılamaz.")]
    [InlineData("""<html><body>upstream proxy error at 10.0.0.5:5056</body></html>""", "10.0.0.5")]
    public async Task The_services_own_text_never_enters_the_response(string upstreamBody, string mustNotAppear)
    {
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.BadRequest, upstreamBody), Form());

        var raw = Body(await controller.ResendInvite(UserId)).GetRawText();

        Assert.DoesNotContain(mustNotAppear, raw);
        Assert.DoesNotContain("rawText", raw); // the flag is gone: there is no relayed text left to flag
    }

    [Fact]
    public async Task An_exceptions_message_never_enters_the_response()
    {
        var controller = ControllerWith(new ThrowingGateway("Connection refused (auth.internal:5056)"), Form());

        foreach (var result in new[]
                 {
                     await controller.Disable(UserId),
                     await controller.Create(new UserEditViewModel { Email = "a@b.test", FirstName = "A", LastName = "B" }),
                     await controller.Edit(UserId, new UserEditViewModel { Email = "a@b.test", FirstName = "A", LastName = "B", IsActive = true })
                 })
        {
            var json = Body(result);
            Assert.False(json.GetProperty("success").GetBoolean());
            Assert.DoesNotContain("auth.internal", json.GetRawText());
            Assert.DoesNotContain("Connection refused", json.GetRawText());
            Assert.Equal("GatewayError", json.GetProperty("ownMessages")[0].GetString()); // the proxy's OWN localized sentence (key localizer)
        }
    }

    [Fact]
    public async Task A_401_is_the_proxys_own_sentence_and_not_an_uncoded_gap()
    {
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.Unauthorized, """{"title":"Unauthorized"}"""), Form());

        var json = Body(await controller.Disable(UserId));

        Assert.Equal("Unauthorized", json.GetProperty("ownMessages")[0].GetString());
        Assert.False(json.GetProperty("uncoded").GetBoolean());
        Assert.Equal(401, json.GetProperty("status").GetInt32());
    }

    // Item 9 — EVERY code travels (not only the first), and a failure without a code is announced, never dropped.
    [Fact]
    public async Task All_codes_of_a_refusal_are_handed_over_and_an_uncoded_failure_is_announced()
    {
        const string body = """{"title":"Validation failed","status":400,"detail":"Validation failed: \n -- FirstName: Ad boş bırakılamaz. Severity: Error\n -- LastName: Soyad boş bırakılamaz. Severity: Error\n -- Password: Şifre en fazla 128 karakter olabilir. Severity: Error","traceId":"t","errorCodes":[{"code":"USER_FIRST_NAME_REQUIRED","params":null},{"code":"USER_LAST_NAME_REQUIRED","params":null}]}""";
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.BadRequest, body), Form());

        var json = Body(await controller.Edit(UserId, new UserEditViewModel { Email = "a@b.test", FirstName = "A", LastName = "B", IsActive = true }));

        Assert.Equal(["USER_FIRST_NAME_REQUIRED", "USER_LAST_NAME_REQUIRED"], json.GetProperty("errorCodes").EnumerateArray().Select(c => c.GetProperty("code").GetString()));
        Assert.Equal("USER_FIRST_NAME_REQUIRED", json.GetProperty("errorCode").GetString()); // the older single-code shape: the first
        Assert.True(json.GetProperty("uncoded").GetBoolean()); // three failures, two codes
    }

    [Fact]
    public async Task A_fully_coded_refusal_is_not_announced_as_uncoded()
    {
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.Conflict, Refusal("USER_NOT_FOUND", "User not found.")), Form());

        Assert.False(Body(await controller.ResetPassword(UserId)).GetProperty("uncoded").GetBoolean());
    }

    // Item 2 — the form's own check answers in CODES (the screen has their sentences in seven languages), never in
    // MVC's English DataAnnotations text. Driven on the real controller with what a reader can actually type.
    [Theory]
    [InlineData("a@b.test", "   ", "Veli", new[] { "USER_FIRST_NAME_REQUIRED" })]
    [InlineData("a@b.test", "Ali", "", new[] { "USER_LAST_NAME_REQUIRED" })]
    [InlineData("", "Ali", "Veli", new[] { "USER_EMAIL_REQUIRED" })]
    [InlineData("not-an-address", "Ali", "Veli", new[] { "USER_EMAIL_INVALID" })]
    [InlineData("  ", " ", null, new[] { "USER_EMAIL_REQUIRED", "USER_FIRST_NAME_REQUIRED", "USER_LAST_NAME_REQUIRED" })]
    public async Task The_forms_own_refusal_is_codes_not_English(string? email, string? firstName, string? lastName, string[] codes)
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, "{}");
        var controller = ControllerWith(gateway, Form());

        var json = Body(await controller.Create(new UserEditViewModel { Email = email, FirstName = firstName, LastName = lastName }));

        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal(codes, json.GetProperty("errorCodes").EnumerateArray().Select(c => c.GetProperty("code").GetString()));
        Assert.Equal(0, json.GetProperty("ownMessages").GetArrayLength());
        Assert.False(json.GetProperty("uncoded").GetBoolean());
        Assert.DoesNotContain("field is required", json.GetRawText());
        Assert.Empty(gateway.Requests); // refused before the gateway is asked
    }

    [Fact]
    public async Task A_name_over_the_limit_is_refused_by_the_form_with_the_too_long_codes()
    {
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.OK, "{}"), Form());
        var tooLong = new string('a', UserEditViewModel.NameMaxLength + 1);

        var json = Body(await controller.Edit(UserId, new UserEditViewModel { Email = "a@b.test", FirstName = tooLong, LastName = tooLong, IsActive = true }));

        Assert.Equal(["USER_FIRST_NAME_TOO_LONG", "USER_LAST_NAME_TOO_LONG"], json.GetProperty("errorCodes").EnumerateArray().Select(c => c.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task A_binding_failure_the_form_check_does_not_know_is_uncoded_never_English()
    {
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.OK, "{}"), Form());
        controller.ModelState.AddModelError("IsActive", "The value 'maybe' is not valid for IsActive.");

        var json = Body(await controller.Create(new UserEditViewModel { Email = "a@b.test", FirstName = "A", LastName = "B" }));

        Assert.True(json.GetProperty("uncoded").GetBoolean());
        Assert.DoesNotContain("is not valid", json.GetRawText());
    }

    [Fact]
    public void Every_code_the_form_can_answer_with_is_one_the_screen_knows()
    {
        var js = File.ReadAllText(RepoPath("frontend", "Diten.Web", "wwwroot", "assets", "js", "Governance", "Users", "index.js"));
        var tooLong = new string('a', UserEditViewModel.NameMaxLength + 1);
        var codes = UsersController.FormRefusalCodes(new UserEditViewModel { Email = "", FirstName = "", LastName = "" }, isCreate: true)
            .Concat(UsersController.FormRefusalCodes(new UserEditViewModel { Email = new string('x', UserEditViewModel.EmailMaxLength + 1), FirstName = tooLong, LastName = tooLong }, isCreate: true))
            .ToList();

        Assert.Equal(7, codes.Distinct().Count());
        Assert.All(codes, code => Assert.Matches($@"\b{code}: '[A-Za-z]+'", js));
    }

    // The limits are the validators': AuthService keeps them in UserFieldLimits, the validators read them there
    // (UserErrorCodeBridgeGuardTests), and the form's maxlength and this proxy's check read the Web constants —
    // which must be the same numbers.
    [Fact]
    public void The_name_limit_of_the_form_is_the_validators()
    {
        Assert.Equal(AuthLimit("NameMaxLength"), UserEditViewModel.NameMaxLength);

        var form = File.ReadAllText(RepoPath("frontend", "Diten.Web", "Views", "Governance", "Users", "_CreateEditOffcanvas.cshtml"));
        foreach (var field in new[] { "FirstName", "LastName" })
        {
            Assert.Matches($@"name=""{field}""[^>]*maxlength=""@Diten\.Web\.Models\.Governance\.UserEditViewModel\.NameMaxLength""", form);
        }
    }

    [Fact]
    public void The_email_limit_of_the_form_is_the_validators()
    {
        Assert.Equal(AuthLimit("EmailMaxLength"), UserEditViewModel.EmailMaxLength);

        var form = File.ReadAllText(RepoPath("frontend", "Diten.Web", "Views", "Governance", "Users", "_CreateEditOffcanvas.cshtml"));
        Assert.Matches(@"name=""Email""[^>]*maxlength=""@Diten\.Web\.Models\.Governance\.UserEditViewModel\.EmailMaxLength""", form);
    }

    private static int AuthLimit(string name)
    {
        var source = File.ReadAllText(RepoPath("services", "Diten.AuthService", "src", "Diten.AuthService.Application", "Features", "Users", "Services", "UserFieldLimits.cs"));
        var match = System.Text.RegularExpressions.Regex.Match(source, $@"public const int {name} = (\d+);");
        Assert.True(match.Success, $"UserFieldLimits.{name} was not found in AuthService.");
        return int.Parse(match.Groups[1].Value);
    }

    // ── acceptance round 2 ──────────────────────────────────────────────────────────────────────────────

    // Item 7 — the limit itself: exactly at the limit goes to the gateway, one more character is refused here.
    [Fact]
    public async Task A_name_of_exactly_the_limit_is_sent_on_and_one_more_character_is_refused()
    {
        var atLimit = new string('n', UserEditViewModel.NameMaxLength);
        var gateway = new CapturingGateway(HttpStatusCode.OK, "{}");

        var sent = Body(await ControllerWith(gateway, Form()).Edit(UserId, new UserEditViewModel { Email = "a@b.test", FirstName = atLimit, LastName = atLimit, IsActive = true }));
        Assert.True(sent.GetProperty("success").GetBoolean());
        Assert.Single(gateway.Requests);

        var refused = Body(await ControllerWith(new CapturingGateway(HttpStatusCode.OK, "{}"), Form()).Edit(UserId, new UserEditViewModel { Email = "a@b.test", FirstName = atLimit + "n", LastName = atLimit, IsActive = true }));
        Assert.Equal(["USER_FIRST_NAME_TOO_LONG"], refused.GetProperty("errorCodes").EnumerateArray().Select(c => c.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task An_address_of_exactly_the_limit_is_sent_on_and_one_more_character_is_refused()
    {
        var atLimit = new string('e', UserEditViewModel.EmailMaxLength - "@b.test".Length) + "@b.test";
        var gateway = new CapturingGateway(HttpStatusCode.OK, "{}");

        var sent = Body(await ControllerWith(gateway, Form()).Create(new UserEditViewModel { Email = atLimit, FirstName = "A", LastName = "B" }));
        Assert.True(sent.GetProperty("success").GetBoolean());
        Assert.Single(gateway.Requests);

        var refused = Body(await ControllerWith(new CapturingGateway(HttpStatusCode.OK, "{}"), Form()).Create(new UserEditViewModel { Email = "e" + atLimit, FirstName = "A", LastName = "B" }));
        Assert.Equal(["USER_EMAIL_TOO_LONG"], refused.GetProperty("errorCodes").EnumerateArray().Select(c => c.GetProperty("code").GetString()));
    }

    // Item 3 — a binding failure the check does not know is announced ALSO next to codes, never swallowed by them.
    [Fact]
    public async Task A_binding_failure_next_to_a_coded_one_is_still_announced_as_uncoded()
    {
        var controller = ControllerWith(new CapturingGateway(HttpStatusCode.OK, "{}"), Form());
        controller.ModelState.AddModelError("IsActive", "The value 'maybe' is not valid for IsActive.");

        var json = Body(await controller.Create(new UserEditViewModel { Email = "a@b.test", FirstName = "", LastName = "B" }));

        Assert.Equal(["USER_FIRST_NAME_REQUIRED"], json.GetProperty("errorCodes").EnumerateArray().Select(c => c.GetProperty("code").GetString()));
        Assert.True(json.GetProperty("uncoded").GetBoolean());
        Assert.DoesNotContain("is not valid", json.GetRawText());
    }

    // Item 4 — the e-mail is not judged on edit: an update never sends it, so an older account whose address is not
    // address-shaped (or is missing from the post) can still be renamed.
    [Theory]
    [InlineData("not-an-address")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Edit_does_not_judge_the_email(string? email)
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, "{}");

        var json = Body(await ControllerWith(gateway, Form()).Edit(UserId, new UserEditViewModel { Email = email, FirstName = "New", LastName = "Name", IsActive = true }));

        Assert.True(json.GetProperty("success").GetBoolean());
        var (_, _, sent) = Assert.Single(gateway.Requests);
        Assert.Equal("New", sent.RootElement.GetProperty("firstName").GetString());
        Assert.False(sent.RootElement.TryGetProperty("email", out _));
    }

    // Item 10 — AuthService reads a MISSING isActive as "leave it as it is"; the form's update always says it.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Edit_always_sends_isActive(bool isActive)
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, "{}");

        await ControllerWith(gateway, Form()).Edit(UserId, new UserEditViewModel { Email = "a@b.test", FirstName = "A", LastName = "B", IsActive = isActive });

        var (_, _, sent) = Assert.Single(gateway.Requests);
        Assert.Equal(isActive, sent.RootElement.GetProperty("isActive").GetBoolean());
    }

    // Item 2 — there is no `errors` field at all: the screen reads `ownMessages` and the codes, nothing else.
    [Fact]
    public async Task No_refusal_of_this_proxy_carries_an_errors_field()
    {
        var answers = new List<JsonElement>
        {
            Body(await ControllerWith(new CapturingGateway(HttpStatusCode.Conflict, Refusal("USER_NOT_FOUND", "User not found.")), Form()).Disable(UserId)),
            Body(await ControllerWith(new CapturingGateway(HttpStatusCode.Unauthorized, "{}"), Form()).Disable(UserId)),
            Body(await ControllerWith(new ThrowingGateway("boom"), Form()).Disable(UserId)),
            Body(await ControllerWith(new CapturingGateway(HttpStatusCode.OK, "{}"), Form()).Create(new UserEditViewModel { Email = "", FirstName = "", LastName = "" })),
        };

        Assert.All(answers, json =>
        {
            Assert.False(json.TryGetProperty("errors", out _), json.GetRawText());
            Assert.Equal(JsonValueKind.Array, json.GetProperty("ownMessages").ValueKind);
        });
    }

    private static string RepoPath(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
        {
            dir = dir.Parent;
        }

        return Path.Combine([dir?.FullName ?? throw new DirectoryNotFoundException("repo root"), .. parts]);
    }

    private sealed class ThrowingGateway(string message) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException(message);
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

    private static UsersController ControllerWith(HttpMessageHandler gateway, FormCollection form)
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
