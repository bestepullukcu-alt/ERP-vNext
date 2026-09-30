using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
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
/// MOD-0280-FU01 T2a — <c>/TimeEntry/api/{**path}</c> forwards to the gateway's <c>/api/v1/time-entry/{path}</c> and
/// hands the upstream status and body back VERBATIM, success and coded refusal alike: My Timesheet turns
/// <c>TIME_ENTRY_*</c> / <c>TIMESHEET_*</c> codes into sentences in seven languages, and a proxy that re-shaped the body
/// would leave it reading "an error occurred". The query string goes on still encoded; the path can only carry segments.
/// </summary>
public sealed class TimeEntryProxyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Gateway = "http://gateway.test";
    private static readonly Guid TenantId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private readonly WebApplicationFactory<Program> _factory;

    public TimeEntryProxyTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task A_week_read_is_forwarded_to_the_gateway_route_and_returned_verbatim()
    {
        const string week = """{"data":{"weekKey":"2026-W41","status":"Draft","version":3,"entries":[]},"isSuccessful":true,"statusCode":200}""";
        var gateway = new CapturingGateway(HttpStatusCode.OK, week);
        var controller = ControllerWith(gateway, "GET");

        var result = Assert.IsType<ContentResult>(await controller.Api("weeks/2026-W41"));

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(week, result.Content);
        Assert.Equal($"{Gateway}/api/v1/time-entry/weeks/2026-W41", gateway.LastUri);
        Assert.Equal(HttpMethod.Get, gateway.LastMethod);
        Assert.Equal(TenantId.ToString("D"), gateway.LastTenantHeader);
        Assert.StartsWith("Bearer ", gateway.LastAuthorization);
        Assert.Null(gateway.LastBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "TIME_ENTRY_DAY_IMPLAUSIBLE")]
    [InlineData(HttpStatusCode.Conflict, "TIMESHEET_CONCURRENCY_CONFLICT")]
    [InlineData(HttpStatusCode.Forbidden, null)]
    public async Task A_refusal_keeps_its_status_and_body_untouched(HttpStatusCode status, string? code)
    {
        var refusal = code is null
            ? """{"isSuccessful":false,"statusCode":403}"""
            : $$"""{"isSuccessful":false,"statusCode":{{(int)status}},"errors":["x"],"reason_code":"{{code}}"}""";
        var controller = ControllerWith(new CapturingGateway(status, refusal), "PUT", """{"expectedVersion":3,"entries":[]}""");

        var result = Assert.IsType<ContentResult>(await controller.Api("weeks/2026-W41/entries"));

        Assert.Equal((int)status, result.StatusCode);
        Assert.Equal(refusal, result.Content);
    }

    [Fact]
    public async Task A_write_relays_its_body_and_method_unchanged()
    {
        const string body = """{"expectedVersion":3,"entries":[{"localDate":"2026-10-05","taskItemId":null,"categoryCode":"ADMINISTRATION","durationMinutes":90,"note":null,"source":"Manual","sourceRef":null}]}""";
        var gateway = new CapturingGateway(HttpStatusCode.OK, "{}");
        var controller = ControllerWith(gateway, "PUT", body);

        await controller.Api("weeks/2026-W41/entries");

        Assert.Equal(HttpMethod.Put, gateway.LastMethod);
        Assert.Equal(body, gateway.LastBody);
        Assert.Equal($"{Gateway}/api/v1/time-entry/weeks/2026-W41/entries", gateway.LastUri);
    }

    [Fact]
    public async Task The_query_string_goes_on_whole_and_still_encoded()
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, "[]");
        var controller = ControllerWith(gateway, "GET", query: "?search=R%26D%20plan%3F&x=%C3%A7");

        await controller.Api("task-options");

        Assert.Equal($"{Gateway}/api/v1/time-entry/task-options?search=R%26D%20plan%3F&x=%C3%A7", gateway.LastUri);
    }

    [Theory]
    [InlineData("../admin")]
    [InlineData("weeks/../../settings")]
    [InlineData("")]
    public async Task A_path_that_is_not_plain_segments_never_reaches_the_gateway(string path)
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, "{}");
        var controller = ControllerWith(gateway, "GET");

        var result = await controller.Api(path);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(gateway.LastUri);
    }

    [Fact]
    public async Task A_segment_is_escaped_so_nothing_but_a_segment_is_spliced_in()
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, "{}");
        var controller = ControllerWith(gateway, "GET");

        await controller.Api("weeks/2026-W41?x=1#y");

        Assert.Equal($"{Gateway}/api/v1/time-entry/weeks/2026-W41%3Fx%3D1%23y", gateway.LastUri);
    }

    [Fact]
    public async Task Without_a_session_token_nothing_is_sent_upstream()
    {
        var gateway = new CapturingGateway(HttpStatusCode.OK, "{}");
        var controller = ControllerWith(gateway, "GET", withToken: false);

        var result = await controller.Api("timer");

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Null(gateway.LastUri);
    }

    [Theory]
    [InlineData("/TimeEntry")]
    [InlineData("/TimeEntry/api/weeks/2026-W41")]
    public async Task The_routes_are_served_by_the_web_tier(string url)
    {
        using var client = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(url);

        // Anonymous: the [Authorize] challenge answers — which proves the route exists (an unknown one is 404).
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("login", response.Headers.Location?.OriginalString ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static TimeEntryController ControllerWith(
        CapturingGateway gateway, string method, string? body = null, string? query = null, bool withToken = true)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = Gateway })
            .Build();
        var controller = new TimeEntryController(
            new SingleClientFactory(gateway), configuration, NullLogger<TimeEntryController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        if (query is not null)
        {
            httpContext.Request.QueryString = new QueryString(query);
        }

        if (body is not null)
        {
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            httpContext.Request.ContentType = "application/json";
        }

        if (withToken)
        {
            httpContext.Request.Headers.Cookie = $"access_token={Token()}";
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static string Token()
    {
        var jwt = new JwtSecurityToken(
            claims: [new Claim("sub", Guid.NewGuid().ToString()), new Claim("tenant_id", TenantId.ToString())],
            expires: DateTime.UtcNow.AddMinutes(10));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private sealed class CapturingGateway(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? LastUri { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public string? LastTenantHeader { get; private set; }
        public string LastAuthorization { get; private set; } = string.Empty;
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri?.OriginalString;
            LastMethod = request.Method;
            LastTenantHeader = request.Headers.TryGetValues("X-Tenant-Id", out var values) ? values.First() : null;
            LastAuthorization = request.Headers.Authorization?.ToString() ?? string.Empty;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}

/// <summary>
/// MOD-0280-FU01 T2a — the view side: every localized key My Timesheet and the timer chip use exists in all seven
/// <c>TimeEntryIndex</c> resx files, and the tenant shell holds exactly ONE line for the chip (pack §5.2).
/// </summary>
public sealed class TimeEntryViewContractTests
{
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Theory]
    [InlineData("Views", "TimeEntry", "Index.cshtml")]
    [InlineData("Views", "TimeEntry", "_IndexL10n.cshtml")]
    [InlineData("Views", "Shared", "_TimerChip.cshtml")]
    public void Every_localizer_key_of_the_view_exists_in_all_seven_languages(params string[] view)
    {
        var source = File.ReadAllText(SourcePath(view));
        var keys = Regex.Matches(source, @"Localizer\[""([^""]+)""\]").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.NotEmpty(keys);

        foreach (var language in Languages)
        {
            var resx = File.ReadAllText(SourcePath("Resources", "Views", "TimeEntry", $"TimeEntryIndex.{language}.resx"));
            var missing = keys.Where(k => !resx.Contains($"<data name=\"{k}\"", StringComparison.Ordinal)).ToList();
            Assert.True(missing.Count == 0, $"{language} lacks: {string.Join(", ", missing)}");
        }
    }

    [Fact]
    public void The_page_draws_nothing_but_the_explanation_without_the_read_key_and_uses_the_tenant_shell()
    {
        var index = File.ReadAllText(SourcePath("Views", "TimeEntry", "Index.cshtml"));
        Assert.Contains("Layout = \"_LayoutTenantShell\";", index);
        var gate = index.IndexOf("@if (!Perms.Has(\"time-entry.timesheets.read\"))", StringComparison.Ordinal);
        Assert.True(gate > 0, "the UAS-001 gate is missing");
        Assert.Contains("<partial name=\"_AccessDenied\"", index[gate..(gate + 200)]);
        Assert.True(index.IndexOf("id=\"timeEntryPage\"", StringComparison.Ordinal) > gate, "the page is drawn before the gate");
        Assert.DoesNotContain("Redirect", index);
    }

    [Fact]
    public void The_tenant_shell_holds_exactly_one_line_for_the_timer_chip_next_to_the_bell()
    {
        var layout = File.ReadAllText(SourcePath("Views", "Shared", "_LayoutTenantShell.cshtml"));
        Assert.Single(Regex.Matches(layout, "_TimerChip"));
        var chip = layout.IndexOf("<partial name=\"_TimerChip\" />", StringComparison.Ordinal);
        var bell = layout.IndexOf("<!-- Notification -->", StringComparison.Ordinal);
        Assert.True(chip > 0 && bell > chip && bell - chip < 200, "the chip is not right before the notification bell");
    }

    [Fact]
    public void My_timesheet_has_its_nav_name_in_all_seven_languages()
    {
        foreach (var language in Languages)
        {
            var shared = File.ReadAllText(SourcePath("Resources", $"SharedResource.{language}.resx"));
            Assert.Contains("<data name=\"Nav.Page.MYTIMESHEET\"", shared);
            Assert.Contains("<data name=\"Nav.Module.TIMEENTRY\"", shared);
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
