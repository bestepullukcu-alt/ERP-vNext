using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.Web.Tests.Logging;

/// <summary>
/// BL-517 — a person search must not leave the searched name in the Web log. Measured on the real application (its
/// own Program, its own logging registration): an incoming request and an outgoing HTTP-client call, both carrying
/// <c>?search=Ayşe</c>. The lines stay (levels are not lowered); the query does not.
/// </summary>
public sealed class QueryStringRedactionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] Forbidden = ["Ayşe", "Ay%C5%9Fe", "search="];
    private readonly WebApplicationFactory<Program> _factory;

    public QueryStringRedactionTests(WebApplicationFactory<Program> factory) => _factory = factory;

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

        await client.GetAsync("/bl517-probe/people/lookup?search=Ayşe");

        var lines = capture.For("Microsoft.AspNetCore.Hosting.Diagnostics");
        Assert.Contains(lines, line => line.Contains("/bl517-probe/people/lookup", StringComparison.Ordinal));
        AssertNoQuery(lines);
    }

    [Fact]
    public async Task An_outgoing_client_call_is_logged_with_its_path_and_without_its_query()
    {
        var capture = new CaptureProvider();
        using var app = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging => logging.AddProvider(capture));
            builder.ConfigureServices(services => services.AddHttpClient("bl517-probe")
                .ConfigurePrimaryHttpMessageHandler(() => new Answer()));
        });
        var http = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("bl517-probe");

        await http.GetAsync("https://gateway.invalid/api/people/lookup?search=Ayşe");

        var lines = capture.For("System.Net.Http.HttpClient.bl517-probe.");
        Assert.Contains(lines, line => line.Contains("https://gateway.invalid/api/people/lookup", StringComparison.Ordinal));
        AssertNoQuery(lines);
    }

    [Fact]
    public void A_structured_Uri_value_keeps_its_path_and_loses_its_query()
    {
        // The framework writes the outgoing address as text today (covered above); a Uri VALUE — what another logging
        // call site or a later framework passes — must be cut the same way, not written as-is.
        var capture = new CaptureProvider();
        using var inner = LoggerFactory.Create(logging => logging.AddProvider(capture));
        var logger = new Diten.Web.Services.Http.QueryStringRedactingLoggerFactory(inner)
            .CreateLogger("System.Net.Http.HttpClient.bl517-probe.LogicalHandler");
        var state = new List<KeyValuePair<string, object?>>
        {
            new("Uri", new Uri("https://gateway.invalid/api/people/lookup?search=Ayşe")),
            new("{OriginalFormat}", "Start processing HTTP request {Uri}")
        };

        logger.Log(LogLevel.Information, default, state, null, (_, _) => "Start processing HTTP request");

        var line = Assert.Single(capture.For("System.Net.Http.HttpClient.bl517-probe."));
        Assert.Contains("Uri=https://gateway.invalid/api/people/lookup", line, StringComparison.Ordinal);
        AssertNoQuery([line]);
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

    /// <summary>Records each line twice over: the rendered message and every structured value it carries.</summary>
    private sealed class CaptureProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<(string Category, string Line)> _lines = new();

        public IReadOnlyList<string> For(string categoryPrefix)
            => _lines.Where(x => x.Category.StartsWith(categoryPrefix, StringComparison.Ordinal)).Select(x => x.Line).ToList();

        public ILogger CreateLogger(string categoryName) => new Capture(categoryName, _lines);

        public void Dispose() { }

        private sealed class Capture(string category, ConcurrentQueue<(string, string)> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                    ? string.Join(" | ", pairs.Select(pair => $"{pair.Key}={pair.Value}"))
                    : string.Empty;
                lines.Enqueue((category, formatter(state, exception) + " || " + values));
            }
        }
    }
}
