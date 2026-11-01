using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
/// W1/W3 — every one of the eleven actions checks its pvg.case-intake-triage.* key FIRST (page actions: bare 403 that the
/// status-code pages re-execute to the shared /Home/Status/403; JSON actions: 403 with a message body in the reader's
/// language) and the Gateway is not called; W2 — UAS-001: without the read key the page is the shared 403 page and
/// nothing of the screen; W4 — Bearer, tenant and correlation come only from the verified principal (no X-Diten-*
/// header, no unverified cookie token); W5 — the record version travels to the API; W6/W7 — the list component's
/// start/length become the API's pageNumber/pageSize (at most 100) and the API's counts become the component's envelope.
/// MOD-0230 WP-0230-DL7B: W8/W9/W10 — the revision 04 fields travel with the API's names, the response-only fields never
/// do, and a link removal never comes with a selection; W11 — the option codes pass through unchanged behind the read key;
/// W12 — the form as the real view draws it, through the real MVC model binder and antiforgery check.
/// MOD-0230 WP-0230-DL7B-R1: W13 — the real view draws the bridge with the camelCase keys its loader turns to PascalCase;
/// W14 — the read proxy paths (Options, Detail) carry the verified identity, and without a tenant claim make no call.
/// MOD-0230 WP-0230-DL7C: W15 — every screen draws the bridge, the loader and its own script, once each and in that order;
/// W16 — the bridge carries the shared RecordSaved text; W17 — both date inputs of the form accept seconds.
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
        "Portal", "Spontaneous", "REF-1", "2026-10-20T10:00", "Consumer", null, "SUBJ-1", null, "narrative",
        [
            new("Suspect", "synthetic-product", "synthetic-lot"),
            new("Concomitant", "synthetic-other-product", null)
        ],
        "Serious", "Routine", "E-1, E-2", "2026-10-20T10:00", "unknown", "Spontaneous", "S.Y.", "synthetic-profession", "unknown");

    private sealed record CaseIntake(
        string IntakeChannel, string SourceType, string SourceReference, string ReceivedAtUtc, string ReporterType,
        string? ReporterContactSummary, string PatientSubjectCode, string? EventOnsetDate, string AdverseEventNarrative,
        PharmacovigilanceCaseIntakeTriageController.ProductLineFormInput[] ProductLines, string Seriousness, string IntakePriority,
        string EvidenceLinkReferencesRaw, string AwarenessAtUtc, string EventCountry, string ReportType,
        string ReporterNameOrInitials, string ReporterProfession, string ReporterCountry);

    /// <summary>The eight fields the screen and the proxy never send (prompt NE (4)).</summary>
    private static readonly string[] NeverSent =
    [
        "ReporterSource", "ReporterSourceId", "ReporterCrmSearchState", "ValidityResult", "ValidityMissingCriteria",
        "GlobalProductId", "PresentationGskuId", "CompanyRecipientPersonId"
    ];

    // ------------------------------------------------------------------------------------------------ W1

    /// <summary>The eleven actions, each with the key it needs (NASIL, AC-2) and how to call it.</summary>
    private static readonly (string Action, string Key, bool IsPage, Func<PharmacovigilanceCaseIntakeTriageController, Task<IActionResult>> Call)[] Actions =
    [
        ("Index", Read, true, c => Task.FromResult(c.Index())),
        ("Details", Read, true, c => Task.FromResult(c.Details(DraftId))),
        ("Create", Create, true, c => Task.FromResult(c.Create())),
        ("Edit", Update, true, c => Task.FromResult(c.Edit(DraftId))),
        ("List", Read, false, c => c.List(CancellationToken.None)),
        ("Detail", Read, false, c => c.Detail(DraftId, CancellationToken.None)),
        ("Options", Read, false, c => c.Options(CancellationToken.None)),
        ("CreateDraft", Create, false, c => c.CreateDraft(Input(), CancellationToken.None)),
        ("UpdateDraft", Update, false, c => c.UpdateDraft(DraftId, Input(), CancellationToken.None)),
        ("TriageDraft", Triage, false, c => c.TriageDraft(DraftId, new("Triaged", null, null), CancellationToken.None)),
        ("RouteDraft", Route, false, c => c.RouteDraft(DraftId, new("PV_SAFETY_REVIEW"), CancellationToken.None))
    ];

    [Fact]
    public void The_controller_has_exactly_the_actions_this_test_maps_to_a_key()
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
    [InlineData("Options")]
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

    // ------------------------------------------------------------------------------------------------ W8

    [Fact]
    public async Task Form_inputs_carry_the_revision04_fields_with_api_names()
    {
        var wire = new Wire(ListAnswer);
        // Two real lines, a line of nothing and a line of spaces: only the two real ones travel.
        var lines = FormInput.ProductLines.Concat([new(null, null, null), new("  ", " ", "   ")]).ToArray();

        await Controller(wire, Create).CreateDraft(Input(lines: lines), CancellationToken.None);
        await Controller(wire, Update).UpdateDraft(DraftId, Input(version: 2, lines: lines), CancellationToken.None);

        Assert.Equal(2, wire.Requests.Count);
        foreach (var sent in wire.Bodies)
        {
            using var body = JsonDocument.Parse(sent);
            var root = body.RootElement;
            Assert.Equal("Spontaneous", root.GetProperty("ReportType").GetString());
            Assert.Equal("unknown", root.GetProperty("EventCountry").GetString());
            Assert.Equal("S.Y.", root.GetProperty("ReporterNameOrInitials").GetString());
            Assert.Equal("synthetic-profession", root.GetProperty("ReporterProfession").GetString());
            Assert.Equal("unknown", root.GetProperty("ReporterCountry").GetString());
            Assert.False(root.TryGetProperty("SuspectProductText", out _));
            Assert.Equal("2026-10-20T10:00:00Z", root.GetProperty("AwarenessAtUtc").GetString());
            Assert.Equal("2026-10-20T10:00:00Z", root.GetProperty("ReceivedAtUtc").GetString());

            var productLines = root.GetProperty("ProductLines");
            Assert.Equal(2, productLines.GetArrayLength());
            Assert.Equal("Suspect", LineValue(productLines[0], "ProductRole"));
            Assert.Equal("synthetic-product", LineValue(productLines[0], "ProductNameAsReported"));
            Assert.Equal("synthetic-lot", LineValue(productLines[0], "LotNumber"));
            Assert.Equal("Concomitant", LineValue(productLines[1], "ProductRole"));
            Assert.Equal("synthetic-other-product", LineValue(productLines[1], "ProductNameAsReported"));
        }

        // Seconds survive: the regulatory day-0 moment does not move when a record is saved.
        var withSeconds = new Wire(ListAnswer);
        await Controller(withSeconds, Create).CreateDraft(Input() with { AwarenessAtUtc = "2026-10-20T07:00:30" }, CancellationToken.None);
        using var secondsBody = JsonDocument.Parse(Assert.Single(withSeconds.Bodies));
        Assert.Equal("2026-10-20T07:00:30Z", secondsBody.RootElement.GetProperty("AwarenessAtUtc").GetString());

        // A date that cannot be read stops at the proxy, for either field, and its value is not repeated back.
        const string unreadable = "2026-13-40T99:99";
        var refusedWire = new Wire(ListAnswer);
        foreach (var input in new[] { Input(version: 2) with { AwarenessAtUtc = unreadable }, Input(version: 2) with { ReceivedAtUtc = unreadable } })
        {
            foreach (var result in new[]
            {
                await Controller(refusedWire, Create).CreateDraft(input, CancellationToken.None),
                await Controller(refusedWire, Update).UpdateDraft(DraftId, input, CancellationToken.None)
            })
            {
                var content = Assert.IsType<ContentResult>(result);
                Assert.Equal(StatusCodes.Status400BadRequest, content.StatusCode);
                using var body = JsonDocument.Parse(content.Content!);
                Assert.Equal("VALIDATION_FAILED", body.RootElement.GetProperty("reason_code").GetString());
                Assert.DoesNotContain(unreadable, content.Content!, StringComparison.Ordinal);
            }
        }

        Assert.Empty(refusedWire.Requests);
    }

    private static string? LineValue(JsonElement line, string name)
    {
        // The proxy serializes with JsonSerializerDefaults.Web, so a nested key may carry either case.
        var property = line.EnumerateObject().Single(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        return property.Value.GetString();
    }

    // ------------------------------------------------------------------------------------------------ W9

    [Fact]
    public async Task Proxy_never_sends_response_only_or_unverifiable_fields()
    {
        var wire = new Wire(ListAnswer);

        await Controller(wire, Create).CreateDraft(Input(), CancellationToken.None);
        await Controller(wire, Update).UpdateDraft(DraftId, Input(version: 2), CancellationToken.None);

        Assert.Equal(2, wire.Bodies.Count);
        foreach (var sent in wire.Bodies)
        {
            using var body = JsonDocument.Parse(sent);
            foreach (var field in NeverSent)
            {
                // A null value is a sent key too.
                Assert.False(body.RootElement.TryGetProperty(field, out _), field);
            }
        }
    }

    // ------------------------------------------------------------------------------------------------ W10

    [Fact]
    public async Task Remove_link_with_a_selection_pair_is_refused_without_a_gateway_call()
    {
        var refused = new Wire(ListAnswer);
        foreach (var input in new[]
        {
            Input(version: 2) with { RemoveReporterLink = true, ReporterSelectionDirectory = "Crm", ReporterSelectionId = "synthetic-c" },
            Input(version: 2) with { RemoveReporterLink = true, ReporterSelectionId = "synthetic-c" },
            Input(version: 2) with { RemoveReporterLink = true, ReporterSelectionDirectory = "Crm" }
        })
        {
            var content = Assert.IsType<ContentResult>(await Controller(refused, Update).UpdateDraft(DraftId, input, CancellationToken.None));
            Assert.Equal(StatusCodes.Status400BadRequest, content.StatusCode);
            using var body = JsonDocument.Parse(content.Content!);
            Assert.Equal("PVG_REPORTER_LINK_REMOVAL_WITH_SELECTION", body.RootElement.GetProperty("reason_code").GetString());
            Assert.DoesNotContain("synthetic-c", content.Content!, StringComparison.Ordinal);
        }

        Assert.Empty(refused.Requests);

        // The marker alone travels, and no key of the pair travels with it.
        var removal = new Wire(ListAnswer);
        await Controller(removal, Update).UpdateDraft(DraftId, Input(version: 2) with { RemoveReporterLink = true }, CancellationToken.None);
        using (var body = JsonDocument.Parse(Assert.Single(removal.Bodies)))
        {
            Assert.True(body.RootElement.GetProperty("RemoveReporterLink").GetBoolean());
            Assert.False(body.RootElement.TryGetProperty("ReporterSelectionDirectory", out _));
            Assert.False(body.RootElement.TryGetProperty("ReporterSelectionId", out _));
        }

        // An update without the marker has no such key at all.
        var plain = new Wire(ListAnswer);
        await Controller(plain, Update).UpdateDraft(DraftId, Input(version: 2), CancellationToken.None);
        using (var body = JsonDocument.Parse(Assert.Single(plain.Bodies)))
        {
            Assert.False(body.RootElement.TryGetProperty("RemoveReporterLink", out _));
            Assert.False(body.RootElement.TryGetProperty("ReporterSelectionDirectory", out _));
            Assert.False(body.RootElement.TryGetProperty("ReporterSelectionId", out _));
        }

        // A create has no link to keep or remove, even if the form posted them.
        var create = new Wire(ListAnswer);
        await Controller(create, Create).CreateDraft(
            Input() with { RemoveReporterLink = true, ReporterSelectionDirectory = "Crm", ReporterSelectionId = "synthetic-c" }, CancellationToken.None);
        using (var body = JsonDocument.Parse(Assert.Single(create.Bodies)))
        {
            Assert.False(body.RootElement.TryGetProperty("RemoveReporterLink", out _));
            Assert.False(body.RootElement.TryGetProperty("ReporterSelectionDirectory", out _));
            Assert.False(body.RootElement.TryGetProperty("ReporterSelectionId", out _));
        }
    }

    // ------------------------------------------------------------------------------------------------ W11

    [Fact]
    public async Task Options_action_checks_the_read_key_first_and_passes_only_codes()
    {
        // Deliberately not in alphabetical order: the order is the service's, and the proxy keeps it.
        const string serviceAnswer = "{\"reportTypes\":[\"Study\",\"Other\",\"Spontaneous\"],\"productRoles\":[\"Suspect\",\"Concomitant\"],\"unknownCountry\":\"unknown\"}";

        var deniedWire = new Wire(serviceAnswer);
        var denied = Assert.IsType<ContentResult>(await Controller(deniedWire, Create, Update, Triage, Route).Options(CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, denied.StatusCode);
        Assert.Empty(deniedWire.Requests);

        var wire = new Wire(serviceAnswer);
        var allowed = Assert.IsType<ContentResult>(await Controller(wire, Read).Options(CancellationToken.None));

        Assert.Equal(StatusCodes.Status200OK, allowed.StatusCode);
        var request = Assert.Single(wire.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/pv-case-intake-triage/options", request.RequestUri!.AbsolutePath);
        using var body = JsonDocument.Parse(allowed.Content!);
        Assert.Equal(["Study", "Other", "Spontaneous"], body.RootElement.GetProperty("reportTypes").EnumerateArray().Select(code => code.GetString()!));
        Assert.Equal(["Suspect", "Concomitant"], body.RootElement.GetProperty("productRoles").EnumerateArray().Select(code => code.GetString()!));
        Assert.Equal("unknown", body.RootElement.GetProperty("unknownCountry").GetString());
        Assert.Equal(3, body.RootElement.EnumerateObject().Count());
    }

    // ------------------------------------------------------------------------------------------------ W12

    [Fact]
    public async Task Create_form_post_binds_the_product_lines_through_the_mvc_model_binder()
    {
        var signingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var wire = new Wire(ListAnswer);
        using var host = HostOver(wire, signingKey);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

        var createForm = await DrawnFormAsync(client, "/Pharmacovigilance/CaseIntakeTriage/Create", Token(signingKey, Create));
        var createToken = Token(signingKey, Create);

        // (a) Every field the view draws, two product lines, and the fields no form may set, on both levels.
        var lineExtras = new[] { ("ProductLines[0].GlobalProductId", "synthetic-gp"), ("ProductLines[0].PresentationGskuId", "synthetic-gsku") };
        var topExtras = new[] { ("ReporterSource", "Crm"), ("ReporterSourceId", "synthetic-rs"), ("CompanyRecipientPersonId", "synthetic-p"), ("ValidityResult", "Valid") };
        var fields = createForm.Fields([0, 1]).Concat(lineExtras).Concat(topExtras).ToArray();
        using (var posted = await PostFormAsync(client, "/Pharmacovigilance/CaseIntakeTriage/api/create", createToken, createForm, fields))
        {
            Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        }

        using (var body = JsonDocument.Parse(SentBody(wire, HttpMethod.Post, "/api/pv-case-intake-triage")))
        {
            var root = body.RootElement;
            var lines = root.GetProperty("ProductLines");
            Assert.Equal(2, lines.GetArrayLength());
            foreach (var line in lines.EnumerateArray())
            {
                Assert.Equal(
                    new[] { "LOTNUMBER", "PRODUCTNAMEASREPORTED", "PRODUCTROLE" },
                    line.EnumerateObject().Select(property => property.Name.ToUpperInvariant()).Order(StringComparer.Ordinal));
            }

            Assert.Equal("synthetic-ProductLines[0].ProductNameAsReported", LineValue(lines[0], "ProductNameAsReported"));
            Assert.Equal("synthetic-ProductLines[1].LotNumber", LineValue(lines[1], "LotNumber"));
            foreach (var field in new[] { "EventCountry", "ReportType", "ReporterNameOrInitials", "ReporterProfession", "ReporterCountry" })
            {
                Assert.Equal($"synthetic-{field}", root.GetProperty(field).GetString());
            }

            Assert.Equal("2026-10-20T10:00:00Z", root.GetProperty("AwarenessAtUtc").GetString());
            foreach (var extra in lineExtras.Select(extra => extra.Item1["ProductLines[0].".Length..]).Concat(topExtras.Select(extra => extra.Item1)))
            {
                Assert.False(CarriesKey(root, extra), extra);
            }
        }

        // (b) A gap in the numbers: the binder stops at it. This is why the form numbers its lines without a gap.
        wire.Requests.Clear();
        wire.Bodies.Clear();
        using (var gapped = await PostFormAsync(client, "/Pharmacovigilance/CaseIntakeTriage/api/create", createToken, createForm, createForm.Fields([0, 2]).ToArray()))
        {
            Assert.Equal(HttpStatusCode.OK, gapped.StatusCode);
        }

        using (var body = JsonDocument.Parse(SentBody(wire, HttpMethod.Post, "/api/pv-case-intake-triage")))
        {
            Assert.Equal(1, body.RootElement.GetProperty("ProductLines").GetArrayLength());
        }

        // (c) The update path: the stored pair travels together, and the removal marker travels alone.
        var updateToken = Token(signingKey, Update);
        var editForm = await DrawnFormAsync(client, $"/Pharmacovigilance/CaseIntakeTriage/Edit/{DraftId}", updateToken);
        var updateUrl = $"/Pharmacovigilance/CaseIntakeTriage/api/update/{DraftId}";
        var updatePath = $"/api/pv-case-intake-triage/{DraftId}";
        var version = editForm.Hidden("ExpectedVersion", "7");

        wire.Requests.Clear();
        wire.Bodies.Clear();
        var withPair = editForm.Fields([0]).Append(version)
            .Append(editForm.Hidden("ReporterSelectionDirectory", "Crm"))
            .Append(editForm.Hidden("ReporterSelectionId", "synthetic-c"))
            .ToArray();
        using (var posted = await PostFormAsync(client, updateUrl, updateToken, editForm, withPair))
        {
            Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        }

        using (var body = JsonDocument.Parse(SentBody(wire, HttpMethod.Put, updatePath)))
        {
            Assert.Equal("Crm", body.RootElement.GetProperty("ReporterSelectionDirectory").GetString());
            Assert.Equal("synthetic-c", body.RootElement.GetProperty("ReporterSelectionId").GetString());
            Assert.Equal(7, body.RootElement.GetProperty("ExpectedVersion").GetInt64());
            Assert.False(body.RootElement.TryGetProperty("RemoveReporterLink", out _));
        }

        wire.Requests.Clear();
        wire.Bodies.Clear();
        var withRemoval = editForm.Fields([0]).Append(version).Append(editForm.Hidden("RemoveReporterLink", "true")).ToArray();
        using (var posted = await PostFormAsync(client, updateUrl, updateToken, editForm, withRemoval))
        {
            Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        }

        using (var body = JsonDocument.Parse(SentBody(wire, HttpMethod.Put, updatePath)))
        {
            Assert.True(body.RootElement.GetProperty("RemoveReporterLink").GetBoolean());
            Assert.False(body.RootElement.TryGetProperty("ReporterSelectionDirectory", out _));
            Assert.False(body.RootElement.TryGetProperty("ReporterSelectionId", out _));
        }
    }

    // ------------------------------------------------------------------------------------------------ W13

    [Fact]
    public async Task The_form_bridge_is_drawn_with_the_camelCase_keys_the_loader_expects()
    {
        var signingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        using var host = HostOver(new Wire(ListAnswer), signingKey);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

        using var page = await GetAsync(client, "/Pharmacovigilance/CaseIntakeTriage/Create", Token(signingKey, Create));
        var html = await page.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        var payload = Regex.Match(html, "<script id=\"pvg-case-intake-triage-l10n\" type=\"application/json\">([\\s\\S]*?)</script>");
        Assert.True(payload.Success, "the page drew no bridge");
        using var bridge = JsonDocument.Parse(payload.Groups[1].Value);
        var root = bridge.RootElement;

        Assert.True(root.TryGetProperty("reportTypeSpontaneous", out _));
        Assert.True(root.TryGetProperty("productRoleSuspect", out _));
        Assert.True(root.TryGetProperty("countryUnknown", out _));
        Assert.False(root.TryGetProperty("ReportTypeSpontaneous", out _));
        Assert.NotEmpty(root.EnumerateObject());
        Assert.All(root.EnumerateObject(), key => Assert.True(char.IsLower(key.Name[0]), key.Name));
    }

    // ------------------------------------------------------------------------------------------------ W14

    [Theory]
    [InlineData("Options")]
    [InlineData("Detail")]
    public async Task The_read_proxy_paths_carry_the_verified_identity_and_are_401_without_a_tenant(string action)
    {
        var (_, key, _, call) = Actions.Single(entry => entry.Action == action);
        var wire = new Wire(ListAnswer);

        await call(Controller(wire, key));

        var request = Assert.Single(wire.Requests);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("synthetic-pvg-dl7a", request.Headers.Authorization?.Parameter);
        Assert.Equal(TenantId, Assert.Single(request.Headers.GetValues("X-Tenant-Id")));
        Assert.Equal("trace-pvg-dl7a", Assert.Single(request.Headers.GetValues("X-Correlation-Id")));
        Assert.DoesNotContain(request.Headers, header => header.Key.StartsWith("X-Diten-", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(request.Headers, header => header.Key.Contains("Actor", StringComparison.OrdinalIgnoreCase));

        // An UNSIGNED token that names a tenant: nothing in the proxy may read it back (it is not verified).
        var unsigned = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: [new Claim("tenant_id", TenantId), new Claim("sub", "someone")]));
        var refusedWire = new Wire(ListAnswer);

        var refused = await call(ControllerFor(refusedWire, [key], withTenantClaim: false, cookieToken: unsigned));

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<ContentResult>(refused).StatusCode);
        Assert.Empty(refusedWire.Requests);
    }

    // ------------------------------------------------------------------------------------------------ W15

    [Theory]
    [InlineData("Create", Create, "form.js")]
    [InlineData("Edit", Update, "form.js")]
    [InlineData("Details", Read, "details.js")]
    [InlineData("Index", Read, "index.js")]
    public async Task Every_screen_draws_the_bridge_then_the_loader_then_its_own_script(string page, string key, string script)
    {
        var path = page switch
        {
            "Index" => string.Empty,
            "Create" => "/Create",
            _ => $"/{page}/{DraftId}"
        };

        var html = await DrawnHtmlAsync($"/Pharmacovigilance/CaseIntakeTriage{path}", key);

        var inOrder = new[] { "id=\"pvg-case-intake-triage-l10n\"", "CaseIntakeTriage/index.l10n.js", $"CaseIntakeTriage/{script}" };
        foreach (var marker in inOrder)
        {
            Assert.True(Regex.Matches(html, Regex.Escape(marker)).Count == 1, $"{page}: {marker} is not drawn exactly once");
        }

        var positions = inOrder.Select(marker => html.IndexOf(marker, StringComparison.Ordinal)).ToArray();
        Assert.True(positions[0] < positions[1] && positions[1] < positions[2], $"{page}: {string.Join(", ", positions)}");
    }

    // ------------------------------------------------------------------------------------------------ W16

    [Fact]
    public async Task The_bridge_carries_the_shared_RecordSaved_text()
    {
        var html = await DrawnHtmlAsync("/Pharmacovigilance/CaseIntakeTriage/Create", Create);

        var payload = Regex.Match(html, "<script id=\"pvg-case-intake-triage-l10n\" type=\"application/json\">([\\s\\S]*?)</script>");
        Assert.True(payload.Success, "the page drew no bridge");
        using var bridge = JsonDocument.Parse(payload.Groups[1].Value);
        Assert.True(bridge.RootElement.TryGetProperty("recordSaved", out var saved), "the bridge has no recordSaved");
        Assert.False(string.IsNullOrWhiteSpace(saved.GetString()));
        Assert.NotEqual("RecordSaved", saved.GetString());
    }

    // ------------------------------------------------------------------------------------------------ W17

    [Theory]
    [InlineData("ReceivedAtUtc")]
    [InlineData("AwarenessAtUtc")]
    public async Task Both_date_inputs_of_the_form_accept_seconds(string field)
    {
        var html = await DrawnHtmlAsync("/Pharmacovigilance/CaseIntakeTriage/Create", Create);

        // The input's own tag: an attribute of the other input cannot answer for this one.
        var input = Regex.Match(html, $"<input\\b[^>]*\\bname=\"{field}\"[^>]*>");
        Assert.True(input.Success, $"{field} is not drawn");
        Assert.Contains("type=\"datetime-local\"", input.Value, StringComparison.Ordinal);
        Assert.Contains("step=\"1\"", input.Value, StringComparison.Ordinal);
    }

    private async Task<string> DrawnHtmlAsync(string url, string key)
    {
        var signingKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        using var host = HostOver(new Wire(ListAnswer), signingKey);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        using var page = await GetAsync(client, url, Token(signingKey, key));
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        return await page.Content.ReadAsStringAsync();
    }

    private WebApplicationFactory<Program> HostOver(Wire wire, string signingKey) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("JwtSettings:Secret", signingKey);
            builder.UseSetting("JwtSettings:Issuer", "pvg-web-test");
            builder.UseSetting("JwtSettings:Audience", "pvg-web-test");
            builder.UseSetting("GatewayUrl", "http://127.0.0.1:9");
            builder.UseSetting("PlatformServiceUrl", "http://127.0.0.1:9");
            builder.ConfigureTestServices(services =>
                services.AddTransient(_ => new HttpClient(wire, disposeHandler: false)));
        });

    /// <summary>The field names of the form as the real view drew it, with the antiforgery pair of that page. Nothing here
    /// lists a field by hand: a name the view gets wrong is the name the test posts.</summary>
    private sealed record DrawnForm(string Cookie, string FormToken, IReadOnlyList<(string Name, string Type)> Inputs)
    {
        public IEnumerable<(string Name, string Value)> Fields(int[] lineIndexes)
        {
            foreach (var (name, type) in Inputs.Where(input => input.Type != "hidden"))
            {
                if (name.Contains("__index__", StringComparison.Ordinal))
                {
                    foreach (var index in lineIndexes)
                    {
                        var line = name.Replace("__index__", index.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
                        yield return (line, $"synthetic-{line}");
                    }

                    continue;
                }

                yield return (name, type switch
                {
                    "datetime-local" => "2026-10-20T10:00",
                    "date" => "2026-10-20",
                    _ => $"synthetic-{name}"
                });
            }
        }

        public (string Name, string Value) Hidden(string name, string value)
        {
            Assert.Contains(Inputs, input => input.Name == name && input.Type == "hidden");
            return (name, value);
        }
    }

    private static async Task<DrawnForm> DrawnFormAsync(HttpClient client, string url, string token)
    {
        using var page = await GetAsync(client, url, token);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        var formToken = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        var cookie = page.Headers.GetValues("Set-Cookie")
            .Select(value => value.Split(';')[0])
            .Single(value => value.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
        Assert.False(string.IsNullOrEmpty(formToken), "the page drew no antiforgery token");

        var form = Regex.Match(html, "<form id=\"pvg-case-intake-form\"[\\s\\S]*?</form>").Value;
        var inputs = Regex.Matches(form, "<(?:input|select|textarea)\\b[^>]*\\bname=\"([^\"]+)\"[^>]*>")
            .Select(match =>
            {
                var type = Regex.Match(match.Value, "\\btype=\"([^\"]+)\"").Groups[1].Value;
                return (Name: match.Groups[1].Value, Type: type);
            })
            .Where(input => input.Name != "__RequestVerificationToken")
            .ToArray();
        Assert.NotEmpty(inputs);
        return new DrawnForm(cookie, formToken, inputs);
    }

    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client, string url, string token, DrawnForm form, IEnumerable<(string Name, string Value)> fields)
    {
        var content = fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))
            .Append(new("__RequestVerificationToken", form.FormToken));
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(content) };
        request.Headers.Add("Cookie", $"access_token={token}; {form.Cookie}");
        request.Headers.Add("Accept", "application/json");
        return await client.SendAsync(request);
    }

    /// <summary>Drawing a page can reach the same handler: the body read is the one of this method and path.</summary>
    private static string SentBody(Wire wire, HttpMethod method, string path)
    {
        var index = wire.Requests.FindIndex(request =>
            request.Method == method && request.RequestUri!.AbsolutePath.EndsWith(path, StringComparison.Ordinal));
        Assert.True(index >= 0, $"{method} {path} never reached the handler");
        return wire.Bodies[index];
    }

    private static bool CarriesKey(JsonElement element, string key) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Any(property =>
            string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase) || CarriesKey(property.Value, key)),
        JsonValueKind.Array => element.EnumerateArray().Any(item => CarriesKey(item, key)),
        _ => false
    };

    // ------------------------------------------------------------------------------------------------ helpers

    private static PharmacovigilanceCaseIntakeTriageController.CaseIntakeFormInput Input(
        long? version = null, PharmacovigilanceCaseIntakeTriageController.ProductLineFormInput[]? lines = null) =>
        new(FormInput.IntakeChannel, FormInput.SourceType, FormInput.SourceReference, FormInput.ReceivedAtUtc,
            FormInput.ReporterType, FormInput.ReporterContactSummary, FormInput.PatientSubjectCode, FormInput.EventOnsetDate,
            FormInput.AdverseEventNarrative, lines ?? FormInput.ProductLines, FormInput.Seriousness, FormInput.IntakePriority,
            FormInput.EvidenceLinkReferencesRaw, FormInput.AwarenessAtUtc, FormInput.EventCountry, FormInput.ReportType,
            FormInput.ReporterNameOrInitials, FormInput.ReporterProfession, FormInput.ReporterCountry, ExpectedVersion: version);

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
