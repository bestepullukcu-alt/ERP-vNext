using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// MOD-0280-FU01 T2b — the web tier's new routes. A decision on a week is forwarded to the Task Center's OWN action
/// path (<c>api/v1/work-items/{approvalTaskId}/actions/{approve|reject}</c>, provider <c>workflow</c>, reason as
/// <c>comment</c>) — never a second approve route. Bulk approve re-reads the caller's queue and approves only weeks
/// that are there and carry no mark. The list proxies name their own endpoint and pass status and body through.
/// </summary>
public sealed class TimeEntryT2bRouteTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Gateway = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly WebApplicationFactory<Program> _factory;

    public TimeEntryT2bRouteTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task A_return_goes_to_the_Task_Centers_work_item_action_with_the_reason_as_comment()
    {
        var gateway = new RoutingGateway();
        var taskId = Guid.NewGuid();
        var controller = ControllerWith(gateway);

        var result = Assert.IsType<ContentResult>(await controller.Decide("reject",
            new TimeEntryController.ApprovalDecisionRequest(taskId, 4, "Tuesday is missing")));

        Assert.Equal(200, result.StatusCode);
        var call = Assert.Single(gateway.Calls);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal($"{Gateway}/api/v1/work-items/{taskId:D}/actions/reject", call.Uri);
        using var body = JsonDocument.Parse(call.Body!);
        Assert.Equal("workflow", body.RootElement.GetProperty("providerCode").GetString());
        Assert.Equal(4, body.RootElement.GetProperty("payload").GetProperty("expectedVersion").GetInt32());
        Assert.Equal("Tuesday is missing", body.RootElement.GetProperty("payload").GetProperty("comment").GetString());
    }

    [Fact]
    public async Task An_approval_carries_no_comment_and_MOD_0023s_refusal_comes_back_verbatim()
    {
        const string refusal = """{"isSuccessful":false,"statusCode":409,"reason_code":"WORKFLOW_TASK_ALREADY_DECIDED"}""";
        var gateway = new RoutingGateway { Answer = (_, _) => (HttpStatusCode.Conflict, refusal) };
        var taskId = Guid.NewGuid();

        var result = Assert.IsType<ContentResult>(await ControllerWith(gateway).Decide("approve",
            new TimeEntryController.ApprovalDecisionRequest(taskId, 2, "ignored for an approval")));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(refusal, result.Content);
        using var body = JsonDocument.Parse(gateway.Calls.Single().Body!);
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("payload").GetProperty("comment").ValueKind);
    }

    [Theory]
    [InlineData("delegate")]
    [InlineData("requestInfo")]
    [InlineData("edit")]
    public async Task Only_approve_and_reject_are_forwarded(string decision)
    {
        var gateway = new RoutingGateway();

        var result = await ControllerWith(gateway).Decide(decision, new TimeEntryController.ApprovalDecisionRequest(Guid.NewGuid(), 1, "x"));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(gateway.Calls);
    }

    [Fact]
    public async Task Bulk_approve_approves_only_the_callers_UNMARKED_weeks_one_by_one_on_the_same_path()
    {
        var clean = Guid.NewGuid();
        var flagged = Guid.NewGuid();
        var holiday = Guid.NewGuid();
        var outside = Guid.NewGuid();
        var notMine = Guid.NewGuid();
        var cleanTask = Guid.NewGuid();
        object Row(Guid week, Guid? task, int version, string[] flagged, string[] holidays, int outside) => new
        {
            weekId = week, approvalTaskId = task, approvalTaskVersion = version,
            flaggedDates = flagged, autoClosedDates = Array.Empty<string>(), holidayDates = holidays, outsideWorkingMinutes = outside
        };
        var list = JsonSerializer.Serialize(new
        {
            data = new
            {
                items = new[]
                {
                    Row(clean, cleanTask, 3, [], [], 0),
                    Row(flagged, Guid.NewGuid(), 1, ["2026-10-05"], [], 0),
                    Row(holiday, Guid.NewGuid(), 1, [], ["2026-10-06"], 0),
                    Row(outside, Guid.NewGuid(), 1, [], [], 45)
                },
                total = 4,
                filteredTotal = 4
            }
        });
        var gateway = new RoutingGateway
        {
            Answer = (method, uri) => method == HttpMethod.Get ? (HttpStatusCode.OK, list) : (HttpStatusCode.OK, "{}")
        };

        var result = Assert.IsType<OkObjectResult>(await ControllerWith(gateway).BulkApprove(
            new TimeEntryController.BulkApprovalRequest([clean, flagged, holiday, outside, notMine])));

        // BL-484 — the re-read names exactly the selected weeks (no page window a long queue could overflow).
        Assert.Equal(
            $"{Gateway}/api/v1/time-entry/approvals?start=0&length=5&weekIds={clean:D},{flagged:D},{holiday:D},{outside:D},{notMine:D}",
            gateway.Calls[0].Uri);
        var decisions = gateway.Calls.Skip(1).ToList();
        var only = Assert.Single(decisions);                                         // ONE approve: the clean week
        Assert.Equal($"{Gateway}/api/v1/work-items/{cleanTask:D}/actions/approve", only.Uri);
        var json = JsonSerializer.Serialize(result.Value);
        Assert.Contains(clean.ToString(), json);
        Assert.Contains("TIMESHEET_BULK_MARKED", json);
        Assert.Contains("TIMESHEET_APPROVAL_NOT_FOUND", json);                        // not in the caller's queue
    }

    [Fact]
    public async Task Bulk_approve_reads_back_exactly_the_selected_weeks_so_a_queue_longer_than_500_refuses_nothing_valid()
    {
        // BL-484 — a caller's queue of 600 weeks, answered the way Platform answers: weekIds narrows the queue, then the
        // page window applies. The selection sits beyond the first 500.
        var queue = Enumerable.Range(0, 600).Select(i => new QueueWeek(Guid.NewGuid(), Guid.NewGuid(), Flagged: i == 560)).ToList();
        var beyond = queue[550];
        var markedBeyond = queue[560];
        var notMine = Guid.NewGuid();
        var gateway = new RoutingGateway { Answer = PlatformOver(queue) };

        var result = Assert.IsType<OkObjectResult>(await ControllerWith(gateway).BulkApprove(
            new TimeEntryController.BulkApprovalRequest([beyond.WeekId, markedBeyond.WeekId, notMine])));

        using var outcome = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        var data = outcome.RootElement.GetProperty("data");
        Assert.Equal([beyond.WeekId], data.GetProperty("approved").EnumerateArray().Select(e => e.GetGuid()).ToList());
        var skipped = data.GetProperty("skipped").EnumerateArray()
            .ToDictionary(e => e.GetProperty("weekId").GetGuid(), e => e.GetProperty("reasonCode").GetString());
        Assert.Equal("TIMESHEET_BULK_MARKED", skipped[markedBeyond.WeekId]);            // a marked week is still refused
        Assert.Equal("TIMESHEET_APPROVAL_NOT_FOUND", skipped[notMine]);                  // not in the caller's queue
        Assert.Equal(2, skipped.Count);
        var decision = Assert.Single(gateway.Calls.Where(c => c.Method == HttpMethod.Post));
        Assert.Equal($"{Gateway}/api/v1/work-items/{beyond.TaskId:D}/actions/approve", decision.Uri);
        Assert.Equal(
            $"{Gateway}/api/v1/time-entry/approvals?start=0&length=3&weekIds={beyond.WeekId:D},{markedBeyond.WeekId:D},{notMine:D}",
            Assert.Single(gateway.Calls.Where(c => c.Method == HttpMethod.Get)).Uri);
    }

    [Fact]
    public async Task The_bulk_re_read_carries_nothing_of_the_bulk_requests_own_query_string()
    {
        var week = Guid.NewGuid();
        var gateway = new RoutingGateway { Answer = (_, _) => (HttpStatusCode.OK, """{"data":{"items":[],"total":0,"filteredTotal":0}}""") };

        await ControllerWith(gateway, query: $"?weekIds={Guid.NewGuid():D}&length=500").BulkApprove(
            new TimeEntryController.BulkApprovalRequest([week]));

        Assert.Equal($"{Gateway}/api/v1/time-entry/approvals?start=0&length=1&weekIds={week:D}", Assert.Single(gateway.Calls).Uri);
    }

    [Fact]
    public async Task The_approvals_list_proxy_forwards_the_server_mode_query_whole()
    {
        var gateway = new RoutingGateway { Answer = (_, _) => (HttpStatusCode.OK, """{"data":{"items":[],"total":0,"filteredTotal":0}}""") };
        var controller = ControllerWith(gateway, query: "?start=0&length=25&search=Ay%C5%9Fe&orderBy=displayName&orderDir=asc&draw=1");

        await controller.ApprovalsListApi();

        Assert.Equal($"{Gateway}/api/v1/time-entry/approvals?start=0&length=25&search=Ay%C5%9Fe&orderBy=displayName&orderDir=asc&draw=1",
            gateway.Calls.Single().Uri);
    }

    [Fact]
    public async Task The_categories_proxy_reaches_only_the_category_routes()
    {
        var gateway = new RoutingGateway();

        await ControllerWith(gateway).CategoriesApi("manage");
        await ControllerWith(gateway).CategoriesApi("../settings");

        Assert.Equal($"{Gateway}/api/v1/time-entry/categories/manage", Assert.Single(gateway.Calls).Uri);
    }

    [Fact]
    public async Task The_settings_lookups_read_the_existing_platform_and_MDM_readers()
    {
        var gateway = new RoutingGateway();

        await ControllerWith(gateway).PositionsLookup();
        await ControllerWith(gateway).LegalEntitiesLookup();

        Assert.Equal([$"{Gateway}/api/platform/positions", $"{Gateway}/api/legal-entities/lookup"], gateway.Calls.Select(c => c.Uri).ToList());
        Assert.All(gateway.Calls, c => Assert.Equal(HttpMethod.Get, c.Method));
    }

    [Theory]
    [InlineData("/TimeEntry/Approvals")]
    [InlineData("/TimeEntry/Approvals/5b1b4a2e-0c3d-4e5f-8a9b-0c1d2e3f4a5b")]
    [InlineData("/TimeEntry/Categories")]
    [InlineData("/TimeEntry/Settings")]
    [InlineData("/TimeEntry/Approvals/api")]
    [InlineData("/TimeEntry/Categories/api/manage")]
    public async Task The_new_routes_are_served_by_the_web_tier(string url)
    {
        using var client = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("login", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static TimeEntryController ControllerWith(RoutingGateway gateway, string? query = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway })
            .Build();
        var controller = new TimeEntryController(new SingleClientFactory(gateway), configuration, NullLogger<TimeEntryController>.Instance);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        if (query is not null)
        {
            httpContext.Request.QueryString = new QueryString(query);
        }

        var jwt = new JwtSecurityToken(
            claims: [new Claim("sub", Guid.NewGuid().ToString()), new Claim("tenant_id", TenantId.ToString())],
            expires: DateTime.UtcNow.AddMinutes(10));
        httpContext.Request.Headers.Cookie = $"access_token={new JwtSecurityTokenHandler().WriteToken(jwt)}";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private sealed record Call(HttpMethod Method, string Uri, string? Body);

    private sealed record QueueWeek(Guid WeekId, Guid TaskId, bool Flagged);

    /// <summary>Platform's approvals read over <paramref name="queue"/> (in queue order), as its contract says: <c>weekIds</c>
    /// narrows the caller's queue to the listed weeks, then <c>start</c>/<c>length</c> page what is left. Any POST (the
    /// work-item action) succeeds.</summary>
    private static Func<HttpMethod, string, (HttpStatusCode, string)> PlatformOver(IReadOnlyList<QueueWeek> queue) => (method, uri) =>
    {
        if (method != HttpMethod.Get)
        {
            return (HttpStatusCode.OK, "{}");
        }

        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(uri).Query);
        IEnumerable<QueueWeek> rows = queue;
        if (query.TryGetValue("weekIds", out var ids))
        {
            var listed = ids.SelectMany(v => v!.Split(',')).Select(Guid.Parse).ToHashSet();
            rows = rows.Where(w => listed.Contains(w.WeekId));
        }

        var page = rows.Skip(int.Parse(query["start"]!, System.Globalization.CultureInfo.InvariantCulture))
            .Take(int.Parse(query["length"]!, System.Globalization.CultureInfo.InvariantCulture))
            .Select(w => new
            {
                weekId = w.WeekId, approvalTaskId = w.TaskId, approvalTaskVersion = 1,
                flaggedDates = w.Flagged ? new[] { "2026-10-05" } : Array.Empty<string>(),
                autoClosedDates = Array.Empty<string>(), holidayDates = Array.Empty<string>(), outsideWorkingMinutes = 0
            })
            .ToList();
        return (HttpStatusCode.OK, JsonSerializer.Serialize(new { data = new { items = page, total = queue.Count, filteredTotal = page.Count } }));
    };

    private sealed class RoutingGateway : HttpMessageHandler
    {
        public List<Call> Calls { get; } = [];

        public Func<HttpMethod, string, (HttpStatusCode Status, string Body)> Answer { get; set; } = (_, _) => (HttpStatusCode.OK, "{}");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.OriginalString;
            Calls.Add(new Call(request.Method, uri, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            var (status, body) = Answer(request.Method, uri);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}

/// <summary>MOD-0280-FU01 T2b — the new pages' UAS-001 gates, layouts and words.</summary>
public sealed class TimeEntryT2bViewContractTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Theory]
    [InlineData("Approvals/Index.cshtml", "time-entry.approvals.read")]
    [InlineData("Approvals/Details.cshtml", "time-entry.approvals.read")]
    [InlineData("Categories/Index.cshtml", "time-entry.categories.manage")]
    [InlineData("Settings/Index.cshtml", "time-entry.settings.manage")]
    public void Each_page_draws_only_the_explanation_without_its_key(string view, string key)
    {
        var source = File.ReadAllText(SourcePath("Views", "TimeEntry", view));
        Assert.Contains("Layout = \"_LayoutTenantShell\";", source);
        var gate = source.IndexOf($"@if (!Perms.Has(\"{key}\"))", StringComparison.Ordinal);
        Assert.True(gate > 0, $"{view}: the UAS-001 gate on {key} is missing");
        Assert.Contains("<partial name=\"_AccessDenied\"", source[gate..(gate + 220)]);
        Assert.DoesNotContain("Redirect", source);
        // Nothing of the page is drawn before the gate (the first element after @{ } is the gate).
        var firstDiv = source.IndexOf("<div", StringComparison.Ordinal);
        Assert.True(firstDiv < 0 || firstDiv > gate, $"{view}: markup is drawn before the gate");
    }

    [Theory]
    [InlineData("Approvals", "ApprovalsIndex", "Approvals/Index.cshtml", "Approvals/_IndexL10n.cshtml", "Approvals/Details.cshtml", "Approvals/_DataTable.cshtml")]
    [InlineData("Categories", "CategoriesIndex", "Categories/Index.cshtml", "Categories/_IndexL10n.cshtml", "Categories/_CreateEditOffcanvas.cshtml", "Categories/_DataTable.cshtml")]
    [InlineData("Settings", "SettingsIndex", "Settings/Index.cshtml", "Settings/_IndexL10n.cshtml", null, null)]
    public void Every_localizer_key_of_the_page_exists_in_all_seven_languages(string folder, string resource, params string?[] views)
    {
        var keys = views.Where(v => v is not null)
            .SelectMany(v => Regex.Matches(File.ReadAllText(SourcePath("Views", "TimeEntry", v!)), @"(?<!Shared)Localizer\[""([^""]+)""\]").Select(m => m.Groups[1].Value))
            .Distinct().ToList();
        Assert.NotEmpty(keys);
        foreach (var language in Languages)
        {
            var resx = File.ReadAllText(SourcePath("Resources", "Views", "TimeEntry", folder, $"{resource}.{language}.resx"));
            var missing = keys.Where(k => !resx.Contains($"<data name=\"{k}\"", StringComparison.Ordinal)).ToList();
            Assert.True(missing.Count == 0, $"{folder}/{language} lacks: {string.Join(", ", missing)}");
        }
    }

    [Fact]
    public void The_three_new_menu_pages_have_their_names_in_all_seven_languages()
    {
        foreach (var language in Languages)
        {
            var shared = File.ReadAllText(SourcePath("Resources", $"SharedResource.{language}.resx"));
            foreach (var key in new[] { "Nav.Page.TIMEAPPROVALS", "Nav.Page.TIMECATEGORIES", "Nav.Page.TIMESETTINGS" })
            {
                Assert.Contains($"<data name=\"{key}\"", shared);
            }
        }
    }

    private static string SourcePath(params string[] relativeParts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName, "frontend", "Diten.Web" }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join('/', relativeParts));
    }
}
