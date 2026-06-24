using System.Net;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleCatalog;

// MODULE-CATALOG AUTOMATION Phase 1 — platform sync service is best-effort: it must never throw and must
// only call AuthService for canonical keys.
public sealed class CatalogPermissionSyncServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_key_is_skipped_without_calling_auth(string? key)
    {
        var (svc, handler) = Build(_ => throw new InvalidOperationException("must not be called"));

        var status = await svc.SyncPermissionAsync(key, "x", CancellationToken.None);

        Assert.Equal(CatalogPermissionSyncStatus.SkippedEmpty, status);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("goldenslim.records")]            // 2 segments
    [InlineData("goldenslim.records.read_all")]   // underscore
    public async Task Non_canonical_key_is_invalid_without_calling_auth(string key)
    {
        var (svc, handler) = Build(_ => throw new InvalidOperationException("must not be called"));

        var status = await svc.SyncPermissionAsync(key, "x", CancellationToken.None);

        Assert.Equal(CatalogPermissionSyncStatus.InvalidFormat, status);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Auth_down_returns_failed_without_throwing()
    {
        var (svc, _) = Build(_ => throw new HttpRequestException("connection refused"));

        var status = await svc.SyncPermissionAsync("goldenslim.records.read", "Read", CancellationToken.None);

        Assert.Equal(CatalogPermissionSyncStatus.Failed, status);
    }

    [Fact]
    public async Task Non_success_status_returns_failed()
    {
        var (svc, _) = Build(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var status = await svc.SyncPermissionAsync("goldenslim.records.read", "Read", CancellationToken.None);

        Assert.Equal(CatalogPermissionSyncStatus.Failed, status);
    }

    [Fact]
    public async Task Success_returns_synced_and_posts_canonical_key_with_internal_header()
    {
        HttpMethod? method = null;
        string? uri = null;
        IEnumerable<string>? keys = null;
        string? body = null;
        var (svc, _) = Build(req =>
        {
            // Capture everything here — the service disposes the request once SendAsync returns.
            method = req.Method;
            uri = req.RequestUri!.ToString();
            req.Headers.TryGetValues("X-Internal-Api-Key", out keys);
            body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        // Mixed case / surrounding whitespace is normalized before sending.
        var status = await svc.SyncPermissionAsync("  GOLDENSLIM.records.read  ", "Read Golden Slim", CancellationToken.None);

        Assert.Equal(CatalogPermissionSyncStatus.Synced, status);
        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("http://auth.local/internal/permissions/sync", uri);
        Assert.Equal("test-internal-key", Assert.Single(keys!));
        Assert.Contains("goldenslim.records.read", body);
    }

    private static (CatalogPermissionSyncService svc, RecordingHandler handler) Build(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new RecordingHandler(responder);
        var factory = new StubHttpClientFactory(handler);
        var options = Options.Create(new AuthServiceOptions
        {
            BaseUrl = "http://auth.local",
            InternalApiKey = "test-internal-key"
        });
        var svc = new CatalogPermissionSyncService(factory, options, NullLogger<CatalogPermissionSyncService>.Instance);
        return (svc, handler);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(responder(request));
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
