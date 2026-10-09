using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Infrastructure;
using Diten.MdmService.Infrastructure.ReferenceData;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.MdmService.Application.Tests.ReferenceData;

/// <summary>
/// WP-PLT-ITEM-LISTS-01 FIX1 R1 / M5: the item-list client AS PRODUCTION WIRES IT (the real HttpClient pipeline) against a
/// throwaway loopback listener standing in for Platform — a redirect is never followed (the credential and the caller's
/// token never reach another host), and the secret headers never reach the logs, Trace included. The listener is a test
/// stand-in only; nothing here treats loopback as a trust signal.
/// </summary>
public sealed class ItemReferenceClientWiringTests
{
    private const string Secret = "fix1-resolver-secret-value";
    private const string UserToken = "fix1-user-token-value";
    private static readonly ItemReferenceRequest[] Asked =
    [
        new("material-type", "RAW_MATERIAL"), new("item-uom", "KGM"), new("item-uom", "GRM"), new("item-uom", "IU"),
        new("storage-condition", "AMBIENT_15_25"), new("storage-condition", "PROTECT_FROM_LIGHT")
    ];

    // R1 (the rule of DependencyInjection.cs, pinned for THIS client's statement, not anywhere in the file).
    [Fact]
    public void The_item_client_registration_disables_redirects_and_redacts_every_secret_header()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "services", "Diten.MdmService", "src",
            "Diten.MdmService.Infrastructure", "DependencyInjection.cs"));
        var start = source.IndexOf("AddHttpClient<IItemReferenceResolver, PlatformItemReferenceResolverClient>()", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var statement = source[start..source.IndexOf(';', start)];

        Assert.Contains("AllowAutoRedirect = false", statement, StringComparison.Ordinal);
        Assert.Contains("RedactLoggedHeaders", statement, StringComparison.Ordinal);
        foreach (var header in new[] { "\"Authorization\"", "CredentialIdHeader", "CredentialSecretHeader", "AudienceHeader" })
            Assert.Contains(header, statement, StringComparison.Ordinal);
    }

    // R1 behaviour: Platform's address answers 307 to another path — the wired client does not follow it: 503, and the
    // redirect target is never asked.
    [Fact]
    public async Task A_redirect_is_never_followed()
    {
        await using var platform = LoopbackPlatform.Start(redirect: true);
        using var provider = Wired(platform.BaseAddress, new Capture());

        var result = await provider.GetRequiredService<IItemReferenceResolver>().ResolveLatestAsync(Asked);

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(1, platform.Hits(LoopbackPlatform.ResolvePath));
        Assert.Equal(0, platform.Hits(LoopbackPlatform.ElsewherePath));
    }

    // M5: the wired client's logs (Trace included) never carry the secret or the caller's token; the call itself works.
    [Fact]
    public async Task No_secret_reaches_the_logs()
    {
        await using var platform = LoopbackPlatform.Start(redirect: false);
        var capture = new Capture();
        using var provider = Wired(platform.BaseAddress, capture);

        var result = await provider.GetRequiredService<IItemReferenceResolver>().ResolveLatestAsync(Asked);

        Assert.True(result.IsSuccessful, result.FailureCode);
        Assert.Equal(Secret, platform.LastSecret);
        Assert.NotEmpty(capture.Lines);
        Assert.DoesNotContain(capture.Lines, line => line.Contains(Secret, StringComparison.Ordinal));
        Assert.DoesNotContain(capture.Lines, line => line.Contains(UserToken, StringComparison.Ordinal));
    }

    private static ServiceProvider Wired(Uri baseAddress, Capture capture)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(capture));
        services.AddSingleton<IHostEnvironment>(new ItemListsTestEnvironment("Development"));
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user")], "Bearer"))
        };
        http.Request.Headers.Authorization = "Bearer " + UserToken;
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = http });
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VerifiedGskuResolver:PlatformBaseAddress"] = baseAddress.ToString(),
            ["VerifiedGskuResolver:Timeout"] = "00:00:02",
            ["VerifiedGskuResolver:CredentialIdentifier"] = "fix1-resolver-id",
            ["VerifiedGskuResolver:CredentialSecret"] = Secret
        }).Build());
        return services.BuildServiceProvider();
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "services")))
            directory = directory.Parent;
        return directory!.FullName;
    }

    private sealed class Capture : ILoggerProvider
    {
        public ConcurrentQueue<string> Lines { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);
        public void Dispose() { }

        private sealed class Logger(Capture capture, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                capture.Lines.Enqueue($"{logLevel} {category} {formatter(state, exception)} {state}");
        }
    }

    // A throwaway HTTP listener on a free loopback port: the resolve path answers the contract (or 307 elsewhere).
    private sealed class LoopbackPlatform : IAsyncDisposable
    {
        public const string ResolvePath = "/" + PlatformItemReferenceResolverClient.RelativePath;
        public const string ElsewherePath = "/elsewhere";
        private readonly HttpListener _listener = new();
        private readonly ConcurrentDictionary<string, int> _hits = new();
        private readonly bool _redirect;
        private readonly Task _loop;

        public Uri BaseAddress { get; }
        public string? LastSecret { get; private set; }

        private LoopbackPlatform(bool redirect)
        {
            _redirect = redirect;
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            BaseAddress = new Uri($"http://127.0.0.1:{port}/");
            _listener.Prefixes.Add(BaseAddress.ToString());
            _listener.Start();
            _loop = Task.Run(LoopAsync);
        }

        public static LoopbackPlatform Start(bool redirect) => new(redirect);

        public int Hits(string path) => _hits.TryGetValue(path, out var count) ? count : 0;

        private async Task LoopAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception) { return; }
                var path = context.Request.Url!.AbsolutePath;
                _hits.AddOrUpdate(path, 1, (_, count) => count + 1);
                LastSecret = context.Request.Headers[PlatformItemReferenceResolverClient.CredentialSecretHeader];
                if (_redirect && path == ResolvePath)
                {
                    context.Response.StatusCode = 307;
                    context.Response.RedirectLocation = new Uri(BaseAddress, ElsewherePath.TrimStart('/')).ToString();
                    context.Response.Close();
                    continue;
                }
                var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(Root(), "services", "Diten.Platform", "tests",
                    "Diten.Platform.Application.Tests", "BusinessReferenceData", "Fixtures", "item-universal-resolve.contract.json")))!;
                var body = Encoding.UTF8.GetBytes(fixture["response"]!.ToJsonString());
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(body);
                context.Response.Close();
            }
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            try { await _loop; } catch (Exception) { /* listener closed */ }
        }
    }
}
