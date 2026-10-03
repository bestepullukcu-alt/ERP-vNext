using System.Net;
using Diten.SupplyChainService.Api.ModuleRegistration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.SupplyChainService.Tests.Carriers;

public sealed class CarrierModuleRegistrationHostedServiceTests
{
    [Fact]
    public async Task Sends_exact_manifest_with_supported_internal_key()
    {
        var handler = new RecordingHandler();
        var service = CreateService(handler, configured: true);

        await service.RunRegistrationsAsync((_, _) => Task.CompletedTask, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Contains("carrier-management", request.Body, StringComparison.Ordinal);
        Assert.Equal("test-only-secret", request.InternalApiKey);
        Assert.False(request.HasDedicatedCredential);
    }

    [Fact]
    public async Task Missing_internal_key_fails_closed_without_request()
    {
        var handler = new RecordingHandler();
        var service = CreateService(handler, configured: false);

        await service.RunRegistrationsAsync((_, _) => Task.CompletedTask, CancellationToken.None);

        Assert.Empty(handler.Requests);
    }

    private static ModuleRegistrationHostedService CreateService(RecordingHandler handler, bool configured)
    {
        var options = new PlatformRegistrationOptions
        {
            BaseUrl = "https://platform.test",
            InternalApiKey = configured ? "test-only-secret" : string.Empty
        };
        return new ModuleRegistrationHostedService(
            [new CarrierManagementManifestProvider()],
            Options.Create(options),
            new TestHttpClientFactory(handler),
            NullLogger<ModuleRegistrationHostedService>.Instance);
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            request.Headers.TryGetValues("X-Internal-Api-Key", out var keys);
            Requests.Add(new CapturedRequest(body, keys?.SingleOrDefault(),
                request.Headers.Contains("X-Module-Registration-Credential-Id")
                || request.Headers.Contains("X-Module-Registration-Credential")));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed record CapturedRequest(string Body, string? InternalApiKey, bool HasDedicatedCredential);
}
