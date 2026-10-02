using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Diten.Web;
using Diten.Web.Controllers;
using Diten.Web.Models.Governance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-ROLES-CLOSE-01 — what the three governance proxies (Roles, Role Permissions, User Roles) hand the browser when an
/// operation is refused. Driven on the REAL controllers with a gateway handler answering exactly what AuthService
/// answers, and with a localizer that reads the REAL <c>SharedResource.{lang}.resx</c> files — a localizer that merely
/// echoes the key would pass on a key that exists in no language.
/// <list type="bullet">
/// <item>the stable codes travel (every one of them);</item>
/// <item>NOTHING from upstream does — not AuthService's English sentence, not an exception's message, not the gateway
/// host; that text goes to the server log;</item>
/// <item>the one sentence in <c>errors</c> is this application's own, in the reader's language.</item>
/// </list>
/// </summary>
public sealed class GovernanceRoleRefusalProxyTests
{
    private const string Gateway = "http://gateway.internal.test:5000";
    private static readonly Guid RoleId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid OtherId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid TenantId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    private static string Refusal(string code, string message) =>
        $$$"""{"isSuccessful":false,"statusCode":409,"errors":["{{{message}}}"],"errorCodes":[{"code":"{{{code}}}"}]}""";

    // ── the code travels ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Roles_create_hands_over_ROLE_NAME_TAKEN_and_not_AuthServices_sentence()
    {
        var logs = new RecordingLogger();
        var controller = Roles(new FixedGateway(HttpStatusCode.Conflict, Refusal("ROLE_NAME_TAKEN", "Role name is already in use.")), logs: logs);

        var json = Body(await controller.Create(new RoleEditViewModel { Name = "qa", DisplayName = "QA" }));

        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal("ROLE_NAME_TAKEN", json.GetProperty("errorCode").GetString());
        Assert.Equal(["ROLE_NAME_TAKEN"], json.GetProperty("errorCodes").EnumerateArray().Select(e => e.GetString()));
        Assert.DoesNotContain("already in use", json.GetRawText()); // upstream text stays on the server …
        Assert.Contains(logs.Lines, line => line.Contains("Role name is already in use.", StringComparison.Ordinal)); // … in its log
    }

    [Fact]
    public async Task Roles_edit_hands_over_ROLE_NOT_FOUND()
    {
        var controller = Roles(new FixedGateway(HttpStatusCode.NotFound, Refusal("ROLE_NOT_FOUND", "Role not found.")));

        var json = Body(await controller.Edit(RoleId, new RoleEditViewModel { Name = "qa", DisplayName = "QA" }));

        Assert.Equal("ROLE_NOT_FOUND", json.GetProperty("errorCode").GetString());
        Assert.DoesNotContain("Role not found.", json.GetRawText());
    }

    [Fact]
    public async Task Role_permission_assign_hands_over_the_code_even_on_a_403()
    {
        var controller = RoleAssignments(new FixedGateway(HttpStatusCode.Forbidden,
            Refusal("ROLE_PERMISSION_NOT_TENANT_ASSIGNABLE", "This permission cannot be assigned to a tenant role.")));

        var json = Body(await controller.Assign(RoleId, OtherId));

        Assert.Equal("ROLE_PERMISSION_NOT_TENANT_ASSIGNABLE", json.GetProperty("errorCode").GetString());
        Assert.DoesNotContain("cannot be assigned", json.GetRawText());
    }

    [Theory]
    [InlineData("ROLE_PERMISSION_ALREADY_GRANTED", "The role already holds this permission.")]
    public async Task Role_permission_assign_hands_over_the_already_granted_code(string code, string upstream)
    {
        var controller = RoleAssignments(new FixedGateway(HttpStatusCode.Conflict, Refusal(code, upstream)));

        var json = Body(await controller.Assign(RoleId, OtherId));

        Assert.Equal(code, json.GetProperty("errorCode").GetString());
        Assert.DoesNotContain(upstream, json.GetRawText());
    }

    [Fact]
    public async Task Role_permission_revoke_hands_over_ROLE_PERMISSION_GRANT_MANAGED()
    {
        var controller = RoleAssignments(new FixedGateway(HttpStatusCode.Conflict,
            Refusal("ROLE_PERMISSION_GRANT_MANAGED", "System/Module grants are provisioning-managed and cannot be manually removed.")));

        var json = Body(await controller.Revoke(RoleId, OtherId));

        Assert.Equal("ROLE_PERMISSION_GRANT_MANAGED", json.GetProperty("errorCode").GetString());
        Assert.DoesNotContain("provisioning-managed", json.GetRawText());
    }

    [Fact]
    public async Task User_role_assign_hands_over_USER_ROLE_USER_NOT_FOUND()
    {
        var controller = UserRoleAssignments(new FixedGateway(HttpStatusCode.NotFound, Refusal("USER_ROLE_USER_NOT_FOUND", "User not found.")));

        var json = Body(await controller.Assign(OtherId, RoleId));

        Assert.Equal("USER_ROLE_USER_NOT_FOUND", json.GetProperty("errorCode").GetString());
        Assert.DoesNotContain("User not found.", json.GetRawText());
    }

    // ── nothing from upstream reaches the browser (CT 3) ────────────────────────────────────────────────

    [Fact]
    public async Task A_refusal_without_a_code_never_hands_over_the_gateway_sentence()
    {
        var logs = new RecordingLogger();
        var controller = Roles(new FixedGateway(HttpStatusCode.BadRequest, """{"isSuccessful":false,"errors":["Some raw English sentence."]}"""), logs: logs);

        var json = Body(await controller.Create(new RoleEditViewModel { Name = "qa", DisplayName = "QA" }));

        Assert.Equal(JsonValueKind.Null, json.GetProperty("errorCode").ValueKind);
        Assert.Empty(json.GetProperty("errorCodes").EnumerateArray());
        Assert.Equal(Resx("en", "GatewayError"), json.GetProperty("errors")[0].GetString());
        Assert.DoesNotContain("raw English", json.GetRawText());
        Assert.Contains(logs.Lines, line => line.Contains("Some raw English sentence.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_body_that_is_not_an_envelope_is_a_gateway_error_not_a_crash()
    {
        var controller = RoleAssignments(new FixedGateway(HttpStatusCode.BadGateway, "<html>502 nginx upstream</html>"));

        var json = Body(await controller.Assign(RoleId, OtherId));

        Assert.Equal(Resx("en", "GatewayError"), json.GetProperty("errors")[0].GetString());
        Assert.DoesNotContain("nginx", json.GetRawText());
    }

    public static TheoryData<string> Doors => new() { "roles-create", "roles-edit", "permission-assign", "permission-revoke", "user-role-assign", "user-role-revoke" };

    // The exception of a failed call names the gateway host and the driver's own words. Neither leaves the server.
    [Theory, MemberData(nameof(Doors))]
    public async Task When_the_call_itself_fails_the_answer_carries_no_exception_text_and_no_host(string door)
    {
        var logs = new RecordingLogger();
        var gateway = new ThrowingGateway(new HttpRequestException($"Connection refused ({Gateway.Replace("http://", string.Empty)})"));

        var json = Body(door switch
        {
            "roles-create" => await Roles(gateway, logs: logs).Create(new RoleEditViewModel { Name = "qa", DisplayName = "QA" }),
            "roles-edit" => await Roles(gateway, logs: logs).Edit(RoleId, new RoleEditViewModel { Name = "qa", DisplayName = "QA" }),
            "permission-assign" => await RoleAssignments(gateway, logs: logs).Assign(RoleId, OtherId),
            "permission-revoke" => await RoleAssignments(gateway, logs: logs).Revoke(RoleId, OtherId),
            "user-role-assign" => await UserRoleAssignments(gateway, logs: logs).Assign(OtherId, RoleId),
            _ => await UserRoleAssignments(gateway, logs: logs).Revoke(OtherId, RoleId)
        });

        var raw = json.GetRawText();
        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Equal(Resx("en", "GatewayError"), json.GetProperty("errors")[0].GetString());
        Assert.DoesNotContain("Connection refused", raw);
        Assert.DoesNotContain("gateway.internal.test", raw);
        Assert.DoesNotContain("5000", raw);
        Assert.Contains(logs.Lines, line => line.Contains("Connection refused", StringComparison.Ordinal)); // the server keeps it
    }

    // ── this application's own sentence, from the real resx, in the reader's language (CT 4e) ────────────

    public static TheoryData<string> AllLanguages => new(Languages);

    [Theory, MemberData(nameof(AllLanguages))]
    public async Task The_proxys_own_sentences_exist_in_the_real_resx_of_every_language_and_are_what_the_browser_gets(string language)
    {
        var signedOut = Body(await UserRoleAssignments(new FixedGateway(HttpStatusCode.Unauthorized, ""), language).Revoke(OtherId, RoleId));
        var denied = Body(await UserRoleAssignments(new FixedGateway(HttpStatusCode.Forbidden, ""), language).Revoke(OtherId, RoleId));
        var broken = Body(await RoleAssignments(new FixedGateway(HttpStatusCode.BadGateway, ""), language).Assign(RoleId, OtherId));
        var invalid = Body(await RoleAssignments(new FixedGateway(HttpStatusCode.OK, "{}"), language).Assign(Guid.Empty, OtherId));

        foreach (var (answer, key) in new[] { (signedOut, "Unauthorized"), (denied, "AccessDenied"), (broken, "GatewayError"), (invalid, "ValidationFailed") })
        {
            var sentence = answer.GetProperty("errors")[0].GetString();
            Assert.True(ResxKeys(language).Contains(key), $"SharedResource.{language}.resx has no key: {key}");
            Assert.Equal(Resx(language, key), sentence);
            // A missing key cannot hide: ResxLocalizer throws instead of echoing it. And another language is not a copy
            // of the English sentence.
            if (language != "en") Assert.NotEqual(Resx("en", key), sentence);
            Assert.False(string.IsNullOrWhiteSpace(sentence));
            Assert.True(answer.GetProperty("local").GetBoolean());
        }
    }

    // ── the form's own rules (CT 2a) ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_form_with_two_mistakes_answers_with_both_codes_and_never_reaches_the_gateway()
    {
        var gateway = new FixedGateway(HttpStatusCode.OK, "{}");
        var controller = Roles(gateway);

        var json = Body(await controller.Create(new RoleEditViewModel { Name = "   ", DisplayName = new string('d', RoleEditViewModel.DisplayNameMaxLength + 1) }));

        Assert.Equal(["ROLE_NAME_REQUIRED", "ROLE_DISPLAY_NAME_TOO_LONG"], json.GetProperty("errorCodes").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("ROLE_NAME_REQUIRED", json.GetProperty("errorCode").GetString());
        Assert.Equal(0, gateway.Calls);
    }

    [Theory]
    [InlineData(null, "QA", "ROLE_NAME_REQUIRED")]
    [InlineData("qa", "   ", "ROLE_DISPLAY_NAME_REQUIRED")] // the case that used to answer with the framework's English sentence
    [InlineData("qa", null, "ROLE_DISPLAY_NAME_REQUIRED")]
    public async Task A_missing_or_blank_field_answers_with_its_code_on_create_and_on_edit(string? name, string? displayName, string code)
    {
        var create = Body(await Roles(new FixedGateway(HttpStatusCode.OK, "{}")).Create(new RoleEditViewModel { Name = name, DisplayName = displayName }));
        var edit = Body(await Roles(new FixedGateway(HttpStatusCode.OK, "{}")).Edit(RoleId, new RoleEditViewModel { Name = name, DisplayName = displayName }));

        foreach (var json in new[] { create, edit })
        {
            Assert.Equal([code], json.GetProperty("errorCodes").EnumerateArray().Select(e => e.GetString()));
            Assert.Equal(Resx("en", "ValidationFailed"), json.GetProperty("errors")[0].GetString());
            Assert.DoesNotContain("field is required", json.GetRawText()); // not the framework's sentence
        }
    }

    [Fact]
    public void The_forms_rules_refuse_exactly_beyond_the_limits()
    {
        var atLimit = new RoleEditViewModel { Name = new string('n', RoleEditViewModel.NameMaxLength), DisplayName = new string('d', RoleEditViewModel.DisplayNameMaxLength) };
        var over = new RoleEditViewModel { Name = new string('n', RoleEditViewModel.NameMaxLength + 1), DisplayName = new string('d', RoleEditViewModel.DisplayNameMaxLength + 1) };

        Assert.Empty(atLimit.ValidationCodes());
        Assert.Equal([RoleEditViewModel.NameTooLongCode, RoleEditViewModel.DisplayNameTooLongCode], over.ValidationCodes());
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static JsonElement Body(IActionResult result)
        => JsonDocument.Parse(JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value)).RootElement;

    private static IConfiguration Configuration()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway }).Build();

    private static RolesController Roles(HttpMessageHandler gateway, string language = "en", RecordingLogger? logs = null)
        => WithTenant(new RolesController(new HttpClient(gateway), Configuration(), new ResxLocalizer(language), (logs ?? new RecordingLogger()).For<RolesController>()));

    private static RoleAssignmentsController RoleAssignments(HttpMessageHandler gateway, string language = "en", RecordingLogger? logs = null)
        => WithTenant(new RoleAssignmentsController(new HttpClient(gateway), Configuration(), new ResxLocalizer(language), (logs ?? new RecordingLogger()).For<RoleAssignmentsController>()));

    private static UserRoleAssignmentsController UserRoleAssignments(HttpMessageHandler gateway, string language = "en", RecordingLogger? logs = null)
        => WithTenant(new UserRoleAssignmentsController(new HttpClient(gateway), Configuration(), new ResxLocalizer(language), (logs ?? new RecordingLogger()).For<UserRoleAssignmentsController>()));

    private static T WithTenant<T>(T controller) where T : Controller
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenantId", TenantId.ToString())], "test"))
            }
        };
        return controller;
    }

    private static string Resx(string language, string key) => ResxValues(language)[key];

    private static HashSet<string> ResxKeys(string language) => ResxValues(language).Keys.ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, string> ResxValues(string language)
        => XDocument.Load(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources", $"SharedResource.{language}.resx")).Root!
            .Elements("data")
            .Where(d => d.Attribute("name") is not null)
            .GroupBy(d => (string)d.Attribute("name")!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources", "SharedResource.en.resx"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repo root (frontend/Diten.Web/Resources/SharedResource.en.resx).");
    }

    /// <summary>Reads the production resx of one language. A key that is not there is NOT found — it is never echoed as a value.</summary>
    private sealed class ResxLocalizer(string language) : IStringLocalizer<SharedResource>
    {
        private readonly Dictionary<string, string> _values = ResxValues(language);

        public LocalizedString this[string name]
            => _values.TryGetValue(name, out var value)
                ? new LocalizedString(name, value)
                : throw new KeyNotFoundException($"SharedResource.{language}.resx has no key '{name}' — the browser would be shown the bare key.");

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, this[name].Value, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => _values.Select(kv => new LocalizedString(kv.Key, kv.Value));
    }

    private sealed class FixedGateway(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                RequestMessage = request,
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ThrowingGateway(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromException<HttpResponseMessage>(exception);
    }

    /// <summary>What the server log received (message + exception text), so "it went to the log" is measured too.</summary>
    private sealed class RecordingLogger
    {
        public List<string> Lines { get; } = [];

        public ILogger<T> For<T>() => new Typed<T>(this);

        private sealed class Typed<T>(RecordingLogger owner) : ILogger<T>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => owner.Lines.Add(formatter(state, exception) + (exception is null ? string.Empty : " | " + exception));
        }
    }
}
