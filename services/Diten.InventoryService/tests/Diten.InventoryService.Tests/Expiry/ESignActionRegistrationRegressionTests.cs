using System.Collections.Concurrent;
using System.Net;
using Diten.InventoryService.Infrastructure.ESign;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.InventoryService.Tests.Expiry;

/// <summary>
/// WP-0176-F3 item 1 — the registration hosted service is generalised to one call per module (quarantine + expiry). The quarantine call
/// must stay BYTE-identical to the one before F3: same URL, same internal key header, same JSON body (golden, captured from the
/// pre-F3 implementation at 66f87dd44). The real hosted service runs over a recording HTTP handler.
/// </summary>
public sealed class ESignActionRegistrationRegressionTests
{
    /// <summary>SHA-256 + UTF-8 length of the body the pre-F3 service posted for module <c>quarantine</c> (captured at 66f87dd44 by running this
    /// test against the unchanged service; System.Net.Http.Json defaults, names escaped).</summary>
    private const string QuarantineGoldenSha256 = "b3c267d501199532e83599531f553e24680b04a15d943fda45aa39c1b3d1d205";

    private const int QuarantineGoldenLength = 3576;

    private sealed class Recorder : HttpMessageHandler
    {
        public ConcurrentQueue<(string Url, string? Key, string Body)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Enqueue((request.RequestUri!.ToString(),
                request.Headers.TryGetValues("X-Internal-Api-Key", out var key) ? key.Single() : null,
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":{\"registered\":1,\"unchanged\":0}}") };
        }
    }

    private sealed class Clients(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    internal static async Task<List<(string Url, string? Key, string Body)>> RunAsync(int expectedCalls)
    {
        var recorder = new Recorder();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PlatformRegistration:BaseUrl"] = "http://platform.test",
            ["PlatformRegistration:InternalApiKey"] = "test-internal-key"
        }).Build();
        var service = new ESignActionRegistrationHostedService(configuration, new Clients(recorder), NullLogger<ESignActionRegistrationHostedService>.Instance);
        await service.StartAsync(CancellationToken.None);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (recorder.Requests.Count < expectedCalls && DateTime.UtcNow < deadline) await Task.Delay(20);
        await Task.Delay(100); // no extra call may follow
        await service.StopAsync(CancellationToken.None);
        return [.. recorder.Requests];
    }

    [Fact]
    public async Task The_quarantine_registration_is_byte_identical_to_the_pre_F3_call()
    {
        var calls = await RunAsync(2);
        var quarantine = Assert.Single(calls, c => c.Body.Contains("\"module\":\"quarantine\"", StringComparison.Ordinal));
        Assert.Equal("http://platform.test/api/internal/esign/actions/register", quarantine.Url);
        Assert.Equal("test-internal-key", quarantine.Key);
        Assert.Equal(QuarantineGoldenLength, System.Text.Encoding.UTF8.GetByteCount(quarantine.Body));
        Assert.Equal(QuarantineGoldenSha256, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(quarantine.Body))).ToLowerInvariant());
    }
}
