using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Xunit;

namespace Diten.ApiGateway.Tests;

/// <summary>
/// MOD-0290-FU04 S4 FIX2 (M7) — measured, over the wire: can a request under <c>/api/product-master/{everything}</c>
/// reach MDM's internal surface (<c>/api/internal/**</c>) with an encoded or raw <c>..</c> / <c>%2f</c>? A real Kestrel
/// listener stands in for MDM and records every request it receives (the raw target, and the path Kestrel routes on —
/// decoded except %2F, dot-normalised, exactly as MDM's own Kestrel would); the gateway is a real Kestrel host with Ocelot
/// (the version the gateway runs) and the product-master route as ocelot.json writes it. Requests are written to the
/// socket byte for byte (no client normalises them). The rule: nothing that arrives is routed under <c>/api/internal</c>.
/// </summary>
public sealed class Mod0290Fu04ProductMasterTraversalTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Theory]
    [InlineData("/api/product-master/..%2f..%2finternal/v1/product-master/validate")]
    [InlineData("/api/product-master/..%2F..%2Finternal/v1/product-master/validate")]
    [InlineData("/api/product-master/%2e%2e/%2e%2e/internal/v1/product-master/validate")]
    [InlineData("/api/product-master/%2e%2e%2f%2e%2e%2finternal/v1/product-master/validate")]
    [InlineData("/api/product-master/../../internal/v1/product-master/validate")]
    [InlineData("/api/product-master/skus/..%5c..%5c..%5cinternal/x")]
    [InlineData("/api/product-master/%252e%252e%252f%252e%252e%252finternal/x")]
    public async Task An_encoded_dot_dot_never_reaches_the_internal_surface(string target)
    {
        var seen = new ConcurrentQueue<(string Raw, string Routed)>();
        await using var downstream = await StartDownstreamAsync(seen);
        await using var gateway = await StartGatewayAsync(Port(downstream));

        var status = await RawGetAsync(Port(gateway), target);
        output.WriteLine($"{target} → gateway {status}; reached the service: "
                         + (seen.IsEmpty ? "nothing" : string.Join(" | ", seen.Select(x => $"{x.Raw} (routed {x.Routed})"))));

        foreach (var (raw, routed) in seen)
        {
            // What MDM routes on is Kestrel's own decoded, dot-normalised path — the listener here is Kestrel too.
            Assert.False(routed.StartsWith("/api/internal", StringComparison.OrdinalIgnoreCase),
                $"{target} reached the service as {raw} and was routed as {routed}");
            Assert.StartsWith("/api/product-master/", routed, StringComparison.OrdinalIgnoreCase);
        }
        Assert.True(status is >= 200 and < 600, $"no answer for {target}");
    }

    private static int Port(WebApplication app) => new Uri(app.Urls.Single()).Port;

    private static async Task<WebApplication> StartDownstreamAsync(ConcurrentQueue<(string Raw, string Routed)> seen)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.Run(context =>
        {
            seen.Enqueue((context.Features.Get<IHttpRequestFeature>()!.RawTarget, context.Request.Path.Value ?? string.Empty));
            return context.Response.WriteAsync("ok");
        });
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await app.StartAsync(bound.Token);
        return app;
    }

    private static async Task<WebApplication> StartGatewayAsync(int downstreamPort)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var route = $$"""
            {
              "Routes": [
                {
                  "DownstreamPathTemplate": "/api/product-master/{everything}",
                  "DownstreamScheme": "http",
                  "DownstreamHostAndPorts": [ { "Host": "127.0.0.1", "Port": {{downstreamPort}} } ],
                  "UpstreamPathTemplate": "/api/product-master/{everything}",
                  "UpstreamHttpMethod": [ "GET", "POST", "OPTIONS" ]
                }
              ],
              "GlobalConfiguration": { "BaseUrl": "http://127.0.0.1" }
            }
            """;
        builder.Configuration.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(route)));
        builder.Services.AddOcelot(builder.Configuration);
        var app = builder.Build();
        await app.UseOcelot();
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await app.StartAsync(bound.Token);
        return app;
    }

    private static async Task<int> RawGetAsync(int port, string target)
    {
        using var client = new TcpClient();
        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await client.ConnectAsync(IPAddress.Loopback, port, bound.Token);
        await using var stream = client.GetStream();
        var request = Encoding.ASCII.GetBytes($"GET {target} HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(request, bound.Token);
        using var reader = new StreamReader(stream, Encoding.ASCII);
        var statusLine = await reader.ReadLineAsync(bound.Token) ?? string.Empty;
        var parts = statusLine.Split(' ');
        return parts.Length > 1 && int.TryParse(parts[1], out var status) ? status : 0;
    }
}
