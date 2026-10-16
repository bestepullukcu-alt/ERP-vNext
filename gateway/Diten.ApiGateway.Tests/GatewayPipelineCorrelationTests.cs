using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Diten.ApiGateway.Observability;
using Diten.Platform.Common.Observability;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 FIX3 — the edge's correlation measured through the REAL gateway: its own Program.cs (every
/// registration and every middleware, Ocelot 24 with the shipped ocelot.json), served by a TestServer. The one stand-in
/// is the network: a last delegating handler keeps the request Ocelot would send downstream and answers 200.
/// </summary>
[Collection(GatewayProgramCollection.Name)]
public sealed class GatewayPipelineCorrelationTests
{
    [Fact]
    public async Task Through_the_real_gateway_a_RequestId_header_the_caller_chose_never_becomes_the_forwarded_correlation()
    {
        await using var gateway = await GatewayProgramHost.StartAsync();
        var client = gateway.Server.CreateClient();

        var forwarded = new List<string>();
        for (var i = 0; i < 2; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/platform-auth/probe");
            request.Headers.TryAddWithoutValidation("RequestId", "hep-ayni");          // Ocelot's default request-id header
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", "also-chosen"); // and the correlation header itself
            using var response = await client.SendAsync(request);

            var sent = Assert.Single(gateway.Downstream.TakeAll());
            var correlation = Assert.Single(sent.Headers.GetValues("X-Correlation-Id"));
            Assert.NotEqual("hep-ayni", correlation);
            Assert.NotEqual("also-chosen", correlation);
            // it IS the id the gateway minted for this request (the one it answered with)
            Assert.Equal(response.Headers.GetValues("X-Correlation-Id").Single(), correlation);
            // the caller's own value travels apart, as the client's
            Assert.Equal("also-chosen", Assert.Single(sent.Headers.GetValues("X-Client-Correlation-Id")));
            forwarded.Add(correlation);
        }

        Assert.NotEqual(forwarded[0], forwarded[1]); // one fixed header, two requests, two correlations
    }

    [Fact]
    public async Task The_real_gateway_does_not_trust_an_inbound_correlation()
    {
        await using var gateway = await GatewayProgramHost.StartAsync();

        Assert.False(gateway.Services.GetRequiredService<IOptions<ObservabilityOptions>>().Value.Correlation.TrustInboundCorrelation);
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GatewayProgramCollection
{
    public const string Name = "GatewayProgram";
}

/// <summary>
/// Boots the gateway's own entry point with its server swapped for a TestServer (the way WebApplicationFactory does for
/// a minimal host, without the extra package): the hosting DiagnosticListener's HostBuilding event lets services be
/// added, HostBuilt hands over the host, and Program.cs then runs as written.
/// </summary>
internal sealed class GatewayProgramHost : IAsyncDisposable
{
    private readonly IHost _host;
    private readonly Task _run;

    private GatewayProgramHost(IHost host, Task run, DownstreamCapture downstream)
    {
        _host = host;
        _run = run;
        Downstream = downstream;
    }

    public DownstreamCapture Downstream { get; }
    public IServiceProvider Services => _host.Services;
    public TestServer Server => (TestServer)_host.Services.GetRequiredService<IServer>();

    public static async Task<GatewayProgramHost> StartAsync()
    {
        var downstream = new DownstreamCapture();
        var built = new TaskCompletionSource<IHost>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new HostingObserver(
            building => building.ConfigureServices(services =>
            {
                services.AddSingleton<IServer>(sp => new TestServer(sp));
                // The network: Ocelot's global delegating handlers wrap in registration order; this one is added last.
                var global = typeof(Ocelot.DependencyInjection.IOcelotBuilder).Assembly.GetType("Ocelot.Requester.GlobalDelegatingHandler", throwOnError: true)!;
                services.AddTransient(global, _ => Activator.CreateInstance(global, new DownstreamSink(downstream))!);
            }),
            host => built.TrySetResult(host));

        var subscription = DiagnosticListener.AllListeners.Subscribe(observer);
        var entry = typeof(CorrelationPropagationDelegatingHandler).Assembly.EntryPoint!;
        // Test values only: a Development host, a freshly generated signing secret, the shipped ocelot.json next to the binaries.
        var args = new[]
        {
            "--environment=Development",
            "--contentRoot=" + AppContext.BaseDirectory,
            "--JwtSettings:Secret=" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
        };
        var run = Task.Run(() => entry.Invoke(null, [args]));

        try
        {
            var host = await built.Task.WaitAsync(TimeSpan.FromSeconds(60));
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
            using (lifetime.ApplicationStarted.Register(() => started.TrySetResult()))
            {
                await Task.WhenAny(started.Task, run).WaitAsync(TimeSpan.FromSeconds(60));
            }

            if (run.IsFaulted) await run; // Program.cs failed: say why
            return new GatewayProgramHost(host, run, downstream);
        }
        finally
        {
            subscription.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync();
        await _run.WaitAsync(TimeSpan.FromSeconds(30));
        _host.Dispose();
    }

    private sealed class HostingObserver(Action<IHostBuilder> building, Action<IHost> built)
        : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
    {
        private readonly List<IDisposable> _subscriptions = [];

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name == "Microsoft.Extensions.Hosting") _subscriptions.Add(listener.Subscribe(this));
        }

        public void OnNext(KeyValuePair<string, object?> pair)
        {
            if (pair.Key == "HostBuilding" && pair.Value is IHostBuilder builder) building(builder);
            if (pair.Key == "HostBuilt" && pair.Value is IHost host) built(host);
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }
}

/// <summary>What the gateway would have sent downstream.</summary>
internal sealed class DownstreamCapture
{
    private readonly ConcurrentQueue<HttpRequestMessage> _requests = new();
    public void Add(HttpRequestMessage request) => _requests.Enqueue(request);

    public List<HttpRequestMessage> TakeAll()
    {
        var taken = new List<HttpRequestMessage>();
        while (_requests.TryDequeue(out var request)) taken.Add(request);
        return taken;
    }
}

internal sealed class DownstreamSink(DownstreamCapture capture) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        capture.Add(request);
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { RequestMessage = request });
    }
}
