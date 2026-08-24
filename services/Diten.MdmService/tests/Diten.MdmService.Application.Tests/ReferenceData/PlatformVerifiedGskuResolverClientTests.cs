using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Infrastructure.ReferenceData;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.ReferenceData;

public sealed class PlatformVerifiedGskuResolverClientTests
{
    [Fact]
    public async Task VerifiedMinimumResponse_IsMappedWithoutFallback()
    {
        var versionId = Guid.NewGuid();
        var handler = new ResolverRecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                isSuccessful = true,
                data = new
                {
                    selections = new object[]
                    {
                        new { set_code = "pack-applicability", value_code = "SCALAR_QUANTITY_APPLIES", catalog_version_id = versionId, catalog_version_number = 1, resolution_mode = "LATEST", resolved_at_utc = DateTimeOffset.Parse("2026-08-05T00:00:00Z"), is_retired = false, selectable_for_new = true },
                        new { set_code = "uom", value_code = "KGM", catalog_version_id = versionId, catalog_version_number = 1, resolution_mode = "LATEST", resolved_at_utc = DateTimeOffset.Parse("2026-08-05T00:00:00Z"), is_retired = false, selectable_for_new = true }
                    }
                }
            }), Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var result = await client.ResolveLatestAsync("SCALAR_QUANTITY_APPLIES", "KGM");

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Selections.Count);
        Assert.Equal(versionId, result.Selections[0].CatalogVersionId);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "REFERENCE_UNAUTHENTICATED")]
    [InlineData(HttpStatusCode.Forbidden, "REFERENCE_FORBIDDEN")]
    [InlineData(HttpStatusCode.NotFound, "REFERENCE_SET_NOT_ACCESSIBLE")]
    [InlineData(HttpStatusCode.Conflict, "REFERENCE_VALUE_RETIRED")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "REFERENCE_PROVIDER_UNAVAILABLE")]
    [InlineData(HttpStatusCode.GatewayTimeout, "REFERENCE_PROVIDER_TIMEOUT")]
    public async Task ProviderFailure_IsMappedWithoutRetry(HttpStatusCode status, string reason)
    {
        var handler = new ResolverRecordingHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { isSuccessful = false, reason_code = reason }),
                Encoding.UTF8,
                "application/json")
        });

        var result = await CreateClient(handler).ResolveLatestAsync("SCALAR_QUANTITY_APPLIES", "KGM");

        Assert.False(result.IsSuccessful);
        Assert.Equal((int)status, result.StatusCode);
        Assert.Equal(reason, result.FailureCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task MalformedSuccessfulEvidence_IsAContractMismatchWithoutRetry()
    {
        var handler = new ResolverRecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"isSuccessful\":true,\"data\":{\"selections\":[]}}", Encoding.UTF8, "application/json")
        });

        var result = await CreateClient(handler).ResolveLatestAsync("SCALAR_QUANTITY_APPLIES", "KGM");

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("REFERENCE_CONTRACT_MISMATCH", result.FailureCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task BoundedTimeout_ReturnsStable504WithoutRetry()
    {
        var handler = new ResolverBlockingHandler();

        var result = await CreateClient(handler, timeout: TimeSpan.FromMilliseconds(25))
            .ResolveLatestAsync("SCALAR_QUANTITY_APPLIES", "KGM");

        Assert.Equal(504, result.StatusCode);
        Assert.Equal("REFERENCE_PROVIDER_TIMEOUT", result.FailureCode);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task CallerCancellation_IsPropagatedWithoutRetry()
    {
        var handler = new ResolverBlockingHandler();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateClient(handler).ResolveLatestAsync(
                "SCALAR_QUANTITY_APPLIES", "KGM", cancellation.Token));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Verified_uom_enumeration_is_bounded_and_forwards_no_body()
    {
        var handler = new ResolverRecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                isSuccessful = true,
                data = new
                {
                    uoms = new object[]
                    {
                        new { code = "C62", display_text = "One", sort_order = 10, maximum_decimal_precision = 0 },
                        new { code = "GRM", display_text = "Gram", sort_order = 20, maximum_decimal_precision = 3 },
                        new { code = "KGM", display_text = "Kilogram", sort_order = 30, maximum_decimal_precision = 3 },
                        new { code = "MLT", display_text = "Millilitre", sort_order = 40, maximum_decimal_precision = 3 },
                        new { code = "LTR", display_text = "Litre", sort_order = 50, maximum_decimal_precision = 3 }
                    }
                }
            }), Encoding.UTF8, "application/json")
        });

        var result = await CreateClient(handler).EnumerateUomsAsync();

        Assert.True(result.IsSuccessful);
        Assert.Equal(["C62", "GRM", "KGM", "MLT", "LTR"], result.Uoms.Select(x => x.Code));
        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("/api/internal/v1/reference-data/verified-gsku/enumerate-uom", request.Uri!.AbsoluteUri);
        Assert.Null(request.Body);
        Assert.Equal("Bearer delegated-user-jwt", request.Authorization);
    }

    [Fact]
    public async Task Malformed_or_extended_uom_catalog_fails_closed()
    {
        var handler = new ResolverRecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"isSuccessful\":true,\"data\":{\"uoms\":[{\"code\":\"C62\",\"display_text\":\"One\",\"sort_order\":10,\"maximum_decimal_precision\":0}]}}",
                Encoding.UTF8,
                "application/json")
        });

        var result = await CreateClient(handler).EnumerateUomsAsync();

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("REFERENCE_CONTRACT_MISMATCH", result.FailureCode);
    }

    internal static PlatformVerifiedGskuResolverClient CreateClient(
        HttpMessageHandler handler,
        DefaultHttpContext? context = null,
        TimeSpan? timeout = null)
    {
        context ??= CreateAuthenticatedContext();
        var accessor = new HttpContextAccessor { HttpContext = context };
        return new PlatformVerifiedGskuResolverClient(
            new HttpClient(handler),
            accessor,
            Options.Create(new VerifiedGskuResolverOptions
            {
                PlatformBaseAddress = new Uri("https://platform.internal/"),
                Timeout = timeout ?? TimeSpan.FromSeconds(1),
                CredentialIdentifier = "resolver-id",
                CredentialSecret = "resolver-secret"
            }));
    }

    internal static DefaultHttpContext CreateAuthenticatedContext(string token = "delegated-user-jwt")
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user")], "Bearer"));
        context.Request.Headers.Authorization = $"Bearer {token}";
        return context;
    }
}

internal sealed class ResolverRecordingHandler(
    Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
{
    public List<HttpRequestMessageSnapshot> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new HttpRequestMessageSnapshot(
            request.RequestUri,
            request.Headers.Authorization?.ToString(),
            request.Headers.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
            body));
        return responseFactory(request);
    }
}

internal sealed record HttpRequestMessageSnapshot(
    Uri? Uri,
    string? Authorization,
    IReadOnlyDictionary<string, string[]> Headers,
    string? Body);

internal sealed class ResolverBlockingHandler : HttpMessageHandler
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
