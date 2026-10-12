using System.Net;
using System.Security.Claims;
using System.Text;
using Diten.Web.Models.Inventory;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.Web.Tests.Inventory;

/// <summary>
/// WP-0173-PM0 — the DEVELOPMENT-only product-master bridge, measured through the real request path
/// (<see cref="InventoryProxy.Gateway"/> → <see cref="InventoryGatewayClient"/>) with a handler that records where each
/// call went:
/// <list type="bullet">
/// <item>setting empty → the Gateway's <c>/api/product-master/…</c>, exactly as before;</item>
/// <item>setting set + Development → the bridge, prefix removed, the same headers;</item>
/// <item>setting set + any other environment → the Gateway, and a warning (configuration-safety).</item>
/// </list>
/// Only <c>/api/product-master</c> moves; inventory / location / Model A calls stay on the Gateway in every case.
/// </summary>
public sealed class ProductMasterBridgeTests
{
    private const string Gateway = "http://gateway.test";
    private const string Bridge = "http://127.0.0.1:4010/";
    private static readonly string TenantId = Guid.NewGuid().ToString();

    [Fact]
    public async Task Without_the_setting_product_master_goes_to_the_Gateway_as_before()
    {
        var calls = await CallAsync(configured: null, environment: "Development");

        Assert.Equal($"{Gateway}/api/product-master/products/p-1", calls[0].Url);
        Assert.Equal($"{Gateway}/api/inventory/balance?page=1", calls[1].Url);
    }

    [Fact]
    public async Task With_the_setting_in_Development_product_master_goes_to_the_bridge_without_the_prefix_and_with_the_same_headers()
    {
        var calls = await CallAsync(configured: Bridge, environment: "Development");

        Assert.Equal("http://127.0.0.1:4010/products/p-1", calls[0].Url);
        Assert.Equal("Bearer token-1", calls[0].Authorization);
        Assert.Equal(TenantId, calls[0].Tenant);
        // Everything else still goes to the Gateway.
        Assert.Equal($"{Gateway}/api/inventory/balance?page=1", calls[1].Url);
        Assert.Equal(calls[1].Authorization, calls[0].Authorization);
        Assert.Equal(calls[1].Tenant, calls[0].Tenant);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task With_the_setting_outside_Development_it_is_ignored_and_a_warning_is_logged(string environment)
    {
        var logger = new CapturingLogger();
        Assert.Null(ProductMasterBridge.Resolve(Bridge, environment, logger));
        Assert.Contains(logger.Warnings, w => w.Contains("IGNORED", StringComparison.Ordinal) && w.Contains(environment, StringComparison.Ordinal));

        var calls = await CallAsync(configured: Bridge, environment: environment);
        Assert.Equal($"{Gateway}/api/product-master/products/p-1", calls[0].Url);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://127.0.0.1:4010/")]
    public void A_malformed_setting_is_ignored_with_a_warning(string configured)
    {
        var logger = new CapturingLogger();
        Assert.Null(ProductMasterBridge.Resolve(configured, "Development", logger));
        Assert.NotEmpty(logger.Warnings);
    }

    [Theory]
    [InlineData("/api/product-master/skus/s-1/uom", "http://b.test/skus/s-1/uom")]
    [InlineData("/api/product-master/skus/s-1?level=Gsku", "http://b.test/skus/s-1?level=Gsku")]
    [InlineData("/api/product-masterX/y", "http://gw.test/api/product-masterX/y")]
    [InlineData("/api/locations/l-1", "http://gw.test/api/locations/l-1")]
    public void Only_the_product_master_prefix_is_bridged(string path, string expected) =>
        Assert.Equal(expected, ProductMasterBridge.Target("http://gw.test", "http://b.test", path));

    // ── harness ───────────────────────────────────────────────────────────────────────────────────────────

    private sealed record Call(string Url, string? Authorization, string? Tenant);

    private static async Task<List<Call>> CallAsync(string? configured, string environment)
    {
        var handler = new RecordingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ProductMasterBridge.SettingKey] = configured }).Build());
        services.AddSingleton<IWebHostEnvironment>(new Environment(environment));
        services.AddLogging();

        var http = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenantId", TenantId), new Claim("legal_entity_id", Guid.NewGuid().ToString())], "test"))
        };
        http.Request.Headers.Cookie = "access_token=token-1";

        var client = InventoryProxy.Gateway(new HttpClient(handler), Gateway, http.Request, http.User)!;
        await client.GetAsync("/api/product-master/products/p-1", CancellationToken.None);
        await client.GetAsync("/api/inventory/balance?page=1", CancellationToken.None);
        return handler.Calls;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<Call> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add(new Call(request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Tenant-Id", out var tenant) ? tenant.Single() : null));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
        }
    }

    private sealed class Environment(string name) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Diten.Web";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }
    }
}
