using System.Net;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Infrastructure.ReferenceData;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.ReferenceData;

public sealed class AuthWorkflowVerifiedMarketServiceIdentityProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAsync_issues_exact_purpose_and_caches_per_tenant()
    {
        var tenant = Guid.NewGuid();
        var handler = new CaptureHandler(request => Success(request, tenant));
        var provider = Provider(handler);

        var first = await provider.GetAsync(tenant, false);
        var replay = await provider.GetAsync(tenant, false);

        Assert.Equal(first, replay);
        var sent = Assert.Single(handler.Requests);
        Assert.Contains("\"audience\":\"TRUSTED_REFERENCE_DATA_CONSUMER\"", sent.Body, StringComparison.Ordinal);
        Assert.Contains($"\"tenantId\":\"{tenant:D}\"", sent.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("mdm-reference", sent.ClientId);
        Assert.Equal("active", sent.Secret);
    }

    [Fact]
    public async Task GetAsync_force_refresh_replaces_rejected_token_once()
    {
        var tenant = Guid.NewGuid();
        var sequence = 0;
        var handler = new CaptureHandler(request => Success(request, tenant, Guid.Parse(
            Interlocked.Increment(ref sequence) == 1
                ? "11111111-1111-1111-1111-111111111111"
                : "22222222-2222-2222-2222-222222222222")));
        var provider = Provider(handler);

        var first = await provider.GetAsync(tenant, false);
        var refreshed = await provider.GetAsync(tenant, true);

        Assert.NotEqual(first.AccessToken, refreshed.AccessToken);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetAsync_active_unauthorized_uses_previous_only_inside_rotation_window()
    {
        var tenant = Guid.NewGuid();
        var handler = new CaptureHandler(request => request.Secret == "active"
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : Success(request, tenant));
        var options = new AuthWorkflowVerifiedMarketServiceIdentityProviderOptions
        {
            AuthBaseUrl = "https://auth.internal/",
            ExpectedIssuer = "https://auth.internal",
            ClientId = "mdm-reference",
            ActiveClientSecret = "active",
            RefreshSkewSeconds = 30,
            PreviousClientSecret = "previous",
            PreviousClientSecretValidUntilUtc = Now.AddMinutes(5)
        };

        var result = await Provider(handler, options).GetAsync(tenant, false);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal(["active", "previous"], handler.Requests.Select(x => x.Secret));
    }

    [Fact]
    public async Task GetAsync_rejects_wrong_purpose_token()
    {
        var tenant = Guid.NewGuid();
        var handler = new CaptureHandler(request => Success(
            request, tenant, audience: "TRUSTED_WORKFLOW_CONSUMER"));

        var error = await Assert.ThrowsAsync<Application.Contracts.ReferenceData.WorkflowVerifiedMarketServiceIdentityException>(
            () => Provider(handler).GetAsync(tenant, false));

        Assert.Equal("REFERENCE_WORKFLOW_IDENTITY_RESPONSE_INVALID", error.ErrorCode);
        Assert.False(error.IsRetryable);
    }

    private static AuthWorkflowVerifiedMarketServiceIdentityProvider Provider(
        HttpMessageHandler handler,
        AuthWorkflowVerifiedMarketServiceIdentityProviderOptions? options = null) => new(
        new Factory(handler),
        Options.Create(options ?? ValidOptions()),
        new FixedTimeProvider(Now));

    private static AuthWorkflowVerifiedMarketServiceIdentityProviderOptions ValidOptions() => new()
    {
        AuthBaseUrl = "https://auth.internal/",
        ExpectedIssuer = "https://auth.internal",
        ClientId = "mdm-reference",
        ActiveClientSecret = "active",
        RefreshSkewSeconds = 30
    };

    private static HttpResponseMessage Success(
        RequestSnapshot request,
        Guid tenant,
        Guid? jti = null,
        string? audience = null)
    {
        var token = Jwt(tenant, audience ?? AuthWorkflowVerifiedMarketServiceIdentityProvider.Audience, jti ?? Guid.NewGuid());
        return Json(HttpStatusCode.OK, new
        {
            data = new { accessToken = token, tokenType = "Bearer", expiresIn = 300, expiresAtUtc = Now.AddSeconds(300) },
            statusCode = 200,
            isSuccessful = true,
            errors = Array.Empty<string>(),
            errorCodes = Array.Empty<string>()
        });
    }

    private static string Jwt(Guid tenant, string audience, Guid jti)
    {
        var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", kid = "kid", typ = "JWT" }));
        var now = Now.ToUnixTimeSeconds();
        var payload = Encode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            sub = Guid.NewGuid().ToString("D"), actor_type = "service", service_name = "Diten.MDM",
            tenant_id = tenant.ToString("D"), jti = jti.ToString("D"), iat = now, nbf = now,
            exp = now + 300, iss = "https://auth.internal", aud = audience
        }));
        return $"{header}.{payload}.signature";
    }

    private static string Encode(byte[] value) => Convert.ToBase64String(value)
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static HttpResponseMessage Json(HttpStatusCode status, object value) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
    };

    private sealed class CaptureHandler(Func<RequestSnapshot, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<RequestSnapshot> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var snapshot = new RequestSnapshot(
                await request.Content!.ReadAsStringAsync(cancellationToken),
                request.Headers.GetValues(AuthWorkflowVerifiedMarketServiceIdentityProvider.ClientIdHeader).Single(),
                request.Headers.GetValues(AuthWorkflowVerifiedMarketServiceIdentityProvider.ClientSecretHeader).Single());
            Requests.Add(snapshot);
            return response(snapshot);
        }
    }

    private sealed record RequestSnapshot(string Body, string ClientId, string Secret);
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
