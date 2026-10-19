using System.Collections.Concurrent;
using System.Diagnostics;
using Diten.Web.Services.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Diten.Web.Tests.Logging;

/// <summary>
/// BL-517 — a person search must not leave the searched name in the Web log. Measured on the real application (its
/// own Program, its own logging registration) wherever the framework or the application writes a URL: an incoming
/// request, an outgoing HTTP-client call and the scope it opens, an MVC redirect, and an application category's own
/// <c>{Path}</c> value — each carrying <c>?search=Ayşe&amp;name=Öz</c>. The lines stay (levels are not lowered); the
/// query does not, and a line without one goes through untouched.
/// </summary>
public sealed class QueryStringRedactionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Query = "?search=Ayşe&name=Öz";
    private static readonly string[] Forbidden = ["Ayşe", "Ay%C5%9Fe", "Öz", "%C3%96z", "search=", "name="];
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public QueryStringRedactionTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task An_incoming_request_is_logged_with_its_path_and_without_its_query()
    {
        var capture = new CaptureProvider();
        // Production writes these lines (appsettings.json: Microsoft.AspNetCore = Information); Development mutes them
        // (Warning), so the test opens the one category to production's level to see what production would write.
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging
            .AddProvider(capture)
            .AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Information)));
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        await client.GetAsync("/bl517-probe/people/lookup" + Query);

        var lines = capture.For("Microsoft.AspNetCore.Hosting.Diagnostics");
        Assert.Contains(lines, line => line.Contains("/bl517-probe/people/lookup", StringComparison.Ordinal));
        AssertNoQuery(lines);
    }

    [Fact]
    public async Task An_outgoing_client_call_is_logged_with_its_path_and_without_its_query_and_so_is_the_scope_it_opens()
    {
        var capture = new CaptureProvider();
        using var app = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(capture));
            builder.ConfigureServices(services => services.AddHttpClient("bl517-probe")
                .ConfigurePrimaryHttpMessageHandler(() => new Answer()));
        });
        var http = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("bl517-probe");

        await http.GetAsync("https://gateway.invalid/api/people/lookup" + Query);

        var lines = capture.For("System.Net.Http.HttpClient.bl517-probe.");
        Assert.Contains(lines, line => line.Contains("https://gateway.invalid/api/people/lookup", StringComparison.Ordinal));
        AssertNoQuery(lines);

        // net8.0 LogicalHandler opens "HTTP {HttpMethod} {Uri}" around the call with the FULL address; a provider that
        // writes scopes (console IncludeScopes, the JSON formatter, OpenTelemetry) prints it on every line inside.
        var scopes = capture.ScopesFor("System.Net.Http.HttpClient.bl517-probe.");
        Assert.Contains(scopes, scope => scope.Contains("HTTP GET https://gateway.invalid/api/people/lookup", StringComparison.Ordinal));
        AssertNoQuery(scopes);
    }

    [Fact]
    public async Task An_MVC_redirect_is_logged_with_its_destination_path_and_without_its_query()
    {
        var capture = new CaptureProvider();
        using var app = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging
                .AddProvider(capture)
                .AddFilter("Microsoft.AspNetCore.Mvc.Infrastructure", LogLevel.Information));
            builder.ConfigureServices(services => services.AddTransient<IStartupFilter, RedirectProbe>());
        });
        using var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/bl517-redirect-probe");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        var lines = capture.For("Microsoft.AspNetCore.Mvc.Infrastructure.RedirectResultExecutor");
        Assert.Contains(lines, line => line.Contains("redirecting to /people/lookup", StringComparison.Ordinal));
        AssertNoQuery(lines);
    }

    [Fact]
    public void An_application_category_writing_a_Path_value_keeps_the_path_and_loses_every_parameter()
    {
        // The application's own lines (about forty call sites) log {Path} / {Url} built with Request.QueryString — e.g.
        // CampaignsController proxies "/api/crm/contacts{Request.QueryString}" and logs it on failure. None is edited:
        // the factory reaches every category.
        var capture = new CaptureProvider();
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureLogging(logging => logging.AddProvider(capture)));
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Diten.Web.Controllers.CampaignsController");

        logger.LogError("Campaign Gateway request failed: {Method} {Path}", "GET", "/api/crm/contacts" + Query);
        using (logger.BeginScope("Proxy {Method} {Target}", "GET", "https://gateway.invalid/api/crm/contacts" + Query))
        {
            logger.LogWarning("Proxy answered {Status}", 502);
        }

        var lines = capture.For("Diten.Web.Controllers.CampaignsController");
        Assert.Contains(lines, line => line.Contains("Campaign Gateway request failed: GET /api/crm/contacts ||", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Path=/api/crm/contacts |", StringComparison.Ordinal));
        AssertNoQuery(lines);

        var scopes = capture.ScopesFor("Diten.Web.Controllers.CampaignsController");
        Assert.Contains(scopes, scope => scope.Contains("Proxy GET https://gateway.invalid/api/crm/contacts", StringComparison.Ordinal));
        AssertNoQuery(scopes);
    }

    [Theory]
    [InlineData("https://gateway.invalid/api/people/lookup", UriKind.Absolute)]
    [InlineData("/api/people/lookup", UriKind.Relative)]
    [InlineData("people/lookup", UriKind.Relative)]
    public void A_structured_Uri_value_keeps_its_path_and_loses_its_query_absolute_or_relative(string path, UriKind kind)
    {
        // The framework writes the outgoing address as text today (covered above); a Uri VALUE — what another logging
        // call site or a later framework passes — must be cut the same way, relative ones included.
        var capture = new CaptureProvider();
        using var inner = LoggerFactory.Create(logging => logging.AddProvider(capture));
        var logger = new QueryStringRedactingLoggerFactory(inner).CreateLogger("Diten.Web.Services.Probe");
        var state = new List<KeyValuePair<string, object?>>
        {
            new("Uri", new Uri(path + Query, kind)),
            new("{OriginalFormat}", "Calling {Uri}")
        };

        logger.Log(LogLevel.Information, default, state, null, (s, _) => "Calling " + s[0].Value);

        var line = Assert.Single(capture.For("Diten.Web.Services.Probe"));
        Assert.Contains("Uri=" + path + " |", line, StringComparison.Ordinal);
        AssertNoQuery([line]);
    }

    [Fact]
    public void A_line_without_a_URL_query_is_written_as_it_was()
    {
        var capture = new CaptureProvider();
        using var inner = LoggerFactory.Create(logging => logging.AddProvider(capture));
        var logger = new QueryStringRedactingLoggerFactory(inner).CreateLogger("Diten.Web.Services.Probe");

        // No question mark at all: the very same state object reaches the provider (nothing rebuilt, nothing formatted).
        var untouched = new List<KeyValuePair<string, object?>> { new("UserId", 42), new("{OriginalFormat}", "User {UserId} opened Users") };
        logger.Log(LogLevel.Information, default, untouched, null, (s, _) => "User 42 opened Users");
        // A question mark that does not follow a URL is not a query.
        logger.LogInformation("Is {Name} allowed? Asked at /api/permissions", "Ali");

        Assert.Same(untouched, capture.States.First());
        var lines = capture.For("Diten.Web.Services.Probe");
        Assert.Contains(lines, line => line.StartsWith("Is Ali allowed? Asked at /api/permissions ||", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Name=Ali", StringComparison.Ordinal));
    }

    [Fact]
    public void The_redaction_costs_a_line_without_a_query_well_under_the_five_microsecond_budget()
    {
        // CT-SHELL-FIX1 item 5: every category is now filtered, so the categories that used to cost nothing pay a scan.
        // Budget: 5 µs per line. The same call through the plain factory and through the redacting one; the best of
        // several rounds of each (the least disturbed), the difference per line.
        using var inner = LoggerFactory.Create(logging => logging.AddProvider(new NullSink()).SetMinimumLevel(LogLevel.Information));
        var plain = inner.CreateLogger("Diten.Web.Bench");
        var wrapped = new QueryStringRedactingLoggerFactory(inner).CreateLogger("Diten.Web.Bench");
        var userId = Guid.NewGuid();

        void Typical(ILogger logger) => logger.LogInformation("User {UserId} opened {Screen} in {Elapsed} ms", userId, "Users", 12.5);
        void WithQuery(ILogger logger) => logger.LogInformation("Proxy failed for {Path}", "/api/crm/contacts" + Query);

        var overhead = BestPerLine(wrapped, Typical) - BestPerLine(plain, Typical);
        var overheadWithQuery = BestPerLine(wrapped, WithQuery) - BestPerLine(plain, WithQuery);
        _output.WriteLine($"BL-517 redaction overhead per line: without a query {overhead * 1000:F0} ns; with a URL query {overheadWithQuery * 1000:F0} ns");

        Assert.True(overhead < 5.0, $"A line without a query costs {overhead:F3} µs more (budget 5 µs).");
    }

    private static double BestPerLine(ILogger logger, Action<ILogger> write)
    {
        const int lines = 100_000;
        for (var i = 0; i < 20_000; i++)
        {
            write(logger);
        }

        var best = double.MaxValue;
        for (var round = 0; round < 7; round++)
        {
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < lines; i++)
            {
                write(logger);
            }

            watch.Stop();
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds * 1000.0 / lines);
        }

        return best;
    }

    private static void AssertNoQuery(IReadOnlyList<string> lines)
    {
        Assert.NotEmpty(lines);
        foreach (var line in lines)
        {
            foreach (var forbidden in Forbidden)
            {
                Assert.DoesNotContain(forbidden, line, StringComparison.Ordinal);
            }
        }
    }

    private sealed class Answer : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
    }

    /// <summary>A real MVC RedirectResult, executed by MVC's own executor inside the real application's pipeline.</summary>
    private sealed class RedirectProbe : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Map("/bl517-redirect-probe", branch => branch.Run(context =>
                new RedirectResult("/people/lookup" + Query)
                    .ExecuteResultAsync(new ActionContext(context, context.GetRouteData(), new ActionDescriptor()))));
            next(app);
        };
    }

    /// <summary>An enabled provider that does nothing — the measurement isolates the redaction's own cost.</summary>
    private sealed class NullSink : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Sink();
        public void Dispose() { }

        private sealed class Sink : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                GC.KeepAlive(state);
            }
        }
    }

    /// <summary>
    /// Records each line as a provider that writes everything would: the rendered message, every structured value it
    /// carries, and — through the factory's external scope provider — every scope open around it, text and values.
    /// </summary>
    private sealed class CaptureProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly ConcurrentQueue<(string Category, string Line, string[] Scopes)> _lines = new();
        private readonly ConcurrentQueue<object?> _states = new();
        private IExternalScopeProvider? _scopes;

        public IReadOnlyList<object?> States => _states.ToList();

        public IReadOnlyList<string> For(string categoryPrefix)
            => _lines.Where(x => x.Category.StartsWith(categoryPrefix, StringComparison.Ordinal)).Select(x => x.Line).ToList();

        public IReadOnlyList<string> ScopesFor(string categoryPrefix)
            => _lines.Where(x => x.Category.StartsWith(categoryPrefix, StringComparison.Ordinal)).SelectMany(x => x.Scopes).ToList();

        public ILogger CreateLogger(string categoryName) => new Capture(categoryName, this);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

        public void Dispose() { }

        private static string Values(object? state) => state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? string.Join(" | ", pairs.Select(pair => $"{pair.Key}={Render(pair.Value)}"))
            : string.Empty;

        private static string Render(object? value) => value is System.Collections.IEnumerable items and not string
            ? string.Join(", ", items.Cast<object?>())
            : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

        private sealed class Capture(string category, CaptureProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
                => owner._scopes?.Push(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var scopes = new List<string>();
                owner._scopes?.ForEachScope((scope, list) => list.Add($"{scope} || {Values(scope)}"), scopes);
                owner._states.Enqueue(state);
                owner._lines.Enqueue((category, formatter(state, exception) + " || " + Values(state) + " |", scopes.ToArray()));
            }
        }
    }
}
