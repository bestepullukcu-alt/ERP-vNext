using Diten.ApiGateway.Observability;
using Diten.Platform.Common.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 FIX2 (A3) — the correlation an audit record carries is the SERVER's. At the gateway (the
/// edge) a caller's X-Correlation-Id is never adopted: the gateway mints its own and forwards the caller's value apart,
/// on X-Client-Correlation-Id. Behind the gateway the Common default is unchanged (the gateway's value is trusted).
/// The Common middleware has no test project of its own; it is measured here, where it runs at the edge.
/// </summary>
public sealed class EdgeCorrelationTests
{
    private static CorrelationIdMiddleware Middleware(bool trustInbound, RequestDelegate? next = null) =>
        new(next ?? (_ => Task.CompletedTask), Options.Create(new ObservabilityOptions
        {
            Correlation = new CorrelationOptions { TrustInboundCorrelation = trustInbound }
        }));

    private static async Task<(HttpContext Http, CorrelationContext Correlation)> Run(bool trustInbound, params (string Name, string Value)[] headers)
    {
        var http = new DefaultHttpContext();
        foreach (var (name, value) in headers)
        {
            http.Request.Headers[name] = value;
        }

        var correlation = new CorrelationContext();
        await Middleware(trustInbound).InvokeAsync(http, correlation);
        return (http, correlation);
    }

    [Fact]
    public async Task At_the_edge_two_requests_with_one_fixed_client_header_get_two_server_correlations_and_keep_the_client_value()
    {
        var first = await Run(trustInbound: false, ("X-Correlation-Id", "fixed-by-the-caller"));
        var second = await Run(trustInbound: false, ("X-Correlation-Id", "fixed-by-the-caller"));

        Assert.NotEqual("fixed-by-the-caller", first.Correlation.CorrelationId);
        Assert.NotEqual(first.Correlation.CorrelationId, second.Correlation.CorrelationId);
        Assert.Equal("fixed-by-the-caller", first.Correlation.ClientCorrelationId);
        Assert.Equal("fixed-by-the-caller", second.Correlation.ClientCorrelationId);
        Assert.Equal("fixed-by-the-caller", first.Http.Items[CorrelationIdMiddleware.ClientCorrelationItemKey]);
        Assert.Equal(first.Correlation.CorrelationId, first.Http.Response.Headers["X-Correlation-Id"].ToString());
    }

    [Fact]
    public async Task Behind_the_edge_the_default_is_unchanged_and_the_client_value_is_read_from_its_own_header()
    {
        var (_, correlation) = await Run(trustInbound: true,
            ("X-Correlation-Id", "minted-by-the-gateway"), ("X-Client-Correlation-Id", "from-the-caller"));

        Assert.Equal("minted-by-the-gateway", correlation.CorrelationId);
        Assert.Equal("from-the-caller", correlation.ClientCorrelationId);
        Assert.True(new CorrelationOptions().TrustInboundCorrelation); // the Common default stays "trust"
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("<script>")]
    public async Task An_unsafe_client_value_is_not_kept(string value)
    {
        var (http, correlation) = await Run(trustInbound: false, ("X-Correlation-Id", value));

        Assert.Null(correlation.ClientCorrelationId);
        Assert.False(http.Items.ContainsKey(CorrelationIdMiddleware.ClientCorrelationItemKey));
    }

    [Fact]
    public async Task The_gateway_forwards_its_own_correlation_and_the_client_value_apart_and_drops_what_the_caller_put_on_the_client_header()
    {
        var http = new DefaultHttpContext { TraceIdentifier = "server-minted" };
        http.Items[CorrelationIdMiddleware.ClientCorrelationItemKey] = "client-said";
        var sink = new Sink();
        var handler = new CorrelationPropagationDelegatingHandler(new HttpContextAccessor { HttpContext = http },
            Options.Create(new ObservabilityOptions())) { InnerHandler = sink };
        var request = new HttpRequestMessage(HttpMethod.Get, "http://platform.test/x");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", "client-said");
        request.Headers.TryAddWithoutValidation("X-Client-Correlation-Id", "forged-by-the-caller");

        await new HttpMessageInvoker(handler).SendAsync(request, CancellationToken.None);

        Assert.Equal(["server-minted"], sink.Request!.Headers.GetValues("X-Correlation-Id"));
        Assert.Equal(["client-said"], sink.Request.Headers.GetValues("X-Client-Correlation-Id"));
    }

    [Fact]
    public void The_gateway_turns_trust_off_and_only_the_gateway()
    {
        var edge = new ServiceCollection().Configure<ObservabilityOptions>(_ => { }).AddEdgeCorrelation().BuildServiceProvider();
        var service = new ServiceCollection().Configure<ObservabilityOptions>(_ => { }).BuildServiceProvider();

        Assert.False(edge.GetRequiredService<IOptions<ObservabilityOptions>>().Value.Correlation.TrustInboundCorrelation);
        Assert.True(service.GetRequiredService<IOptions<ObservabilityOptions>>().Value.Correlation.TrustInboundCorrelation);

        var program = File.ReadAllText(Path.Combine(RepoRoot(), "gateway", "Diten.ApiGateway", "Program.cs"));
        Assert.Contains("builder.Services.AddEdgeCorrelation();", program);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repo root not found.");
    }

    private sealed class Sink : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
