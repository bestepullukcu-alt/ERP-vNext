using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// MOD-0230 WP-0230-DL7A on the real MVC proxy (and, for the unauthorized face, the real web tier with its layout):
/// W1/W3 — every one of the ten actions checks its pvg.case-intake-triage.* key FIRST (page actions: bare 403 that the
/// status-code pages re-execute to the shared /Home/Status/403; JSON actions: 403 with a message body in the reader's
/// language) and the Gateway is not called; W2 — UAS-001: without the read key the page is the shared 403 page and
/// nothing of the screen; W4 — Bearer, tenant and correlation come only from the verified principal (no X-Diten-*
/// header, no unverified cookie token); W5 — the record version travels to the API; W6/W7 — the list component's
/// start/length become the API's pageNumber/pageSize (at most 100) and the API's counts become the component's envelope.
/// </summary>
public sealed class PvgCaseIntakeTriageWebTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Read = "pvg.case-intake-triage.read";
    private const string Create = "pvg.case-intake-triage.create";
    private const string Update = "pvg.case-intake-triage.update";
    private const string Triage = "pvg.case-intake-triage.triage";
    private const string Route = "pvg.case-intake-triage.route";
    private static readonly string[] AllKeys = [Read, Create, Update, Triage, Route];

    private const string TenantId = "7b2c51f0-0000-4000-8000-0000000000b2";
    private const string DraftId = "7b2c51f0-0000-4000-8000-0000000000c1";
    private const string GatewayUrl = "http://gateway.pvg.test";

    private const string ListAnswer =
        "{\"outcome\":\"Succeeded\",\"items\":[{\"intakeDraftId\":\"d1\",\"status\":\"IntakeCreated\",\"version\":3}],\"totalCount\":101,\"filteredCount\":101}";

    private static readonly CaseIntake FormInput = new(
        "Portal", "Spontaneous", "REF-1", "2026-10-20T10:00", "Consumer", null, "SUBJ-1", null, "narrative", "product",
        "Serious", "Routine", "E-1, E-2");

    private sealed record CaseIntake(
        string IntakeChannel, string SourceType, string SourceReference, string ReceivedAtUtc, string ReporterType,
        string? ReporterContactSummary, string PatientSubjectCode, string? EventOnsetDate, string AdverseEventNarrative,
        string SuspectProductText, string Seriousness, string IntakePriority, string EvidenceLinkReferencesRaw);

    // ------------------------------------------------------------------------------------------------ W1

    /// <summary>The ten actions, each with the key it needs (NASIL, AC-2) and how to call it.</summary>
    private static readonly (string Action, string Key, bool IsPage, Func<PharmacovigilanceCaseIntakeTriageController, Task<IActionResult>> Call)[] Actions =
    [
        ("Index", Read, true, c => Task.FromResult(c.Index())),
        ("Details", Read, true, c => Task.FromResult(c.Details(DraftId))),
        ("Create", Create, true, c => Task.FromResult(c.Create())),
        ("Edit", Update, true, c => Task.FromResult(c.Edit(DraftId))),
        ("List", Read, false, c => c.List(CancellationToken.None)),
        ("Detail", Read, false, c => c.Detail(DraftId, CancellationToken.None)),
        ("CreateDraft", Create, false, c => c.CreateDraft(Input(), CancellationToken.None)),
        ("UpdateDraft", Update, false, c => c.UpdateDraft(DraftId, Input(), CancellationToken.None)),
        ("TriageDraft", Triage, false, c => c.TriageDraft(DraftId, new("Triaged", null, null), CancellationToken.None)),
        ("RouteDraft", Route, false, c => c.RouteDraft(DraftId, new("PV_SAFETY_REVIEW"), CancellationToken.None))
    ];

    [Fact]
    public void The_controller_has_exactly_the_ten_actions_this_test_maps_to_a_key()
    {
        var actions = typeof(PharmacovigilanceCaseIntakeTriageController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(Actions.Select(entry => entry.Action).Order(StringComparer.Ordinal), actions);
    }

    [Fact]
    public async Task Every_case_intake_action_checks_its_key_first()
    {
        foreach (var (action, key, isPage, call) in Actions)
        {
            // Every key EXCEPT the action's own: a wrong key never opens the action.
            var wire = new Wire(ListAnswer);
            var denied = await call(Controller(wire, AllKeys.Where(other => other != key).ToArray()));

            if (isPage)
            {
                // A bare 403 (re-executed to the shared status page) — not a view, not a redirect.
                Assert.True(denied is StatusCodeResult { StatusCode: StatusCodes.Status403Forbidden }, $"{action}: {denied.GetType().Name}");
            }
            else
            {
                var content = Assert.IsType<ContentResult>(denied);
                Assert.True(content.StatusCode == StatusCodes.Status403Forbidden, $"{action}: {content.StatusCode}");
                Assert.False(string.IsNullOrWhiteSpace(content.Content), action);
            }

            Assert.True(wire.Requests.Count == 0, $"{action}: the Gateway was called without the key");

            // Non-vacuity: the same action with ONLY its own key is not a 403.
            var allowed = await call(Controller(new Wire(ListAnswer), key));
            Assert.False(allowed is StatusCodeResult { StatusCode: StatusCodes.Status403Forbidden }, $"{action}: refused with its own key");
            Assert.False(allowed is ContentResult { StatusCode: StatusCodes.Status403Forbidden }, $"{action}: refused with its own key");
            if (isPage)
            {
                Assert.IsType<ViewResult>(allowed);
            }
        }
    }

    [Fact]
    public void Without_the_key_a_page_action_neither_redirects_nor_writes_view_data()
    {
        var controller = Controller(new Wire(ListAnswer), Create);

        var index = controller.Index();
        var edit = controller.Edit(DraftId);
        var details = controller.Details(DraftId);

        foreach (var result in new[] { index, edit, details })
        {
            Assert.IsType<StatusCodeResult>(result);
            Assert.False(result is RedirectResult or RedirectToActionResult or LocalRedirectResult);
        }

        Assert.Empty(controller.ViewData);
    }

    // ------------------------------------------------------------------------------------------------ W2

    [Fact]
    public async Task Without_the_read_key_the_case_intake_page_is_the_shared_403_without_its_skeleton()
    {
        var signingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("JwtSettings:Secret", signingKey);
            builder.UseSetting("JwtSettings:Issuer", "pvg-web-test");
            builder.UseSetting("JwtSettings:Audience", "pvg-web-test");
            builder.UseSetting("GatewayUrl", "http://127.0.0.1:9");
            builder.UseSetting("PlatformServiceUrl", "http://127.0.0.1:9");
            builder.ConfigureTestServices(services =>
                services.AddTransient(_ => new HttpClient(new Wire(ListAnswer), disposeHandler: false)));
        });
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

        using var denied = await GetAsync(client, "/Pharmacovigilance/CaseIntakeTriage", Token(signingKey, Create, Update, Triage, Route));
        var deniedHtml = await denied.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Null(denied.Headers.Location);
        Assert.False(string.IsNullOrWhiteSpace(deniedHtml));
        foreach (var marker in ScreenMarkers)
        {
            Assert.DoesNotContain(marker, deniedHtml);
        }

        // Non-vacuity: with the read key the same request is the screen.
        using var allowed = await GetAsync(client, "/Pharmacovigilance/CaseIntakeTriage", Token(signingKey, Read));
        var allowedHtml = await allowed.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        // The list screen draws the table, the filter and the list script (the form is another page's).
        foreach (var marker in ScreenMarkers.Where(marker => marker != "pvg-case-intake-form"))
        {
            Assert.Contains(marker, allowedHtml);
        }

        Assert.Contains("data-dt-data-mode=\"server\"", allowedHtml);
    }

    private static readonly string[] ScreenMarkers =
    [
        "dt-pvg-case-intake-triage", "datatables-pvg-case-intake-triage", "pvgStatusFilter", "inlineFilterHost",
        "pvg-case-intake-form", "CaseIntakeTriage/index.js"
    ];

    // ------------------------------------------------------------------------------------------------ W3

    [Theory]
    [InlineData("List")]
    [InlineData("Detail")]
    [InlineData("CreateDraft")]
    [InlineData("UpdateDraft")]
    [InlineData("TriageDraft")]
    [InlineData("RouteDraft")]
    public async Task Case_intake_json_actions_without_their_key_answer_403_without_a_gateway_call(string action)
    {
        var (_, key, _, call) = Actions.Single(entry => entry.Action == action);
        var wire = new Wire(ListAnswer);

        var result = await call(Controller(wire, AllKeys.Where(other => other != key).ToArray()));

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, content.StatusCode);
        using var body = JsonDocument.Parse(content.Content!);
        Assert.Equal("FORBIDDEN", body.RootElement.GetProperty("reason_code").GetString());
        var message = body.RootElement.GetProperty("errors")[0].GetString();
        // The reader's sentence (the shared AccessDenied text), never the technical one (UAS-001 §5).
        Assert.Equal("AccessDenied", message);
        Assert.DoesNotContain("Permission denied", content.Content!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(wire.Requests);
    }

    // ------------------------------------------------------------------------------------------------ W4

    [Fact]
    public async Task Proxy_sends_bearer_tenant_and_correlation_only_from_the_verified_principal()
    {
        var wire = new Wire(ListAnswer);

        await Controller(wire, Read).List(CancellationToken.None);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("synthetic-pvg-dl7a", request.Headers.Authorization?.Parameter);
        Assert.Equal(TenantId, Assert.Single(request.Headers.GetValues("X-Tenant-Id")));
        Assert.Equal("trace-pvg-dl7a", Assert.Single(request.Headers.GetValues("X-Correlation-Id")));
        Assert.DoesNotContain(request.Headers, header => header.Key.StartsWith("X-Diten-", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(request.Headers, header => header.Key.Contains("Actor", StringComparison.OrdinalIgnoreCase));

        // The write path carries the same, and nothing else about the caller.
        var writeWire = new Wire(ListAnswer);
        await Controller(writeWire, Route).RouteDraft(DraftId, new("PV_SAFETY_REVIEW", 4), CancellationToken.None);
        var write = Assert.Single(writeWire.Requests);
        Assert.Equal(TenantId, Assert.Single(write.Headers.GetValues("X-Tenant-Id")));
        Assert.DoesNotContain(write.Headers, header => header.Key.StartsWith("X-Diten-", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Without_a_verified_tenant_claim_the_call_is_401_even_if_the_cookie_token_names_a_tenant()
    {
        // An UNSIGNED token that names a tenant: nothing in the proxy may read it back (it is not verified).
        var unsigned = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: [new Claim("tenant_id", TenantId), new Claim("sub", "someone")]));
        var wire = new Wire(ListAnswer);

        var list = await ControllerFor(wire, [Read], withTenantClaim: false, cookieToken: unsigned).List(CancellationToken.None);
        var route = await ControllerFor(wire, [Route], withTenantClaim: false, cookieToken: unsigned)
            .RouteDraft(DraftId, new("PV_SAFETY_REVIEW", 4), CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<ContentResult>(list).StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<ContentResult>(route).StatusCode);
        Assert.Empty(wire.Requests);
    }

    // ------------------------------------------------------------------------------------------------ W5

    [Fact]
    public async Task Form_inputs_carry_the_expected_version_to_the_api()
    {
        var wire = new Wire(ListAnswer);

        await Controller(wire, Update).UpdateDraft(DraftId, Input(version: 7), CancellationToken.None);
        await Controller(wire, Triage).TriageDraft(DraftId, new("Triaged", "R1", "why", 7), CancellationToken.None);
        await Controller(wire, Route).RouteDraft(DraftId, new("PV_SAFETY_REVIEW", 7), CancellationToken.None);
        await Controller(wire, Create).CreateDraft(Input(version: 7), CancellationToken.None);

        Assert.Equal(4, wire.Requests.Count);
        for (var i = 0; i < 3; i++)
        {
            Assert.Contains("\"ExpectedVersion\":7", wire.Bodies[i]);
        }

        // A create is a new record: it has no version to send, even if the form posted one.
        Assert.DoesNotContain("ExpectedVersion", wire.Bodies[3]);
        Assert.Equal(HttpMethod.Put, wire.Requests[0].Method);
        Assert.EndsWith($"/api/pv-case-intake-triage/{DraftId}", wire.Requests[0].RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith("/triage", wire.Requests[1].RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith("/route", wire.Requests[2].RequestUri!.AbsolutePath, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------------------------------------ W6

    [Theory]
    [InlineData("{\"outcome\":\"Succeeded\",\"items\":[{\"intakeDraftId\":\"d1\"}],\"totalCount\":101,\"filteredCount\":101}")]
    [InlineData("{\"Outcome\":\"Succeeded\",\"Items\":[{\"IntakeDraftId\":\"d1\"}],\"TotalCount\":101,\"FilteredCount\":101}")]
    public async Task List_proxy_answers_the_list_component_envelope_from_the_api_counts(string apiAnswer)
    {
        var result = await Controller(new Wire(apiAnswer), Read).List(CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        using var body = JsonDocument.Parse(content.Content!);
        var page = body.RootElement.GetProperty("data");
        Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        Assert.Equal(101, page.GetProperty("total").GetInt64());
        Assert.Equal(101, page.GetProperty("filteredTotal").GetInt64());
    }

    [Fact]
    public async Task List_proxy_keeps_the_total_and_the_filtered_count_apart()
    {
        var wire = new Wire("{\"outcome\":\"Succeeded\",\"items\":[],\"totalCount\":240,\"filteredCount\":37}");

        var content = Assert.IsType<ContentResult>(await Controller(wire, Read).List(CancellationToken.None));

        using var body = JsonDocument.Parse(content.Content!);
        Assert.Equal(240, body.RootElement.GetProperty("data").GetProperty("total").GetInt64());
        Assert.Equal(37, body.RootElement.GetProperty("data").GetProperty("filteredTotal").GetInt64());
    }

    [Fact]
    public async Task A_refusal_of_the_api_is_passed_on_with_its_status_and_body()
    {
        const string refusal = "{\"outcome\":\"Blocked\",\"reasonCode\":\"PVG_INTAKE_DRAFT_VERSION_CONFLICT\",\"items\":[]}";
        var wire = new Wire(refusal, HttpStatusCode.Conflict);

        var list = await Controller(wire, Read).List(CancellationToken.None);
        var update = await Controller(wire, Update).UpdateDraft(DraftId, Input(version: 2), CancellationToken.None);

        foreach (var result in new[] { list, update })
        {
            var content = Assert.IsType<ContentResult>(result);
            Assert.Equal(StatusCodes.Status409Conflict, content.StatusCode);
            Assert.Equal(refusal, content.Content);
        }
    }

    [Fact]
    public async Task A_page_without_counts_is_a_gateway_error_not_a_zero()
    {
        var result = await Controller(new Wire("{\"outcome\":\"Succeeded\",\"items\":[]}"), Read).List(CancellationToken.None);

        Assert.Equal(StatusCodes.Status502BadGateway, Assert.IsType<ContentResult>(result).StatusCode);
    }

    // ------------------------------------------------------------------------------------------------ W7 (review 12, F-1)

    [Theory]
    [InlineData("start=100&length=100", "pageNumber=2&pageSize=100")]
    [InlineData("start=50&length=25", "pageNumber=3&pageSize=25")]
    [InlineData("start=0&length=500", "pageNumber=1&pageSize=100")]
    [InlineData("start=200&length=500", "pageNumber=3&pageSize=100")]
    [InlineData("start=0", "pageNumber=1&pageSize=25")]
    [InlineData("", "pageNumber=1&pageSize=25")]
    [InlineData("start=0&length=0", "pageNumber=1&pageSize=25")]
    [InlineData("start=0&length=-1", "pageNumber=1&pageSize=25")]
    [InlineData("start=-5&length=25", "pageNumber=1&pageSize=25")]
    [InlineData("start=abc&length=25", "pageNumber=1&pageSize=25")]
    [InlineData("start=9223372036854775807&length=100", "pageNumber=1000000&pageSize=100")]
    [InlineData("start=99999999999999999999999&length=100", "pageNumber=1000000&pageSize=100")]
    [InlineData("start=0&length=25&status=%20Triaged%20", "pageNumber=1&pageSize=25&status=Triaged")]
    [InlineData("start=0&length=25&status=%20", "pageNumber=1&pageSize=25")]
    [InlineData("draw=3&start=25&length=25&search%5Bvalue%5D=x&columns%5B0%5D%5Bdata%5D=a&order%5B0%5D%5Bcolumn%5D=1&order%5B0%5D%5Bdir%5D=desc&search=y&orderBy=status",
        "pageNumber=2&pageSize=25")]
    public async Task List_proxy_maps_start_and_length_to_page_number_and_clamped_page_size(string browserQuery, string gatewayQuery)
    {
        var wire = new Wire(ListAnswer);

        await ControllerFor(wire, [Read], browserQuery).List(CancellationToken.None);

        var request = Assert.Single(wire.Requests);
        Assert.Equal($"?{gatewayQuery}", request.RequestUri!.Query);
        Assert.Equal($"{GatewayUrl}/api/pv-case-intake-triage", request.RequestUri.GetLeftPart(UriPartial.Path));
        foreach (var forbidden in new[] { "draw", "search", "columns", "order" })
        {
            Assert.DoesNotContain(forbidden, request.RequestUri.Query, StringComparison.Ordinal);
        }
    }

    // ------------------------------------------------------------------------------------------------ helpers

    private static PharmacovigilanceCaseIntakeTriageController.CaseIntakeFormInput Input(long? version = null) =>
        new(FormInput.IntakeChannel, FormInput.SourceType, FormInput.SourceReference, FormInput.ReceivedAtUtc,
            FormInput.ReporterType, FormInput.ReporterContactSummary, FormInput.PatientSubjectCode, FormInput.EventOnsetDate,
            FormInput.AdverseEventNarrative, FormInput.SuspectProductText, FormInput.Seriousness, FormInput.IntakePriority,
            FormInput.EvidenceLinkReferencesRaw, version);

    private static PharmacovigilanceCaseIntakeTriageController Controller(Wire wire, params string[] permissions) =>
        ControllerFor(wire, permissions);

    private static PharmacovigilanceCaseIntakeTriageController ControllerFor(
        Wire wire, string[] permissions, string queryString = "", bool withTenantClaim = true, string cookieToken = "synthetic-pvg-dl7a")
    {
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = GatewayUrl }).Build();
        var claims = new List<Claim> { new("sub", "7b2c51f0-0000-4000-8000-0000000000a1"), new("actor_type", "tenant_user") };
        if (withTenantClaim)
        {
            claims.Add(new Claim("tenant_id", TenantId));
        }

        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "fixture")), TraceIdentifier = "trace-pvg-dl7a" };
        // Synthetic fixture credential: only seen by the recording handler, never sent to a network.
        context.Request.Headers.Cookie = $"access_token={cookieToken}";
        context.Request.QueryString = new QueryString(queryString.Length == 0 ? string.Empty : $"?{queryString}");
        return new PharmacovigilanceCaseIntakeTriageController(
            new HttpClient(wire), gateway, new Echo<Diten.Web.SharedResource>(), NullLogger<PharmacovigilanceCaseIntakeTriageController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = context } };
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", $"access_token={token}");
        request.Headers.Add("Accept", "text/html");
        request.Headers.Add("Accept-Language", "en");
        return await client.SendAsync(request);
    }

    private static string Token(string signingKey, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new("sub", "7b2c51f0-0000-4000-8000-0000000000a1"),
            new("tenant_id", TenantId),
            new("tenantId", TenantId),
            new("actor_type", "tenant_user")
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: "pvg-web-test",
            audience: "pvg-web-test",
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256)));
    }

    /// <summary>Records every request (with its body) and answers one fixed body.</summary>
    private sealed class Wire(string answer, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct));
            return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class Echo<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
