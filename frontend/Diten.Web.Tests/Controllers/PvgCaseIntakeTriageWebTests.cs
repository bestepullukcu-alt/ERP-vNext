extern alias pvgapi;

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Infrastructure.RegPvBase;
using Diten.PvgService.Domain.RegPvBase;
using Diten.Web.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using Xunit.Abstractions;
using PvgApiHost = pvgapi::Diten.PvgService.Api.PvgServiceApiHost;

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
public sealed class PvgCaseIntakeTriageWebTests(WebApplicationFactory<Program> factory, ITestOutputHelper output, PvgCaseIntakeTriageWebTests.ConnectedMatrixFixture matrix)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<PvgCaseIntakeTriageWebTests.ConnectedMatrixFixture>
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
        ("UpdateDraft", Update, false, c => UpdateDraftAsync(c, DraftId, Input(), CancellationToken.None)),
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

        await UpdateDraftAsync(Controller(wire, Update), DraftId, Input(version: 7), CancellationToken.None);
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
        var update = await UpdateDraftAsync(Controller(wire, Update), DraftId, Input(version: 2), CancellationToken.None);

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
        await UpdateDraftAsync(Controller(wire, Update), DraftId, Input(version: 2, lines: lines), CancellationToken.None);

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
                await UpdateDraftAsync(Controller(refusedWire, Update), DraftId, input, CancellationToken.None)
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
        await UpdateDraftAsync(Controller(wire, Update), DraftId, Input(version: 2), CancellationToken.None);

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
            var content = Assert.IsType<ContentResult>(await UpdateDraftAsync(Controller(refused, Update), DraftId, input, CancellationToken.None));
            Assert.Equal(StatusCodes.Status400BadRequest, content.StatusCode);
            using var body = JsonDocument.Parse(content.Content!);
            Assert.Equal("PVG_REPORTER_LINK_REMOVAL_WITH_SELECTION", body.RootElement.GetProperty("reason_code").GetString());
            Assert.DoesNotContain("synthetic-c", content.Content!, StringComparison.Ordinal);
        }

        Assert.Empty(refused.Requests);

        // The marker alone travels, and no key of the pair travels with it.
        var removal = new Wire(ListAnswer);
        await UpdateDraftAsync(Controller(removal, Update), DraftId, Input(version: 2) with { RemoveReporterLink = true }, CancellationToken.None);
        using (var body = JsonDocument.Parse(Assert.Single(removal.Bodies)))
        {
            Assert.True(body.RootElement.GetProperty("RemoveReporterLink").GetBoolean());
            Assert.False(body.RootElement.TryGetProperty("ReporterSelectionDirectory", out _));
            Assert.False(body.RootElement.TryGetProperty("ReporterSelectionId", out _));
        }

        // An update without the marker has no such key at all.
        var plain = new Wire(ListAnswer);
        await UpdateDraftAsync(Controller(plain, Update), DraftId, Input(version: 2), CancellationToken.None);
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
            .Append(("UpdateValuesJson", UpdateFixtureJson(Input(version: 7) with { ReporterSelectionDirectory = "Crm", ReporterSelectionId = "synthetic-c" })))
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
        var withRemoval = editForm.Fields([0]).Append(version).Append(editForm.Hidden("RemoveReporterLink", "true"))
            .Append(("UpdateValuesJson", UpdateFixtureJson(Input(version: 7) with { RemoveReporterLink = true }))).ToArray();
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

    private const string BrowserPrefix = "/Pharmacovigilance/CaseIntakeTriage";
    private const string TokenMember = "__RequestVerificationToken";
    private const string TokenHeader = "RequestVerificationToken";
    private const string ApiPrefix = pvgapi::Diten.PvgService.Api.PvgCaseIntakeTriageEndpoints.RoutePrefix;

    [Fact]
    public Task Connected_edit_submission_reaches_the_real_store() => ConnectedEditAsync("Valid");

    [Fact]
    public Task Connected_edit_submission_preserves_browser_antiforgery_request_token() => ConnectedEditAsync("Valid");

    [Theory]
    [InlineData("MissingToken")]
    [InlineData("InvalidToken")]
    public Task Connected_edit_with_an_unusable_request_token_is_refused_by_mvc(string fault) =>
        ConnectedEditAsync(fault);

    [Theory]
    [InlineData("MissingNode")]
    [InlineData("MissingJsdom")]
    [InlineData("StartFailure")]
    [InlineData("Timeout")]
    [InlineData("BridgeFailure")]
    [InlineData("EarlyEnd")]
    [InlineData("Malformed")]
    [InlineData("MissingReceipt")]
    [InlineData("SuppressUpdate")]
    public async Task Connected_edit_test_fails_when_browser_runtime_or_update_leg_is_unavailable(string fault)
    {
        var failure = await Assert.ThrowsAsync<BrowserFailure>(() => ConnectedEditAsync(fault));
        Assert.Equal(fault, failure.Message);
    }

    private sealed class BrowserFailure(string reason) : Exception(reason);

    public static IEnumerable<object[]> LosslessCorpusCases()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(),
            "docs/records/audits/2026-11/pvg-dl12-fe-corpus-01/FE-CORPUS.json")));
        var cases = doc.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(47, cases.Length);
        Assert.Equal(47, cases.Select(row => row.GetProperty("name").GetString()).Distinct(StringComparer.Ordinal).Count());
        foreach (var row in cases) yield return [row.GetProperty("name").GetString()!];
    }

    [Fact]
    public Task Mvc_update_preserves_untouched_stored_text_and_reference_sequence() =>
        RunNewMatrixAsync("corpus", LosslessCorpusCases().Select(row => (string)row[0]),
            name => ConnectedEditAsync("Valid", caseName: name, lossless: true, receiptKind: "corpus"));

    [Fact]
    public async Task Edit_optional_source_reference_and_patient_code_match_service_validation()
    {
        // This named acceptance itself runs the connected browser/MVC/API/store chain.
        await ConnectedEditAsync("Valid", caseName: "source-and-patient-null", lossless: true);
        await ConnectedEditAsync("Valid", caseName: "source-and-patient-empty", lossless: true);
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        using var web = HostOver(new Wire(ListAnswer), key);
        using var client = web.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        foreach (var mode in new[] { "Create", "Edit/" + DraftId })
        {
            using var response = await GetAsync(client, BrowserPrefix + "/" + mode, Token(key, Create, Update));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            foreach (var field in new[] { "SourceReference", "PatientSubjectCode" })
            {
                var input = Regex.Match(html, "<input\\b[^>]*id=\"" + field + "\"[^>]*>").Value;
                var label = Regex.Match(html, "<label\\b[^>]*for=\"" + field + "\"[^>]*>[\\s\\S]*?</label>").Value;
                Assert.NotEmpty(input);
                Assert.NotEmpty(label);
                Assert.Equal(mode == "Create", Regex.IsMatch(input, "\\brequired(?:=|\\s|/|>)"));
                Assert.Equal(mode == "Create", label.Contains("text-danger", StringComparison.Ordinal));
            }
        }
    }

    [Theory]
    [InlineData("timestamp-seven-ticks-Z")]
    [InlineData("timestamp-seven-ticks-+0530")]
    [InlineData("timestamp-seven-ticks--0400")]
    [InlineData("timestamp-mixed-offsets")]
    public Task Edit_round_trip_preserves_subsecond_received_and_awareness_instants(string caseName) =>
        ConnectedEditAsync("Valid", caseName: caseName, lossless: true);

    [Theory]
    [InlineData("ReporterContactSummary")]
    [InlineData("SourceReference")]
    [InlineData("PatientSubjectCode")]
    [InlineData("EventOnsetDate")]
    public Task Edit_round_trip_clears_only_the_optional_field_the_user_emptied(string field) =>
        ConnectedEditAsync("Valid", caseName: "baseline", editField: field, editValue: "", lossless: true);

    [Theory]
    [InlineData("ReporterContactSummary")]
    [InlineData("SourceReference")]
    [InlineData("PatientSubjectCode")]
    [InlineData("EventOnsetDate")]
    public Task Edit_round_trip_sends_the_users_nonempty_change(string field) =>
        ConnectedEditAsync("Valid", caseName: "baseline", editField: field, editValue: field == "EventOnsetDate" ? "2020-02-02" : "changed-synthetic", lossless: true);

    [Fact]
    public Task Edit_accepts_the_services_explicit_empty_product_collection() =>
        ConnectedEditAsync("Valid", caseName: "baseline", lossless: true, emptyProducts: true);

    private static readonly string[] DetailMembers =
    [
        "intakeDraftId", "status", "version", "productLines", "awarenessAtUtc", "companyRecipientPersonId",
        "eventCountry", "reportType", "reporterSource", "reporterSourceId", "reporterCrmSearchState",
        "reporterNameOrInitials", "reporterProfession", "reporterCountry", "intakeChannel", "sourceType",
        "sourceReference", "receivedAtUtc", "reporterType", "reporterContactSummary", "patientSubjectCode",
        "eventOnsetDate", "adverseEventNarrative", "seriousness", "intakePriority", "triageOutcome", "triageReason",
        "routeTargetQueue", "evidenceLinkReferences"
    ];
    private static readonly string[] NullableDetailMembers =
    [
        "sourceReference", "reporterContactSummary", "patientSubjectCode", "eventOnsetDate", "companyRecipientPersonId",
        "reporterSourceId", "reporterNameOrInitials", "reporterProfession", "reporterCountry", "triageOutcome",
        "triageReason", "routeTargetQueue"
    ];
    private static readonly string[] ProductMembers =
        ["productRole", "globalProductId", "productNameAsReported", "presentationGskuId", "lotNumber"];

    public static IEnumerable<object[]> InvalidDetailCases()
    {
        foreach (var member in DetailMembers) yield return ["missing:" + member];
        foreach (var member in DetailMembers) yield return ["type:" + member];
        foreach (var member in ProductMembers) yield return ["nested-missing:" + member];
        foreach (var member in ProductMembers) yield return ["nested-type:" + member];
        foreach (var field in new[] { "status", "reporterSource", "reporterCrmSearchState", "intakeChannel", "sourceType", "reporterType", "adverseEventNarrative", "seriousness", "intakePriority", "eventCountry", "reportType" })
        {
            yield return ["blank:" + field];
            yield return ["empty:" + field];
        }
        foreach (var member in new[] { "productRole", "productNameAsReported" })
        {
            yield return ["nested-blank:" + member];
            yield return ["nested-empty:" + member];
        }
        foreach (var value in new[] { "draft-absent", "draft-null", "draft-number", "draft-array", "version-only", "wrong-id", "wrong-version", "zero-version", "fraction-version", "bad-day", "bad-received", "bad-awareness", "null-evidence", "number-evidence", "object-evidence", "null-products", "null-product", "array-product" })
            yield return [value];
    }

    [Fact]
    public Task Edit_requires_a_complete_well_shaped_detail_before_enabling_save() =>
        RunNewMatrixAsync("shape", InvalidDetailCases().Select(row => (string)row[0]),
            name => ConnectedEditAsync("Valid", caseName: "baseline", shapeCase: name, lossless: true, receiptKind: "shape"));

    private readonly object _receiptLock = new();

    private async Task RunNewMatrixAsync(string kind, IEnumerable<string> names, Func<string, Task> run)
    {
        var cases = names.ToArray();
        Assert.NotEmpty(cases);
        Assert.Equal(cases.Length, cases.Distinct(StringComparer.Ordinal).Count());
        _ = matrix.Host; // Warm actual hosts once before any parallel client/request; no fake response is used.
        using var slots = new SemaphoreSlim(4);
        var tasks = cases.Select(async name =>
        {
            await slots.WaitAsync();
            try { await run(name); }
            catch
            {
                lock (_receiptLock) output.WriteLine("Matrix {0} case {1} failed; all other rows and child cleanup are still awaited.", kind, name);
                throw;
            }
            finally { slots.Release(); }
        }).ToArray();
        // WhenAll completes only after every started/waiting row's finally/reap; a failed row always fails this Fact.
        await Task.WhenAll(tasks);
        WriteReceipt(new { kind = "batch", matrix = kind, cases, concurrency = 4, complete = true });
    }

    private void WriteReceipt(object receipt)
    {
        var json = JsonSerializer.Serialize(receipt);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        lock (_receiptLock) output.WriteLine("PVG_FE_ASSERTION_RECEIPT {0} sha256:{1}", json, hash);
    }

    private static string ReceiptHash(object value) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root missing.");
    }

    private static string CorpusPayload(string name)
    {
        var path = Path.Combine(RepositoryRoot(), "docs/records/audits/2026-11/pvg-dl12-fe-corpus-01/FE-CORPUS.json");
        var bytes = Encoding.UTF8.GetBytes(File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal("003eb12bd41eb6f7028accaed82a76e070862d4fa7678652e73ab3ba139a9adc",
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        using var doc = JsonDocument.Parse(bytes);
        var row = doc.RootElement.GetProperty("cases").EnumerateArray().Single(row => row.GetProperty("name").GetString() == name);
        return row.GetProperty("createPayload").GetProperty("jsonUtf8").GetString()!;
    }

    private static string MalformedDetail(string body, string shape)
    {
        var root = JsonNode.Parse(body)!.AsObject();
        var draft = root["draft"]!.AsObject();
        var parts = shape.Split(':', 2);
        if (parts.Length == 2)
        {
            switch (parts[0])
            {
                case "missing": draft.Remove(parts[1]); break;
                case "type": draft[parts[1]] = 42; break;
                case "blank": draft[parts[1]] = " \t "; break;
                case "empty": draft[parts[1]] = ""; break;
                case "nested-missing": draft["productLines"]![0]!.AsObject().Remove(parts[1]); break;
                case "nested-type": draft["productLines"]![0]![parts[1]] = 42; break;
                case "nested-blank": draft["productLines"]![0]![parts[1]] = " \t "; break;
                case "nested-empty": draft["productLines"]![0]![parts[1]] = ""; break;
                default: throw new InvalidOperationException(shape);
            }
        }
        else switch (shape)
        {
            case "draft-absent": root.Remove("draft"); break;
            case "draft-null": root["draft"] = null; break;
            case "draft-number": root["draft"] = 42; break;
            case "draft-array": root["draft"] = new JsonArray(); break;
            case "version-only": root.Remove("draft"); break;
            case "wrong-id": draft["intakeDraftId"] = "another-synthetic-id"; break;
            case "wrong-version": draft["version"] = 2; break;
            case "zero-version": draft["version"] = 0; break;
            case "fraction-version": draft["version"] = 1.5; break;
            case "bad-day": draft["eventOnsetDate"] = "2020-02-30"; break;
            case "bad-received": draft["receivedAtUtc"] = "2020-02-30T08:00:00Z"; break;
            case "bad-awareness": draft["awarenessAtUtc"] = "2020-03-01T08:00:00.12345678Z"; break;
            case "null-evidence": draft["evidenceLinkReferences"] = null; break;
            case "number-evidence": draft["evidenceLinkReferences"] = new JsonArray(42); break;
            case "object-evidence": draft["evidenceLinkReferences"] = new JsonArray(new JsonObject()); break;
            case "null-products": draft["productLines"] = null; break;
            case "null-product": draft["productLines"] = new JsonArray((JsonNode?)null); break;
            case "array-product": draft["productLines"] = new JsonArray(new JsonArray()); break;
            default: throw new InvalidOperationException(shape);
        }
        return root.ToJsonString();
    }

    private async Task ConnectedEditAsync(string fault, string? caseName = null, string? editField = null,
        string? editValue = null, string? shapeCase = null, bool lossless = false, bool emptyProducts = false, string receiptKind = "connected")
    {
        // Only the new matrix reuses immutable hosts. Every row still creates its own actual record,
        // client, Razor antiforgery session, browser and keyed metrics. Existing transport faults keep own hosts.
        var shared = lossless ? matrix.Host : null;
        var signingKey = shared?.SigningKey ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var jwt = Token(signingKey, Read, Create, Update);
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = signingKey,
            ["JwtSettings:Issuer"] = "pvg-web-test",
            ["JwtSettings:Audience"] = "pvg-web-test",
            [PvgApiHost.PersistenceStoreKey] = "InMemory"
        }).Build();
        var ports = new ConnectedPorts();
        using var ownApi = shared is null ? new TestServer(PvgApiHost.CreatePvgServiceWebHostBuilder(
            "Development", settings, "http://127.0.0.1:0", services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IPvgPermissionGate>(ports));
                services.Replace(ServiceDescriptor.Singleton<IPvgFieldSecurityPolicy>(ports));
                services.Replace(ServiceDescriptor.Singleton<IPvgWorkflowTransitionGate>(ports));
                services.Replace(ServiceDescriptor.Singleton<IPvgEvidenceLinkPort>(ports));
                services.Replace(ServiceDescriptor.Singleton<IPvgIntakeDraftStore>(new ObservedStore()));
            })) : null;
        var api = shared?.Api ?? ownApi!;
        var store = Assert.IsType<ObservedStore>(api.Services.GetRequiredService<IPvgIntakeDraftStore>());
        Assert.IsType<InMemoryPvgIntakeDraftRepository>(store.Inner);
        using var apiClient = api.CreateClient();
        var createJson = caseName is null ? null : CorpusPayload(caseName);
        if (emptyProducts)
        {
            var create = JsonNode.Parse(createJson!)!.AsObject();
            create["productLines"] = new JsonArray();
            createJson = create.ToJsonString();
        }
        var id = await SeedDraftAsync(apiClient, jwt, createJson);
        var firstRead = await ReadConnectedDraftAsync(apiClient, jwt, id);
        var scope = new PvgPersistenceTenantScope(TenantId);
        var before = Assert.IsType<PvgPersistedIntakeDraft>(await store.FindByIdAsync(scope, id));
        var writesBefore = store.Writes(scope, id);
        var attemptsBefore = store.ReplaceCalls(scope, id);
        using var ownGateway = shared is null ? new ConnectedGateway(api.CreateHandler(), id) : null;
        using var row = shared?.Register(id);
        IUpdateTraffic gateway = (IUpdateTraffic?)row ?? ownGateway!;
        ITokenTraffic observer = (ITokenTraffic?)row ?? new MvcTokenObserver($"{BrowserPrefix}/api/update/{id}");
        using var ownWeb = shared is null ? HostOver(ownGateway!, signingKey, (MvcTokenObserver)observer) : null;
        var web = shared?.Web ?? ownWeb!;
        using var client = shared is null
            ? web.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false })
            : shared.CreateClient();
        using var page = await GetAsync(client, $"{BrowserPrefix}/Edit/{id}", jwt);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        var renderedToken = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.True(renderedToken.Length > 0 && !html.Contains(jwt), "Expected a Razor request token and a server-only session credential.");
        var cookie = page.Headers.GetValues("Set-Cookie").Select(header => header.Split(';')[0])
            .Single(header => header.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
        var webRoot = web.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
        try
        {
            var status = await SubmitInBrowserAsync(webRoot, html, id, fault, client, $"access_token={jwt}; {cookie}", renderedToken, lossless, editField, editValue, shapeCase, firstRead);
            var after = Assert.IsType<PvgPersistedIntakeDraft>(await store.FindByIdAsync(scope, id));
            if (shapeCase is not null)
            {
                Assert.Equal(0, status);
                Assert.Equal(0, gateway.Updates);
                Assert.Equal(0, store.Writes(scope, id) - writesBefore);
                Assert.Equal(0, store.ReplaceCalls(scope, id) - attemptsBefore);
                Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
                output.WriteLine("Shape {0}: Save disabled, forced submit updates 0, all stored fields unchanged.", shapeCase);
                WriteReceipt(new { kind = receiptKind, @case = shapeCase, id, status, mvcPosts = observer.Posts,
                    apiUpdates = gateway.Updates, storeWrites = store.Writes(scope, id) - writesBefore,
                    storeReplaceCalls = store.ReplaceCalls(scope, id) - attemptsBefore, beforeHash = ReceiptHash(before), afterHash = ReceiptHash(after),
                    firstGetHash = ReceiptHash(firstRead), zeroWriteAndFullEqualityAssertions = true });
                return;
            }
            if (fault is "MissingToken" or "InvalidToken")
            {
                Assert.Equal(StatusCodes.Status400BadRequest, status);
                Assert.Equal(1, observer.Posts);
                Assert.False(observer.HeaderMatches(renderedToken));
                Assert.False(observer.FormMatches(renderedToken));
                Assert.Equal(0, gateway.Updates);
                Assert.Equal(0, store.Writes(scope, id) - writesBefore);
                Assert.Equal(0, store.ReplaceCalls(scope, id) - attemptsBefore);
                Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
                output.WriteLine("MVC status 400, API updates 0, stored version {0}.", after.Version);
                return;
            }

            if (gateway.Updates != 1)
                throw new BrowserFailure("SuppressUpdate");
            Assert.Equal(StatusCodes.Status200OK, status);
            Assert.Equal(1, observer.Posts);
            Assert.True(observer.HeaderMatches(renderedToken), "MVC request token header differs from Razor.");
            Assert.True(observer.FormMatches(renderedToken), "MVC form token member differs from Razor.");
            Assert.Equal(1, gateway.Details);
            Assert.Equal(before.Version + 1, after.Version);
            Assert.Equal(1, store.Writes(scope, id) - writesBefore);
            Assert.Equal(1, store.ReplaceCalls(scope, id) - attemptsBefore);
            if (lossless)
            {
                var expected = JsonNode.Parse(JsonSerializer.Serialize(before.Intake))!.AsObject();
                expected["Status"] = JsonSerializer.SerializeToNode(Diten.PvgService.Domain.RegPvBase.PvgIntakeStatus.IntakeUpdated);
                if (editField is not null) expected[editField] = editField == "EventOnsetDate" && editValue == "" ? null : editValue;
                Assert.True(JsonNode.DeepEquals(expected, JsonSerializer.SerializeToNode(after.Intake)),
                    "The actual stored record differs from the first stored GET baseline beyond the deliberate edit/status.");
                Assert.Equal(before.Intake.ReceivedAtUtc.UtcTicks, after.Intake.ReceivedAtUtc.UtcTicks);
                Assert.Equal(before.Intake.AwarenessAtUtc.UtcTicks, after.Intake.AwarenessAtUtc.UtcTicks);
                var reread = await ReadConnectedDraftAsync(apiClient, jwt, id);
                var expectedRead = JsonNode.Parse(firstRead.GetRawText())!.AsObject();
                expectedRead["status"] = "IntakeUpdated";
                expectedRead["version"] = before.Version + 1;
                if (editField is not null) expectedRead[char.ToLowerInvariant(editField[0]) + editField[1..]] = editField == "EventOnsetDate" && editValue == "" ? null : editValue;
                Assert.True(JsonNode.DeepEquals(expectedRead, JsonNode.Parse(reread.GetRawText())), "Actual API reread lost a stored field.");
                output.WriteLine("Connected case {0}: actual create/firstGET/Razor/productionJS/orderedFormData/MVC/API/InMemory/reread equality; update 1; all 29 fields; tokens 4.", caseName);
                WriteReceipt(new { kind = receiptKind, @case = caseName, editField, editValueHash = ReceiptHash(editValue), emptyProducts, id,
                    status, mvcPosts = observer.Posts, apiDetails = gateway.Details, apiUpdates = gateway.Updates,
                    storeWrites = store.Writes(scope, id) - writesBefore, storeReplaceCalls = store.ReplaceCalls(scope, id) - attemptsBefore,
                    createPayloadHash = ReceiptHash(createJson), firstGetHash = ReceiptHash(firstRead), rereadHash = ReceiptHash(reread),
                    beforeHash = ReceiptHash(before), afterHash = ReceiptHash(after), expectedAfterHash = ReceiptHash(expected),
                    storedFullEquality = true, apiFullEquality = true, fourTokenCarrierAssertions = true });
                return;
            }
            Assert.Equal("browser-edited-synthetic", after.Intake.AdverseEventNarrative);
            Assert.Equal(before.Intake.ProductLines.Single().ProductNameAsReported, after.Intake.ProductLines.Single().ProductNameAsReported);
            output.WriteLine("API detail reads {0}, updates {1}, stored version {2} -> {3}.",
                gateway.Details, gateway.Updates, before.Version, after.Version);
            // Observe the current behavior; the later lossless feature owns the equality assertions.
            output.WriteLine("Untouched reporter contact preserved: {0}. Untouched evidence list preserved: {1}.",
                before.Intake.ReporterContactSummary == after.Intake.ReporterContactSummary,
                before.Intake.EvidenceLinkReferences.SequenceEqual(after.Intake.EvidenceLinkReferences));
        }
        catch (BrowserFailure failure)
        {
            // An unavailable browser/bridge must fail the same save test before changing the seeded draft.
            Assert.Equal(0, gateway.Updates);
                Assert.Equal(0, store.Writes(scope, id) - writesBefore);
                Assert.Equal(0, store.ReplaceCalls(scope, id) - attemptsBefore);
            var unchanged = Assert.IsType<PvgPersistedIntakeDraft>(await store.FindByIdAsync(scope, id));
            Assert.Equal(before.Version, unchanged.Version);
            output.WriteLine("Child failure {0}, API updates 0, stored version {1}.", failure.Message, unchanged.Version);
            throw;
        }
    }

    private static async Task<string> SeedDraftAsync(HttpClient api, string jwt, string? createJson = null)
    {
        using var create = new HttpRequestMessage(HttpMethod.Post, ApiPrefix);
        create.Headers.Authorization = new("Bearer", jwt);
        create.Headers.Add("X-Tenant-Id", TenantId);
        create.Headers.Add("X-Correlation-Id", "synthetic-prep-web");
        create.Content = JsonContent.Create(new
        {
            intakeChannel = "Portal", sourceType = "Spontaneous", sourceReference = "synthetic-reference",
            receivedAtUtc = "2020-03-01T08:00:00Z", awarenessAtUtc = "2020-03-01T08:00:00Z",
            reporterType = "Consumer", reporterContactSummary = "synthetic-contact", patientSubjectCode = "synthetic-subject",
            eventOnsetDate = "2020-02-01", adverseEventNarrative = "seeded-synthetic", seriousness = "Serious",
            intakePriority = "Routine", evidenceLinkReferences = new[] { "synthetic-evidence" },
            productLines = new[] { new { productRole = "Suspect", productNameAsReported = "synthetic-product", lotNumber = "synthetic-lot" } },
            eventCountry = "unknown", reportType = "Spontaneous", reporterNameOrInitials = "S.Y.",
            reporterProfession = "synthetic-profession", reporterCountry = "unknown"
        });
        if (createJson is not null) create.Content = new StringContent(createJson, Encoding.UTF8, "application/json");
        using var result = await api.SendAsync(create);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var body = await result.Content.ReadFromJsonAsync<JsonElement>();
        return Assert.IsType<string>(body.GetProperty("intakeDraftId").GetString());
    }

    private static async Task<JsonElement> ReadConnectedDraftAsync(HttpClient api, string jwt, string id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiPrefix + "/" + id);
        request.Headers.Authorization = new("Bearer", jwt);
        request.Headers.Add("X-Tenant-Id", TenantId);
        request.Headers.Add("X-Correlation-Id", "synthetic-fe-read");
        using var response = await api.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var draft = doc.RootElement.GetProperty("draft");
        Assert.Equal(DetailMembers.Order(StringComparer.Ordinal), draft.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        foreach (var member in NullableDetailMembers) Assert.True(draft.TryGetProperty(member, out _));
        foreach (var line in draft.GetProperty("productLines").EnumerateArray())
            Assert.Equal(ProductMembers.Order(StringComparer.Ordinal), line.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        return draft.Clone();
    }

    // Pass-through instrumentation: actual production InMemory store performs every operation. Counters are
    // keyed by tenant/id, never reset, and distinguish Replace attempts from successful writes after the first GET.
    internal sealed class ObservedStore : IPvgIntakeDraftStore
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<(string Tenant, string Id), int> _writes = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<(string Tenant, string Id), int> _replaces = new();
        public InMemoryPvgIntakeDraftRepository Inner { get; } = new();
        public int Writes(PvgPersistenceTenantScope scope, string id) => _writes.GetValueOrDefault((scope.TenantId, id));
        public int ReplaceCalls(PvgPersistenceTenantScope scope, string id) => _replaces.GetValueOrDefault((scope.TenantId, id));
        public async ValueTask<string> AddAsync(PvgPersistenceTenantScope scope, string actor, SafetyCaseIntake intake, CancellationToken ct = default)
        {
            var id = await Inner.AddAsync(scope, actor, intake, ct);
            _writes.AddOrUpdate((scope.TenantId, id), 1, (_, n) => n + 1);
            return id;
        }
        public async ValueTask<PvgIntakeDraftReplaceResult> ReplaceAsync(PvgPersistenceTenantScope scope, string id,
            long? version, string actor, SafetyCaseIntake intake, CancellationToken ct = default)
        {
            _replaces.AddOrUpdate((scope.TenantId, id), 1, (_, n) => n + 1);
            var result = await Inner.ReplaceAsync(scope, id, version, actor, intake, ct);
            if (result.Outcome == PvgIntakeDraftReplaceOutcome.Replaced)
                _writes.AddOrUpdate((scope.TenantId, id), 1, (_, n) => n + 1);
            return result;
        }
        public ValueTask<PvgPersistedIntakeDraft?> FindByIdAsync(PvgPersistenceTenantScope scope, string id, CancellationToken ct = default) =>
            Inner.FindByIdAsync(scope, id, ct);
        public ValueTask<IReadOnlyList<PvgPersistedIntakeDraft>> ListAsync(PvgPersistenceListScope scope, CancellationToken ct = default) =>
            Inner.ListAsync(scope, ct);
        public ValueTask<int> CountAsync(PvgPersistenceTenantScope scope, PvgIntakeStatus? status, CancellationToken ct = default) =>
            Inner.CountAsync(scope, status, ct);
    }

    internal interface IUpdateTraffic
    {
        int Details { get; }
        int Updates { get; }
    }

    internal interface ITokenTraffic
    {
        int Posts { get; }
        bool HeaderMatches(string expected);
        bool FormMatches(string expected);
    }

    // xUnit owns this fixture for the class lifetime; a lazy host is disposed only after every awaited row/child.
    // No current-id switch, shared counter reset, shared client/cookie or mutable permission gate exists.
    public sealed class ConnectedMatrixFixture : IDisposable
    {
        private readonly Lazy<MatrixHost> _host = new(() => new MatrixHost());
        internal MatrixHost Host => _host.Value;
        public void Dispose()
        {
            if (_host.IsValueCreated) _host.Value.Dispose();
        }
    }

    internal sealed class MatrixRow(string id, Action release) : IUpdateTraffic, ITokenTraffic, IDisposable
    {
        private int _details, _updates, _posts;
        private string[] _headers = [], _form = [];
        public string Id { get; } = id;
        public int Details => Volatile.Read(ref _details);
        public int Updates => Volatile.Read(ref _updates);
        public int Posts => Volatile.Read(ref _posts);
        public void Detail() => Interlocked.Increment(ref _details);
        public void Update() => Interlocked.Increment(ref _updates);
        public void Observe(IHeaderDictionary headers, IFormCollection form)
        {
            _headers = headers[TokenHeader].OfType<string>().ToArray();
            _form = form[TokenMember].OfType<string>().ToArray();
            Interlocked.Increment(ref _posts);
        }
        public bool HeaderMatches(string expected) => _headers.Length == 1 && _headers[0] == expected;
        public bool FormMatches(string expected) => _form.Length == 1 && _form[0] == expected;
        public void Dispose()
        {
            release();
            _headers = [];
            _form = [];
        }
    }

    internal sealed class MatrixHost : IDisposable
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, MatrixRow> _rows = new(StringComparer.Ordinal);
        private readonly WebApplicationFactory<Program> _base = new();
        private readonly MatrixGateway _gateway;
        private readonly object _clientLock = new();
        public string SigningKey { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        public TestServer Api { get; }
        public WebApplicationFactory<Program> Web { get; }

        public MatrixHost()
        {
            var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = SigningKey, ["JwtSettings:Issuer"] = "pvg-web-test",
                ["JwtSettings:Audience"] = "pvg-web-test", [PvgApiHost.PersistenceStoreKey] = "InMemory"
            }).Build();
            var ports = new ConnectedPorts();
            Api = new TestServer(PvgApiHost.CreatePvgServiceWebHostBuilder("Development", settings, "http://127.0.0.1:0", services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IPvgPermissionGate>(ports));
                services.Replace(ServiceDescriptor.Singleton<IPvgFieldSecurityPolicy>(ports));
                services.Replace(ServiceDescriptor.Singleton<IPvgWorkflowTransitionGate>(ports));
                services.Replace(ServiceDescriptor.Singleton<IPvgEvidenceLinkPort>(ports));
                services.Replace(ServiceDescriptor.Singleton<IPvgIntakeDraftStore>(new ObservedStore()));
            }));
            _gateway = new MatrixGateway(Api.CreateHandler(), _rows);
            Web = _base.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("JwtSettings:Secret", SigningKey);
                builder.UseSetting("JwtSettings:Issuer", "pvg-web-test");
                builder.UseSetting("JwtSettings:Audience", "pvg-web-test");
                builder.UseSetting("GatewayUrl", "http://127.0.0.1:9");
                builder.UseSetting("PlatformServiceUrl", "http://127.0.0.1:9");
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IStartupFilter>(new MatrixTokenObserver(_rows));
                    services.AddTransient(_ => new HttpClient(_gateway, disposeHandler: false));
                });
            });
            _ = Web.Server; // Publish the immutable actual server before concurrent per-row client creation.
        }

        public HttpClient CreateClient()
        {
            // Factory bookkeeping is serialized briefly; actual Razor/MVC/browser requests run concurrently.
            lock (_clientLock)
                return Web.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });
        }

        public MatrixRow Register(string id)
        {
            var row = new MatrixRow(id, () => Assert.True(_rows.TryRemove(id, out _), "Matrix row ownership missing."));
            Assert.True(_rows.TryAdd(id, row), "A fresh API id must not be registered twice.");
            return row;
        }

        public void Dispose()
        {
            try { Assert.Empty(_rows); }
            finally
            {
                Web.Dispose();
                _base.Dispose();
                _gateway.Dispose();
                Api.Dispose();
            }
        }
    }

    private sealed class MatrixGateway(HttpMessageHandler api,
        System.Collections.Concurrent.ConcurrentDictionary<string, MatrixRow> rows) : DelegatingHandler(api)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            const string upstream = "/api/pv-case-intake-triage/";
            var path = request.RequestUri!.AbsolutePath;
            if (path == upstream + "options" && request.Method == HttpMethod.Get) { }
            else if (path.StartsWith(upstream, StringComparison.Ordinal) && rows.TryGetValue(path[upstream.Length..], out var row))
            {
                if (request.Method == HttpMethod.Get) row.Detail();
                else if (request.Method == HttpMethod.Put) row.Update();
                else return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
            else return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            request.RequestUri = new Uri("http://localhost" + ApiPrefix + "/" + path[upstream.Length..]);
            return base.SendAsync(request, ct);
        }
    }

    private sealed class MatrixTokenObserver(System.Collections.Concurrent.ConcurrentDictionary<string, MatrixRow> rows) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (ctx, run) =>
            {
                const string prefix = BrowserPrefix + "/api/update/";
                var path = ctx.Request.Path.Value!;
                if (HttpMethods.IsPost(ctx.Request.Method) && path.StartsWith(prefix, StringComparison.Ordinal)
                    && rows.TryGetValue(path[prefix.Length..], out var row))
                    row.Observe(ctx.Request.Headers, await ctx.Request.ReadFormAsync(ctx.RequestAborted));
                await run(ctx);
            });
            next(app);
        };
    }

    private sealed class ConnectedPorts : IPvgPermissionGate, IPvgFieldSecurityPolicy, IPvgWorkflowTransitionGate, IPvgEvidenceLinkPort
    {
        public ValueTask<PvgPermissionDecision> EvaluateAsync(PvgPermissionRequest request, CancellationToken ct) =>
            ValueTask.FromResult(request.TenantId == TenantId && request.ActorId == "7b2c51f0-0000-4000-8000-0000000000a1"
                && request.RequiredPermission is Read or Create or Update
                ? PvgPermissionDecision.Allowed() : PvgPermissionDecision.Denied("PVG_TEST_DENIED"));
        public ValueTask<PvgPortDecision> EvaluateAsync(PvgFieldSecurityRequest request, CancellationToken ct) => Allowed();
        public ValueTask<PvgPortDecision> EvaluateAsync(PvgWorkflowTransitionRequest request, CancellationToken ct) => Allowed();
        public ValueTask<PvgPortDecision> EvaluateAsync(PvgEvidenceLinkRequest request, CancellationToken ct) => Allowed();
        private static ValueTask<PvgPortDecision> Allowed() => ValueTask.FromResult(new PvgPortDecision(true, true, "PVG_TEST_ALLOWED"));
    }

    // The same upstream/downstream route translation as the Gateway. All module responses come from the API.
    private sealed class ConnectedGateway(HttpMessageHandler api, string id) : DelegatingHandler(api), IUpdateTraffic
    {
        public int Details { get; private set; }
        public int Updates { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            const string upstream = "/api/pv-case-intake-triage/";
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path == upstream + id) Details++;
            else if (request.Method == HttpMethod.Put && path == upstream + id) Updates++;
            else if (request.Method != HttpMethod.Get || path != upstream + "options")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            request.RequestUri = new Uri("http://localhost" + ApiPrefix + "/" + path[upstream.Length..]);
            return base.SendAsync(request, ct);
        }
    }

    private sealed class MvcTokenObserver(string updatePath) : IStartupFilter, ITokenTraffic
    {
        private string[] header = [];
        private string[] form = [];
        public int Posts { get; private set; }
        public bool HeaderMatches(string expected) => header.Length == 1 && header[0] == expected;
        public bool FormMatches(string expected) => form.Length == 1 && form[0] == expected;

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (ctx, run) =>
            {
                if (HttpMethods.IsPost(ctx.Request.Method) && ctx.Request.Path == updatePath)
                {
                    Posts++;
                    header = ctx.Request.Headers[TokenHeader].OfType<string>().ToArray();
                    var submitted = await ctx.Request.ReadFormAsync(ctx.RequestAborted);
                    form = submitted[TokenMember].OfType<string>().ToArray();
                }
                await run(ctx);
            });
            next(app);
        };
    }

    private async Task<int> SubmitInBrowserAsync(
        string webRoot, string html, string id, string fault, HttpClient mvc, string cookie, string renderedToken,
        bool lossless = false, string? editField = null, string? editValue = null, string? shapeCase = null, JsonElement firstRead = default)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var ct = timeout.Token;
        var start = new ProcessStartInfo
        {
            FileName = fault == "MissingNode" ? Path.Combine(webRoot, "unavailable-node-executable") : "node",
            WorkingDirectory = fault == "MissingJsdom" ? AppContext.BaseDirectory : webRoot,
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add("--no-global-search-paths");
        start.ArgumentList.Add("-e");
        start.ArgumentList.Add(BrowserProgram);
        start.ArgumentList.Add(fault);
        using var child = new Process { StartInfo = start };
        try { child.Start(); }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2) { throw new BrowserFailure("MissingNode"); }

        // Never echo browser stderr, JWTs, request tokens or form data into test output.
        var drain = child.StandardError.BaseStream.CopyToAsync(Stream.Null);
        try
        {
            var script = Path.Combine(webRoot, "wwwroot", "assets", "js", "Pharmacovigilance", "CaseIntakeTriage", "form.js");
            await ReplyAsync(new { html, script, lossless, editField, editValue, shapeCase });
            var status = 0;
            var gets = 0;
            var posted = false;
            for (var frame = 0; frame < 10; frame++)
            {
                var line = await child.StandardOutput.ReadLineAsync(ct);
                if (line is null)
                {
                    await child.WaitForExitAsync(ct);
                    throw new BrowserFailure(child.ExitCode == 0 ? "EarlyEnd" : "StartFailure");
                }
                if (line.Length > 256 * 1024) throw new BrowserFailure("Malformed");
                using var doc = JsonDocument.Parse(line);
                var message = doc.RootElement;
                switch (message.GetProperty("kind").GetString())
                {
                    case "missing-jsdom": throw new BrowserFailure("MissingJsdom");
                    case "stall":
                        timeout.CancelAfter(TimeSpan.FromMilliseconds(500));
                        continue;
                    case "ready":
                        if (shapeCase is not null)
                        {
                            Assert.True(message.GetProperty("disabled").GetBoolean(), "Incomplete/malformed detail enabled Save: " + shapeCase);
                        }
                        else if (lossless)
                        {
                            Assert.False(message.GetProperty("disabled").GetBoolean(), "Complete accepted detail kept Save disabled.");
                            Assert.True(message.GetProperty("valid").GetBoolean(), "Untouched accepted stored detail failed actual browser validity.");
                            var values = message.GetProperty("values");
                            foreach (var member in new[] { "reporterContactSummary", "sourceReference", "patientSubjectCode", "eventOnsetDate" })
                            {
                                var field = char.ToUpperInvariant(member[0]) + member[1..];
                                var stored = firstRead.GetProperty(member);
                                var display = stored.ValueKind == JsonValueKind.Null ? "" : stored.GetString()!.Replace("\r\n", "\n").Replace("\r", "\n");
                                if (member is "sourceReference" or "patientSubjectCode") display = display.Replace("\n", "");
                                Assert.Equal(display, values.GetProperty(field).GetString());
                            }
                            Assert.Equal(string.Join("\n", firstRead.GetProperty("evidenceLinkReferences").EnumerateArray().Select(x => x.ValueKind == JsonValueKind.Null ? "" : x.GetString())).Replace("\r\n", "\n").Replace("\r", "\n"),
                                values.GetProperty("EvidenceLinkReferencesRaw").GetString());
                        }
                        await ReplyAsync(new { proceed = true });
                        continue;
                    case "done":
                        child.StandardInput.Close();
                        await child.WaitForExitAsync(ct);
                        if (child.ExitCode != 0 || (shapeCase is null && !posted) || await child.StandardOutput.ReadLineAsync(ct) is not null)
                            throw new BrowserFailure("EarlyEnd");
                        return status;
                    case "fetch": break;
                    default: throw new BrowserFailure("Malformed");
                }

                var path = message.GetProperty("path").GetString();
                var method = message.GetProperty("method").GetString();
                var isPost = method == "POST";
                if (method == "GET" && !posted &&
                    path == (gets == 0 ? $"{BrowserPrefix}/api/options" : $"{BrowserPrefix}/api/detail/{id}") && gets < 2)
                    gets++;
                else if (isPost && !posted && gets == 2 && path == $"{BrowserPrefix}/api/update/{id}")
                    posted = true;
                else throw new BrowserFailure("BridgeFailure");

                using var request = new HttpRequestMessage(isPost ? HttpMethod.Post : HttpMethod.Get, path);
                request.Headers.Add("Cookie", cookie);
                var headers = message.GetProperty("headers");
                foreach (var property in headers.EnumerateObject())
                {
                    if (property.Name is not ("X-Requested-With" or TokenHeader))
                        throw new BrowserFailure("Malformed");
                    request.Headers.Add(property.Name, property.Value.GetString());
                }
                if (isPost)
                {
                    if (!message.GetProperty("receipt").GetBoolean()) throw new BrowserFailure("MissingReceipt");
                    var pairs = message.GetProperty("fields").EnumerateArray()
                        .Select(pair => new KeyValuePair<string, string>(pair[0].GetString()!, pair[1].GetString()!)).ToArray();
                    if (fault is not ("MissingToken" or "InvalidToken"))
                    {
                        Assert.True(headers.TryGetProperty(TokenHeader, out var header) && header.GetString() == renderedToken,
                            "Browser request token header differs from Razor.");
                        Assert.True(pairs.Where(pair => pair.Key == TokenMember).Select(pair => pair.Value).SequenceEqual([renderedToken]),
                            "Browser form token member differs from Razor.");
                    }
                    // Preserve the multipart entries as received, including repeated names, empty values and whitespace.
                    var form = new MultipartFormDataContent();
                    foreach (var pair in pairs) form.Add(new StringContent(pair.Value, Encoding.UTF8), pair.Key);
                    request.Content = form;
                    if (fault == "SuppressUpdate")
                    {
                        await ReplyAsync(new { status = 503, body = "{}" });
                        continue;
                    }
                }
                using var response = await mvc.SendAsync(request, ct);
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                if (shapeCase is not null && method == "GET" && path!.Contains("/detail/", StringComparison.Ordinal))
                    responseBody = MalformedDetail(responseBody, shapeCase);
                if (isPost) status = (int)response.StatusCode;
                await ReplyAsync(new { status = (int)response.StatusCode, body = responseBody });
            }
            throw new BrowserFailure("Malformed");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw new BrowserFailure("Timeout"); }
        catch (JsonException) { throw new BrowserFailure("Malformed"); }
        finally
        {
            if (!child.HasExited) child.Kill(entireProcessTree: true);
            using var reap = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await child.WaitForExitAsync(reap.Token);
            await drain.WaitAsync(reap.Token);
            output.WriteLine("Reaped browser PID {0}.", child.Id);
        }

        async Task ReplyAsync(object reply)
        {
            var json = JsonSerializer.Serialize(reply);
            if (json.Length > 2 * 1024 * 1024) throw new BrowserFailure("Malformed");
            await child.StandardInput.WriteLineAsync(json.AsMemory(), ct);
            await child.StandardInput.FlushAsync(ct);
        }
    }

    private const string BrowserProgram = """
        const { createInterface } = require('node:readline');
        const { readFileSync } = require('node:fs');
        const { setTimeout: delay } = require('node:timers/promises');
        const input = createInterface({ input: process.stdin, crlfDelay: Infinity });
        const lines = input[Symbol.asyncIterator]();
        const send = frame => process.stdout.write(JSON.stringify(frame) + '\n');
        async function read() {
            const line = await lines.next();
            if (line.done) throw Error('end');
            return JSON.parse(line.value);
        }
        async function run() {
            const { html, script, lossless, editField, editValue, shapeCase } = await read();
            const fault = process.argv[1];
            if (fault === 'StartFailure') process.exit(17);
            if (fault === 'EarlyEnd') return;
            if (fault === 'Malformed') { process.stdout.write('invalid\n'); return; }
            if (fault === 'Timeout') { send({ kind: 'stall' }); await read(); return; }
            if (process.versions.node.split('.')[0] !== '22') throw Error('runtime version');
            let JSDOM, VirtualConsole;
            try {
                ({ JSDOM, VirtualConsole } = require('jsdom'));
            } catch (error) {
                if (error.code !== 'MODULE_NOT_FOUND') throw error;
                send({ kind: 'missing-jsdom' });
                return;
            }
            const dom = new JSDOM(html, { url: 'http://localhost/', runScripts: 'outside-only', virtualConsole: new VirtualConsole() });
            try {
                const w = dom.window;
                const form = w.document.querySelector('#pvg-case-intake-form');
                const save = w.document.querySelector('button[type="submit"][form="pvg-case-intake-form"]');
                const version = form.elements.namedItem('ExpectedVersion');
                let detailRead = false, submitted = false, event;
                w.fetch = async (path, options) => {
                    const method = options.method || 'GET';
                    const isPost = method === 'POST';
                    const receipt = fault !== 'MissingReceipt' && detailRead && !!version.value &&
                        event?.defaultPrevented === true && options.body instanceof w.FormData;
                    send({ kind: 'fetch', path: fault === 'BridgeFailure' ? '/invalid-bridge-path' : path,
                        method, headers: options.headers, fields: isPost ? Array.from(options.body.entries()) : [], receipt });
                    const { status, body } = await read();
                    if (isPost) submitted = true;
                    if (!isPost && path.includes('/detail/') && status === 200) detailRead = true;
                    return { status, ok: status >= 200 && status < 300, json: async () => JSON.parse(body) };
                };
                w.eval(readFileSync(script, 'utf8'));
                if (lossless) {
                    while (!detailRead) await delay(5);
                    await delay(30);
                    send({ kind: 'ready', disabled: save.disabled, valid: form.checkValidity(),
                        values: Object.fromEntries(Array.from(form.elements).filter(e => e.id).map(e => [e.id, e.value])) });
                    await read();
                } else while (save.disabled) await delay(5);
                // Simulated user edits in the actual controls. Current detail/prefill omits required fields.
                // The transport does not fill in or normalize any field after production constructs FormData.
                const edits = {
                    IntakeChannel: 'Portal', SourceType: 'Spontaneous', SourceReference: 'edited-reference',
                    ReceivedAtUtc: '2020-03-01T08:00', ReporterType: 'Consumer', PatientSubjectCode: 'edited-subject',
                    Seriousness: 'Serious', IntakePriority: 'Routine', AdverseEventNarrative: 'browser-edited-synthetic'
                };
                if (!lossless || shapeCase) {
                    for (const [name, value] of Object.entries(edits)) form.elements.namedItem(name).value = value;
                    if (shapeCase) {
                        form.elements.namedItem('AwarenessAtUtc').value = '2020-03-01T08:00';
                        form.elements.namedItem('ReportType').value = 'Spontaneous';
                        form.elements.namedItem('EventCountry').value = 'unknown';
                        for (const row of form.querySelectorAll('[data-product-line]')) {
                            row.querySelector('[name$=".ProductRole"]').value = 'Suspect';
                            row.querySelector('[name$=".ProductNameAsReported"]').value = 'synthetic-product';
                        }
                    }
                }
                if (editField) {
                    const control = form.elements.namedItem(editField);
                    control.value = editValue;
                    control.dispatchEvent(new w.Event('input', { bubbles: true }));
                }
                const token = form.elements.namedItem('__RequestVerificationToken');
                if (fault === 'MissingToken') token.remove();
                if (fault === 'InvalidToken') token.value = 'invalid-synthetic-token';
                if (!form.checkValidity()) throw Error('form invalid');
                event = new w.Event('submit', { cancelable: true, bubbles: true });
                form.dispatchEvent(event);
                if (shapeCase) {
                    await delay(50);
                    if (submitted) throw Error('invalid detail posted');
                } else while (!submitted || save.getAttribute('aria-busy') === 'true') await delay(5);
                send({ kind: 'done' });
            } finally { dom.window.close(); }
        }
        run().catch(() => { process.exitCode = 19; }).finally(() => { input.close(); process.stdin.destroy(); });
        """;

    private WebApplicationFactory<Program> HostOver(HttpMessageHandler wire, string signingKey, IStartupFilter? observer = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("JwtSettings:Secret", signingKey);
            builder.UseSetting("JwtSettings:Issuer", "pvg-web-test");
            builder.UseSetting("JwtSettings:Audience", "pvg-web-test");
            builder.UseSetting("GatewayUrl", "http://127.0.0.1:9");
            builder.UseSetting("PlatformServiceUrl", "http://127.0.0.1:9");
            builder.ConfigureTestServices(services =>
            {
                if (observer is not null) services.AddSingleton(observer);
                services.AddTransient(_ => new HttpClient(wire, disposeHandler: false));
            });
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

    // Legacy direct-action fixtures use the current Update representation. Reflection keeps the SAME source
    // compilable on the old product for assertion RED. Connected tests never call this fixture converter.
    private static Task<IActionResult> UpdateDraftAsync(PharmacovigilanceCaseIntakeTriageController controller,
        string id, PharmacovigilanceCaseIntakeTriageController.CaseIntakeFormInput input, CancellationToken ct)
    {
        var method = typeof(PharmacovigilanceCaseIntakeTriageController).GetMethod(nameof(PharmacovigilanceCaseIntakeTriageController.UpdateDraft))!;
        object bound = method.GetParameters()[1].ParameterType == typeof(PharmacovigilanceCaseIntakeTriageController.CaseIntakeFormInput)
            ? input : new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                ["UpdateValuesJson"] = UpdateFixtureJson(input)
            });
        return (Task<IActionResult>)method.Invoke(controller, [id, bound, ct])!;
    }

    private static string UpdateFixtureJson(PharmacovigilanceCaseIntakeTriageController.CaseIntakeFormInput input)
    {
        // Pre-existing normalized synthetic fixtures only: the fresh lossless browser cases independently
        // forward actual FormData and assert raw stored equality without this shared Create converter.
        var values = input.ToPayload(isUpdate: true).ToDictionary(pair => pair.Key, pair => pair.Value);
        if (!input.HasReadableDates())
        {
            values["ReceivedAtUtc"] = input.ReceivedAtUtc;
            values["AwarenessAtUtc"] = input.AwarenessAtUtc;
        }
        return JsonSerializer.Serialize(values);
    }

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
