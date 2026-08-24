using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Infrastructure.ReferenceData;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.ReferenceData;

public sealed class PlatformVerifiedMarketResolverClientTests
{
    [Fact]
    public async Task Enumerate_active_posts_no_body_and_maps_only_public_market_fields()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, new
        {
            isSuccessful = true,
            data = new
            {
                markets = new object[]
                {
                    new { code = "US", display_text = "United States", sort_order = 20 },
                    new { code = "TR", display_text = "Türkiye", sort_order = 10 }
                }
            }
        }));

        var result = await CreateClient(handler).EnumerateActiveAsync();

        Assert.True(result.IsSuccessful, result.FailureCode);
        Assert.Equal(["US", "TR"], result.Markets.Select(x => x.Code));
        Assert.Equal(["United States", "Türkiye"], result.Markets.Select(x => x.DisplayText));
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith(
            "/api/internal/v1/reference-data/verified-market/enumerate-active",
            request.Uri!.AbsoluteUri);
        Assert.Null(request.Body);
        Assert.Equal("Bearer delegated-market-jwt", request.Authorization);
        Assert.False(request.Headers.ContainsKey("X-Tenant-Id"));
        Assert.Equal("resolver-id", Assert.Single(request.Headers["X-Verified-Gsku-Credential-Id"]));
        Assert.Equal("resolver-secret", Assert.Single(request.Headers["X-Verified-Gsku-Credential"]));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "REFERENCE_MARKET_NOT_FOUND", 404)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "REFERENCE_PROVIDER_UNAVAILABLE", 503)]
    [InlineData(HttpStatusCode.GatewayTimeout, "REFERENCE_PROVIDER_TIMEOUT", 504)]
    [InlineData(HttpStatusCode.Unauthorized, "REFERENCE_UNAUTHENTICATED", 503)]
    public async Task Enumeration_provider_failures_map_to_fail_closed_classes(
        HttpStatusCode providerStatus,
        string reasonCode,
        int expectedStatus)
    {
        var handler = new RecordingHandler(_ => Json(providerStatus, new
        {
            isSuccessful = false,
            reason_code = reasonCode
        }));

        var result = await CreateClient(handler).EnumerateActiveAsync();

        Assert.False(result.IsSuccessful);
        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.Empty(result.Markets);
    }

    [Theory]
    [InlineData("{\"isSuccessful\":true,\"data\":{\"markets\":[]}}")]
    [InlineData("{\"isSuccessful\":true,\"data\":{\"markets\":[{\"code\":\"tr\",\"display_text\":\"Türkiye\",\"sort_order\":10}]}}")]
    [InlineData("{\"isSuccessful\":true,\"data\":{\"markets\":[{\"code\":\"TR\",\"display_text\":\"Türkiye\",\"sort_order\":10,\"catalog_version\":7}]}}")]
    [InlineData("{\"isSuccessful\":true,\"data\":{\"markets\":[{\"code\":\"TR\",\"display_text\":\"Türkiye\",\"sort_order\":10},{\"code\":\"TR\",\"display_text\":\"Duplicate\",\"sort_order\":20}]}}")]
    public async Task Malformed_or_extended_enumeration_fails_closed(string payload)
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });

        var result = await CreateClient(handler).EnumerateActiveAsync();

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("REFERENCE_CONTRACT_MISMATCH", result.FailureCode);
        Assert.Empty(result.Markets);
    }

    [Fact]
    public async Task Enumeration_timeout_maps_to_504_and_caller_cancellation_propagates()
    {
        var blocking = new BlockingHandler();
        var timeout = await CreateClient(blocking, TimeSpan.FromMilliseconds(20)).EnumerateActiveAsync();
        Assert.Equal(504, timeout.StatusCode);

        using var caller = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateClient(blocking, TimeSpan.FromSeconds(1)).EnumerateActiveAsync(caller.Token));
        Assert.Equal(2, blocking.CallCount);
    }

    [Fact]
    public async Task Missing_configuration_or_delegated_identity_returns_503_without_transport()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("transport must not run"));
        Assert.Equal(
            503,
            (await CreateClient(handler, TimeSpan.Zero).EnumerateActiveAsync()).StatusCode);

        var noIdentity = new PlatformVerifiedMarketResolverClient(
            new HttpClient(handler),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Options.Create(ValidOptions()));
        Assert.Equal(503, (await noIdentity.EnumerateActiveAsync()).StatusCode);
        Assert.Empty(handler.Requests);
    }

    private static PlatformVerifiedMarketResolverClient CreateClient(
        HttpMessageHandler handler,
        TimeSpan? timeout = null)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "actor")], "Bearer"));
        context.Request.Headers.Authorization = "Bearer delegated-market-jwt";
        var options = ValidOptions();
        options.Timeout = timeout ?? TimeSpan.FromSeconds(1);
        return new(
            new HttpClient(handler),
            new HttpContextAccessor { HttpContext = context },
            Options.Create(options));
    }

    private static VerifiedMarketResolverOptions ValidOptions() => new()
    {
        PlatformBaseAddress = new Uri("https://platform.internal/"),
        Timeout = TimeSpan.FromSeconds(1),
        CredentialIdentifier = "resolver-id",
        CredentialSecret = "resolver-secret"
    };

    private static HttpResponseMessage Json(HttpStatusCode status, object value) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<RequestSnapshot> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new(
                request.Method,
                request.RequestUri,
                request.Headers.Authorization?.ToString(),
                request.Headers.ToDictionary(
                    x => x.Key,
                    x => x.Value.ToArray(),
                    StringComparer.OrdinalIgnoreCase),
                body));
            return responseFactory(request);
        }
    }

    private sealed record RequestSnapshot(
        HttpMethod Method,
        Uri? Uri,
        string? Authorization,
        IReadOnlyDictionary<string, string[]> Headers,
        string? Body);

    private sealed class BlockingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }
}
