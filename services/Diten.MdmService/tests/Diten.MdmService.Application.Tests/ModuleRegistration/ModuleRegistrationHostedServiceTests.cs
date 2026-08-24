using System.Net;
using System.Security.Cryptography;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.MdmService.Api.ModuleRegistration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.ModuleRegistration;

public sealed class ModuleRegistrationHostedServiceTests
{
    [Fact]
    public async Task Sends_each_provider_independently_and_retries_only_the_failed_manifest()
    {
        var secret = NewEphemeralSecret();
        var handler = new RecordingHandler(body =>
            body.Contains("product-item-sku-master", StringComparison.Ordinal)
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.OK);
        var service = CreateService(
            [new ProductItemSkuMasterManifestProvider(), new LegalEntityManifestProvider()],
            handler,
            secret);

        await service.RunRegistrationsAsync((_, _) => Task.CompletedTask, CancellationToken.None);

        Assert.Equal(5, handler.Requests.Count(request => request.Body.Contains("product-item-sku-master", StringComparison.Ordinal)));
        Assert.Single(handler.Requests, request => request.Body.Contains("legal-entity", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sends_only_dedicated_credential_headers_and_never_serializes_the_secret()
    {
        var secret = NewEphemeralSecret();
        var handler = new RecordingHandler(_ => HttpStatusCode.OK);
        var logger = new RecordingLogger<ModuleRegistrationHostedService>();
        var service = CreateService([new ProductItemSkuMasterManifestProvider()], handler, secret, logger);

        await service.RunRegistrationsAsync((_, _) => Task.CompletedTask, CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("test-only-mdm-registration", request.CredentialIdentifier);
        Assert.Equal(secret, request.CredentialSecret);
        Assert.False(request.HasSharedInternalApiKey);
        Assert.DoesNotContain(secret, request.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("ProducerOwnerCode", request.Body, StringComparison.OrdinalIgnoreCase);
        Assert.All(logger.Messages, message => Assert.DoesNotContain(secret, message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Missing_dedicated_credential_fails_closed_without_shared_key_fallback()
    {
        var handler = new RecordingHandler(_ => HttpStatusCode.OK);
        var options = new PlatformRegistrationOptions
        {
            BaseUrl = "https://platform.test",
            InternalApiKey = NewEphemeralSecret()
        };
        var service = new ModuleRegistrationHostedService(
            [new ProductItemSkuMasterManifestProvider()],
            Options.Create(options),
            new TestHttpClientFactory(handler),
            NullLogger<ModuleRegistrationHostedService>.Instance);

        await service.RunRegistrationsAsync((_, _) => Task.CompletedTask, CancellationToken.None);

        Assert.Empty(handler.Requests);
    }

    private static ModuleRegistrationHostedService CreateService(
        IEnumerable<IModuleManifestProvider> providers,
        RecordingHandler handler,
        string secret,
        ILogger<ModuleRegistrationHostedService>? logger = null)
    {
        var options = new PlatformRegistrationOptions
        {
            BaseUrl = "https://platform.test",
            InternalApiKey = NewEphemeralSecret(),
            ModuleRegistrationCredentialIdentifier = "test-only-mdm-registration",
            ModuleRegistrationCredentialSecret = secret
        };

        return new ModuleRegistrationHostedService(
            providers,
            Options.Create(options),
            new TestHttpClientFactory(handler),
            logger ?? NullLogger<ModuleRegistrationHostedService>.Instance);
    }

    private static string NewEphemeralSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(Func<string, HttpStatusCode> responseSelector) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            request.Headers.TryGetValues("X-Module-Registration-Credential-Id", out var identifiers);
            request.Headers.TryGetValues("X-Module-Registration-Credential", out var secrets);

            Requests.Add(new CapturedRequest(
                body,
                identifiers?.SingleOrDefault(),
                secrets?.SingleOrDefault(),
                request.Headers.Contains("X-Internal-Api-Key")));

            return new HttpResponseMessage(responseSelector(body));
        }
    }

    private sealed record CapturedRequest(
        string Body,
        string? CredentialIdentifier,
        string? CredentialSecret,
        bool HasSharedInternalApiKey);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (exception is not null)
            {
                Messages.Add(exception.ToString());
            }
        }
    }
}
