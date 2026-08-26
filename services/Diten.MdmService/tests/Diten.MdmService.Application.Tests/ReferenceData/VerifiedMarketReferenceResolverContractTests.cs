using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Infrastructure.ReferenceData;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.ReferenceData;

public sealed class VerifiedMarketReferenceResolverContractTests
{
    [Fact]
    public async Task Client_posts_exact_contract_and_maps_six_field_evidence()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var versionId = Guid.NewGuid();
        var resolvedAt = DateTimeOffset.UtcNow;
        var handler = new StubHandler(async (request, _) =>
        {
            captured = request;
            body = await request.Content!.ReadAsStringAsync();
            return Json(HttpStatusCode.OK, new
            {
                isSuccessful = true,
                data = new
                {
                    market = new
                    {
                        set_code = "market",
                        value_code = "TR",
                        catalog_version_id = versionId,
                        catalog_version_number = 7,
                        resolution_mode = "LATEST",
                        resolved_at_utc = resolvedAt
                    }
                }
            });
        });

        var result = await CreateClient(handler).ResolveLatestAsync("TR");

        Assert.True(result.IsSuccessful, result.FailureCode);
        Assert.Equal("/api/internal/v1/reference-data/verified-market/resolve", captured!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, captured.Method);
        using var document = JsonDocument.Parse(body!);
        Assert.Equal(["market_code"], document.RootElement.EnumerateObject().Select(x => x.Name));
        Assert.Equal("TR", document.RootElement.GetProperty("market_code").GetString());
        Assert.False(captured.Headers.Contains("X-Tenant-Id"));
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme);
        Assert.Equal("delegated-token", captured.Headers.Authorization.Parameter);
        Assert.Equal("resolver-id", Assert.Single(captured.Headers.GetValues("X-Verified-Gsku-Credential-Id")));
        Assert.Equal("resolver-secret", Assert.Single(captured.Headers.GetValues("X-Verified-Gsku-Credential")));
        Assert.Equal("market", result.Selection!.SetCode);
        Assert.Equal("TR", result.Selection.ValueCode);
        Assert.Equal(versionId, result.Selection.CatalogVersionId);
        Assert.Equal(7, result.Selection.CatalogVersionNumber);
        Assert.Equal("LATEST", result.Selection.ResolutionMode);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "REFERENCE_MARKET_NOT_FOUND", 404)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "REFERENCE_PROVIDER_UNAVAILABLE", 503)]
    [InlineData(HttpStatusCode.GatewayTimeout, "REFERENCE_PROVIDER_TIMEOUT", 504)]
    [InlineData(HttpStatusCode.Unauthorized, "REFERENCE_UNAUTHENTICATED", 503)]
    [InlineData(HttpStatusCode.Conflict, "REFERENCE_CONTRACT_MISMATCH", 503)]
    public async Task Provider_failures_collapse_to_exact_fail_closed_classes(
        HttpStatusCode providerStatus,
        string reason,
        int expectedStatus)
    {
        var result = await CreateClient(new StubHandler((_, _) => Task.FromResult(
            Json(providerStatus, new { isSuccessful = false, reason_code = reason }))))
            .ResolveLatestAsync("TR");

        Assert.False(result.IsSuccessful);
        Assert.Equal(expectedStatus, result.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, 404)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
    [InlineData(HttpStatusCode.GatewayTimeout, 504)]
    public async Task Empty_failure_body_preserves_exact_transport_class(
        HttpStatusCode providerStatus,
        int expectedStatus)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(providerStatus)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        }));

        var result = await CreateClient(handler).ResolveLatestAsync("TR");

        Assert.False(result.IsSuccessful);
        Assert.Equal(expectedStatus, result.StatusCode);
    }

    [Theory]
    [InlineData("market", "tr", 1, "LATEST")]
    [InlineData("country", "TR", 1, "LATEST")]
    [InlineData("market", "TR", 0, "LATEST")]
    [InlineData("market", "TR", 1, "PINNED")]
    public async Task Malformed_or_mismatched_success_evidence_fails_closed(
        string setCode,
        string valueCode,
        int version,
        string mode)
    {
        var response = new
        {
            isSuccessful = true,
            data = new
            {
                market = new
                {
                    set_code = setCode,
                    value_code = valueCode,
                    catalog_version_id = Guid.NewGuid(),
                    catalog_version_number = version,
                    resolution_mode = mode,
                    resolved_at_utc = DateTimeOffset.UtcNow
                }
            }
        };
        var result = await CreateClient(new StubHandler((_, _) => Task.FromResult(Json(HttpStatusCode.OK, response))))
            .ResolveLatestAsync("TR");

        Assert.False(result.IsSuccessful);
        Assert.Equal(503, result.StatusCode);
        Assert.Null(result.Selection);
    }

    [Fact]
    public async Task Timeout_maps_to_504_while_caller_cancellation_propagates()
    {
        var slow = new StubHandler(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), token);
            return Json(HttpStatusCode.OK, new { });
        });
        var timeoutResult = await CreateClient(slow, TimeSpan.FromMilliseconds(20)).ResolveLatestAsync("TR");
        Assert.Equal(504, timeoutResult.StatusCode);

        using var caller = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateClient(slow, TimeSpan.FromSeconds(1)).ResolveLatestAsync("TR", caller.Token));
    }

    [Fact]
    public async Task Missing_configuration_or_delegated_identity_is_503_without_transport()
    {
        var handler = new StubHandler((_, _) => throw new InvalidOperationException("transport must not run"));
        var noConfiguration = CreateClient(handler, TimeSpan.Zero);
        Assert.Equal(503, (await noConfiguration.ResolveLatestAsync("TR")).StatusCode);

        var context = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var options = Options.Create(ValidOptions());
        var noIdentity = new PlatformVerifiedMarketResolverClient(new HttpClient(handler), context, options);
        Assert.Equal(503, (await noIdentity.ResolveLatestAsync("TR")).StatusCode);
    }

    private static PlatformVerifiedMarketResolverClient CreateClient(
        HttpMessageHandler handler,
        TimeSpan? timeout = null)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "actor")], "test"));
        context.Request.Headers.Authorization = "Bearer delegated-token";
        var options = ValidOptions();
        options.Timeout = timeout ?? TimeSpan.FromSeconds(1);
        return new PlatformVerifiedMarketResolverClient(
            new HttpClient(handler),
            new HttpContextAccessor { HttpContext = context },
            Options.Create(options));
    }

    private static VerifiedMarketResolverOptions ValidOptions() => new()
    {
        PlatformBaseAddress = new Uri("https://platform.test/"),
        Timeout = TimeSpan.FromSeconds(1),
        CredentialIdentifier = "resolver-id",
        CredentialSecret = "resolver-secret"
    };

    private static HttpResponseMessage Json(HttpStatusCode status, object value) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
