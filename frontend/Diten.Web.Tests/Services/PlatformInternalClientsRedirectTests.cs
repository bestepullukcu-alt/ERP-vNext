using System.Net;
using System.Net.Sockets;
using System.Text;
using Diten.Web.Services.Branding;
using Diten.Web.Services.TenantResolution;
using Diten.Web.Services.TenantStatus;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Web.Tests.Services;

/// <summary>
/// BL-454 FIX3 — the three lookups Web makes DIRECTLY to Platform with the shared internal API key (branding, tenant
/// liveness, vanity slug), resolved from the app's own composition (Program.cs, through WebApplicationFactory) and
/// called for real against a loopback server that answers <c>302</c>. The key is a custom header, which HttpClient keeps
/// across a redirect; the redirect target must never be reached.
/// </summary>
public sealed class PlatformInternalClientsRedirectTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PlatformInternalClientsRedirectTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Theory]
    [InlineData("branding")]
    [InlineData("status")]
    [InlineData("slug")]
    public async Task A_redirect_answer_is_never_followed_with_the_internal_key(string lookup)
    {
        using var server = new RedirectingServer();
        await using var app = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("PlatformServiceUrl", server.Url)
            .UseSetting("Platform:InternalApiKey", "fix3-test-only-internal-key"));
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        switch (lookup)
        {
            case "branding": await services.GetRequiredService<IBrandingGateway>().GetTenantBrandingAsync(Guid.NewGuid()); break;
            case "status": await services.GetRequiredService<ITenantStatusGateway>().GetTenantLivenessAsync(Guid.NewGuid()); break;
            default: await services.GetRequiredService<ITenantSlugResolver>().ResolveActiveTenantIdAsync("fix3-" + Guid.NewGuid().ToString("N")[..8]); break;
        }

        Assert.True(server.First > 0, $"{lookup} never reached the server: the test proves nothing.");
        Assert.Equal(0, server.Trapped);
    }

    private sealed class RedirectingServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private int _first;
        private int _trapped;

        public RedirectingServer()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            Url = $"http://127.0.0.1:{port}";
            _listener.Prefixes.Add(Url + "/");
            _listener.Start();
            _ = Task.Run(LoopAsync);
        }

        public string Url { get; }
        public int First => Volatile.Read(ref _first);
        public int Trapped => Volatile.Read(ref _trapped);

        private async Task LoopAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch
                {
                    return;
                }

                if (context.Request.Url!.AbsolutePath.StartsWith("/trap", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref _trapped);
                    context.Response.StatusCode = 200;
                }
                else
                {
                    Interlocked.Increment(ref _first);
                    context.Response.StatusCode = 302;
                    context.Response.RedirectLocation = Url + "/trap";
                }

                var bytes = Encoding.UTF8.GetBytes("{}");
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        public void Dispose()
        {
            _listener.Stop();
            _listener.Close();
        }
    }
}
