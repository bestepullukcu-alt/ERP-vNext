using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Infrastructure.Workflow;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.Workflow;

public sealed class AuthProductIdentityWorkflowServiceIdentityProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Exact_workflow_identity_is_single_flight_cached_and_tenant_scoped()
    {
        var handler = new TokenHandler((request, _) => Success(request, ReadTenant(request)));
        var provider = Provider(handler);
        var tenant = Guid.NewGuid();

        var values = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => provider.GetAsync(tenant, false)));
        await provider.GetAsync(Guid.NewGuid(), false);

        Assert.Single(values.Select(x => x.AccessToken).Distinct(StringComparer.Ordinal));
        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task Active_401_uses_previous_only_inside_rotation_overlap()
    {
        var tenant = Guid.NewGuid();
        var handler = new TokenHandler((request, _) =>
            request.Headers.GetValues(AuthProductIdentityWorkflowServiceIdentityProvider.ClientSecretHeader).Single() == "active"
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : Success(request, tenant));
        var provider = Provider(handler, new()
        {
            AuthBaseUrl = "http://auth.test", ExpectedIssuer = "issuer", ClientId = "mdm-workflow",
            ActiveClientSecret = "active", PreviousClientSecret = "previous",
            PreviousClientSecretValidUntilUtc = Now.AddMinutes(1), RefreshSkewSeconds = 30
        });

        var identity = await provider.GetAsync(tenant, false);

        Assert.False(string.IsNullOrWhiteSpace(identity.AccessToken));
        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task Wrong_audience_or_claim_shape_is_terminal_and_never_discloses_secret()
    {
        var tenant = Guid.NewGuid();
        var handler = new TokenHandler((request, _) => Success(request, tenant, "TRUSTED_AUDIT_SOURCE_INGEST"));
        var error = await Assert.ThrowsAsync<Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowServiceIdentityException>(
            () => Provider(handler).GetAsync(tenant, false));

        Assert.False(error.IsRetryable);
        Assert.Equal("PRODUCT_WORKFLOW_IDENTITY_RESPONSE_INVALID", error.ErrorCode);
        Assert.DoesNotContain("active-secret", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forced_refresh_reacquires_a_distinct_token_once()
    {
        var handler = new TokenHandler((request, count) => Success(request, ReadTenant(request), audience: null, jti: GuidFrom(count)));
        var provider = Provider(handler);
        var tenant = Guid.NewGuid();
        var first = await provider.GetAsync(tenant, false);
        var second = await provider.GetAsync(tenant, true);

        Assert.NotEqual(first.AccessToken, second.AccessToken);
        Assert.Equal(2, handler.Count);
    }

    private static Guid GuidFrom(int value) => new(value, 0, 0, new byte[8]);
    private static AuthProductIdentityWorkflowServiceIdentityProvider Provider(HttpMessageHandler handler, AuthProductIdentityWorkflowServiceIdentityProviderOptions? options = null) =>
        new(new Factory(handler), Options.Create(options ?? new()
        {
            AuthBaseUrl = "http://auth.test", ExpectedIssuer = "issuer", ClientId = "mdm-workflow",
            ActiveClientSecret = "active-secret", RefreshSkewSeconds = 30
        }), new Clock());

    private static HttpResponseMessage Success(HttpRequestMessage request, Guid tenant, string? audience = null, Guid? jti = null)
    {
        Assert.Equal("http://auth.test/api/internal/v1/auth/service-tokens/issue", request.RequestUri!.ToString());
        Assert.Equal("mdm-workflow", request.Headers.GetValues(AuthProductIdentityWorkflowServiceIdentityProvider.ClientIdHeader).Single());
        var token = Jwt(tenant, audience ?? AuthProductIdentityWorkflowServiceIdentityProvider.Audience, jti ?? Guid.NewGuid());
        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                data = new { accessToken = token, tokenType = "Bearer", expiresIn = 300, expiresAtUtc = Now.AddSeconds(300) },
                statusCode = 200, isSuccessful = true, errors = Array.Empty<string>(), errorCodes = Array.Empty<string>()
            }), Encoding.UTF8, "application/json")
        };
    }

    private static Guid ReadTenant(HttpRequestMessage request)
    {
        using var json = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
        Assert.Equal(AuthProductIdentityWorkflowServiceIdentityProvider.Audience, json.RootElement.GetProperty("audience").GetString());
        return json.RootElement.GetProperty("tenantId").GetGuid();
    }

    private static string Jwt(Guid tenant, string audience, Guid jti)
    {
        var header = JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", kid = "key", typ = "JWT" });
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["sub"] = Guid.NewGuid().ToString("D"), ["actor_type"] = "service", ["service_name"] = "Diten.MDM",
            ["tenant_id"] = tenant.ToString("D"), ["jti"] = jti.ToString("D"), ["iat"] = Now.ToUnixTimeSeconds(),
            ["nbf"] = Now.ToUnixTimeSeconds(), ["exp"] = Now.AddSeconds(300).ToUnixTimeSeconds(), ["iss"] = "issuer", ["aud"] = audience
        });
        return $"{Encode(header)}.{Encode(payload)}.signature";
    }
    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class TokenHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private int _count;
        public int Count => _count;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request, Interlocked.Increment(ref _count)));
    }
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
