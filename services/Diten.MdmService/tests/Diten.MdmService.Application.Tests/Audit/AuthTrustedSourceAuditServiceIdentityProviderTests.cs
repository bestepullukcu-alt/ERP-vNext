using System.Collections.Concurrent;
using System.Collections;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Reflection;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;
using Diten.MdmService.Infrastructure.Audit;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.Audit;

public sealed class AuthTrustedSourceAuditServiceIdentityProviderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Concurrent_same_tenant_requests_are_single_flight_and_cached()
    {
        var tenant = Guid.NewGuid();
        var handler = new TokenHandler((request, _) => Success(request, tenant));
        var provider = Provider(handler);

        var identities = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            provider.GetAsync(tenant, AuditIntentDeliveryProcessor.RequiredAudience, false)));

        Assert.Single(identities.Select(item => item.AccessToken).Distinct(StringComparer.Ordinal));
        Assert.Equal(1, handler.Count);
        _ = await provider.GetAsync(tenant, AuditIntentDeliveryProcessor.RequiredAudience, false);
        Assert.Equal(1, handler.Count);
    }

    [Fact]
    public async Task Cache_isolated_by_tenant_and_force_refresh_reacquires_once()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var handler = new TokenHandler((request, _) => Success(request, ReadTenant(request)));
        var provider = Provider(handler);

        await provider.GetAsync(tenantA, AuditIntentDeliveryProcessor.RequiredAudience, false);
        await provider.GetAsync(tenantB, AuditIntentDeliveryProcessor.RequiredAudience, false);
        await provider.GetAsync(tenantA, AuditIntentDeliveryProcessor.RequiredAudience, true);

        Assert.Equal(3, handler.Count);
        Assert.Equal([tenantA, tenantB, tenantA], handler.Tenants);
    }

    [Fact]
    public async Task Active_unauthorized_uses_bounded_previous_credential_overlap()
    {
        var tenant = Guid.NewGuid();
        var handler = new TokenHandler((request, _) =>
            request.Headers.GetValues(AuthTrustedSourceAuditServiceIdentityProvider.ClientSecretHeader).Single() == "active-secret"
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : Success(request, tenant));
        var provider = Provider(handler, new AuthTrustedSourceAuditServiceIdentityProviderOptions
        {
            AuthBaseUrl = "https://auth.test",
            ExpectedIssuer = "issuer",
            ClientId = "mdm-client",
            ActiveClientSecret = "active-secret",
            PreviousClientSecret = "previous-secret",
            PreviousClientSecretValidUntilUtc = Now.AddMinutes(5),
            RefreshSkewSeconds = 30
        });

        var identity = await provider.GetAsync(tenant, AuditIntentDeliveryProcessor.RequiredAudience, false);

        Assert.False(string.IsNullOrWhiteSpace(identity.AccessToken));
        Assert.Equal(2, handler.Count);
    }

    [Theory]
    [InlineData("issuer", 0)]
    [InlineData(" issuer", 30)]
    [InlineData("issuer", 121)]
    public void Invalid_issuer_or_refresh_skew_is_rejected_before_http(string issuer, int skew)
    {
        var error = Assert.Throws<TrustedSourceAuditServiceIdentityException>(() =>
            AuthTrustedSourceAuditServiceIdentityProvider.EnsureValidConfiguration(new()
            {
                AuthBaseUrl = "https://auth.test",
                ExpectedIssuer = issuer,
                ClientId = "mdm-client",
                ActiveClientSecret = "active-secret",
                RefreshSkewSeconds = skew
            }));

        Assert.Equal("AUDIT_SOURCE_IDENTITY_CONFIGURATION_INVALID", error.ErrorCode);
    }

    [Fact]
    public void Configuration_errors_never_disclose_secret()
    {
        var error = Assert.Throws<TrustedSourceAuditServiceIdentityException>(() =>
            AuthTrustedSourceAuditServiceIdentityProvider.EnsureValidConfiguration(new()
            {
                AuthBaseUrl = "https://auth.test/path",
                ExpectedIssuer = "issuer",
                ClientId = "mdm-client",
                ActiveClientSecret = "do-not-leak",
                RefreshSkewSeconds = 30
            }));
        Assert.DoesNotContain("do-not-leak", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void External_plaintext_auth_url_is_rejected_before_secret_transport()
    {
        var error = Assert.Throws<TrustedSourceAuditServiceIdentityException>(() =>
            AuthTrustedSourceAuditServiceIdentityProvider.EnsureValidConfiguration(new()
            {
                AuthBaseUrl = "http://auth.internal",
                ExpectedIssuer = "issuer",
                ClientId = "mdm-client",
                ActiveClientSecret = "do-not-send",
                RefreshSkewSeconds = 30
            }));

        Assert.Equal("AUDIT_SOURCE_IDENTITY_CONFIGURATION_INVALID", error.ErrorCode);
        Assert.DoesNotContain("do-not-send", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Caller_cancellation_is_observed_while_token_request_is_blocked()
    {
        var provider = Provider(new BlockingHandler());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetAsync(
            Guid.NewGuid(), AuditIntentDeliveryProcessor.RequiredAudience, false, cancellation.Token));
    }

    [Fact]
    public async Task Expired_many_tenant_cache_and_coordination_entries_are_pruned()
    {
        var clock = new MutableClock(Now);
        var handler = new TokenHandler((request, _) => Success(request, ReadTenant(request), clock.GetUtcNow()));
        var provider = Provider(handler, clock: clock);
        foreach (var tenant in Enumerable.Range(0, 25).Select(_ => Guid.NewGuid()))
            await provider.GetAsync(tenant, AuditIntentDeliveryProcessor.RequiredAudience, false);

        Assert.Equal(25, DictionaryCount(provider, "_cache"));
        clock.Advance(TimeSpan.FromSeconds(301));
        await provider.GetAsync(Guid.NewGuid(), AuditIntentDeliveryProcessor.RequiredAudience, false);

        Assert.Equal(1, DictionaryCount(provider, "_cache"));
        Assert.InRange(DictionaryCount(provider, "_coordinators"), 0, 1);
    }

    [Fact]
    public async Task Expiry_cleanup_cannot_remove_a_borrowed_refresh_coordinator()
    {
        var clock = new MutableClock(Now);
        var handler = new CoordinatedRefreshHandler(clock);
        var provider = Provider(handler, clock: clock);
        var tenant = Guid.NewGuid();
        await provider.GetAsync(tenant, AuditIntentDeliveryProcessor.RequiredAudience, false);
        var (key, coordinator) = BorrowOnlyCoordinator(provider);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(301));
            await provider.GetAsync(Guid.NewGuid(), AuditIntentDeliveryProcessor.RequiredAudience, false);
            Assert.True(ContainsCoordinator(provider, key, coordinator));

            var first = provider.GetAsync(tenant, AuditIntentDeliveryProcessor.RequiredAudience, true);
            await handler.RefreshEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var second = provider.GetAsync(tenant, AuditIntentDeliveryProcessor.RequiredAudience, true);
            handler.ReleaseRefresh.TrySetResult();
            await Task.WhenAll(first, second);
            Assert.Equal(3, handler.Count);
        }
        finally
        {
            ReturnCoordinator(provider, key, coordinator);
        }
    }

    [Fact]
    public async Task Zero_ref_observation_cannot_retire_a_coordinator_rented_before_atomic_retirement()
    {
        var provider = Provider(new TokenHandler((request, _) => Success(request, ReadTenant(request))));
        await provider.GetAsync(Guid.NewGuid(), AuditIntentDeliveryProcessor.RequiredAudience, false);
        var coordinator = SingleCoordinator(provider);
        var type = coordinator.GetType();

        Assert.True((bool)type.GetMethod("TryRent")!.Invoke(coordinator, null)!);
        Assert.False((bool)type.GetMethod("TryRetire")!.Invoke(coordinator, null)!);
        Assert.Equal(0, (int)type.GetMethod("Return")!.Invoke(coordinator, null)!);
        Assert.True((bool)type.GetMethod("TryRetire")!.Invoke(coordinator, null)!);
        Assert.False((bool)type.GetMethod("TryRent")!.Invoke(coordinator, null)!);
    }

    private static AuthTrustedSourceAuditServiceIdentityProvider Provider(
        HttpMessageHandler handler,
        AuthTrustedSourceAuditServiceIdentityProviderOptions? options = null,
        TimeProvider? clock = null) => new(
        new Factory(handler),
        Options.Create(options ?? new AuthTrustedSourceAuditServiceIdentityProviderOptions
        {
            AuthBaseUrl = "https://auth.test",
            ExpectedIssuer = "issuer",
            ClientId = "mdm-client",
            ActiveClientSecret = "active-secret",
            RefreshSkewSeconds = 30
        }),
        clock ?? new FixedClock());

    private static HttpResponseMessage Success(HttpRequestMessage request, Guid tenant)
        => Success(request, tenant, Now);

    private static HttpResponseMessage Success(HttpRequestMessage request, Guid tenant, DateTimeOffset now)
    {
        Assert.Equal("https://auth.test/api/internal/v1/auth/service-tokens/issue", request.RequestUri!.ToString());
        Assert.Equal("mdm-client", request.Headers.GetValues(AuthTrustedSourceAuditServiceIdentityProvider.ClientIdHeader).Single());
        var token = Jwt(tenant, now);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                data = new { accessToken = token, tokenType = "Bearer", expiresIn = 300, expiresAtUtc = now.AddSeconds(300) },
                statusCode = 200,
                isSuccessful = true,
                errors = Array.Empty<string>(),
                errorCodes = Array.Empty<string>()
            }), Encoding.UTF8, "application/json")
        };
    }

    private static Guid ReadTenant(HttpRequestMessage request)
    {
        var json = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
        using var document = JsonDocument.Parse(json);
        Assert.Equal(AuditIntentDeliveryProcessor.RequiredAudience, document.RootElement.GetProperty("audience").GetString());
        return document.RootElement.GetProperty("tenantId").GetGuid();
    }

    private static string Jwt(Guid tenant, DateTimeOffset? issuedAt = null)
    {
        var now = issuedAt ?? Now;
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["sub"] = Guid.NewGuid().ToString("D"), ["actor_type"] = "service",
            ["service_name"] = "Diten.MDM", ["tenant_id"] = tenant.ToString("D"),
            ["jti"] = Guid.NewGuid().ToString("D"), ["iat"] = now.ToUnixTimeSeconds(),
            ["nbf"] = now.ToUnixTimeSeconds(), ["exp"] = now.AddSeconds(300).ToUnixTimeSeconds(),
            ["iss"] = "issuer", ["aud"] = AuditIntentDeliveryProcessor.RequiredAudience
        });
        var header = JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "RS256",
            kid = "test-key",
            typ = "JWT"
        });
        return $"{Encode(header)}.{Encode(payload)}.signature";
    }

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class TokenHandler(Func<HttpRequestMessage, int, HttpResponseMessage> response) : HttpMessageHandler
    {
        private int _count;
        public int Count => _count;
        public ConcurrentQueue<Guid> TenantQueue { get; } = new();
        public Guid[] Tenants => TenantQueue.ToArray();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref _count);
            if (request.Content is not null) TenantQueue.Enqueue(ReadTenant(request));
            return Task.FromResult(response(request, count));
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("UNREACHABLE");
        }
    }

    private sealed class CoordinatedRefreshHandler(MutableClock clock) : HttpMessageHandler
    {
        private int _count;
        public int Count => _count;
        public TaskCompletionSource RefreshEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseRefresh { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref _count);
            if (count == 3)
            {
                RefreshEntered.TrySetResult();
                await ReleaseRefresh.Task.WaitAsync(cancellationToken);
            }
            return Success(request, ReadTenant(request), clock.GetUtcNow());
        }
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan value) => _now = _now.Add(value);
    }

    private static int DictionaryCount(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing field {fieldName}.");
        return (int)(field.GetValue(instance)?.GetType().GetProperty("Count")?.GetValue(field.GetValue(instance)!)
            ?? throw new InvalidOperationException($"Missing count for {fieldName}."));
    }

    private static (object Key, object Coordinator) BorrowOnlyCoordinator(object provider)
    {
        var dictionary = provider.GetType().GetField("_coordinators", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(provider)!;
        var keys = (IEnumerable)dictionary.GetType().GetProperty("Keys")!.GetValue(dictionary)!;
        var key = keys.Cast<object>().Single();
        var coordinator = provider.GetType().GetMethod("RentCoordinator", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(provider, [key])!;
        return (key, coordinator);
    }

    private static object SingleCoordinator(object provider)
    {
        var dictionary = provider.GetType().GetField("_coordinators", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(provider)!;
        var values = (IEnumerable)dictionary.GetType().GetProperty("Values")!.GetValue(dictionary)!;
        return values.Cast<object>().Single();
    }

    private static bool ContainsCoordinator(object provider, object key, object coordinator)
    {
        var dictionary = provider.GetType().GetField("_coordinators", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(provider)!;
        var tryGet = dictionary.GetType().GetMethod("TryGetValue")!;
        var arguments = new[] { key, null };
        return (bool)tryGet.Invoke(dictionary, arguments)! && ReferenceEquals(arguments[1], coordinator);
    }

    private static void ReturnCoordinator(object provider, object key, object coordinator) =>
        provider.GetType().GetMethod("ReturnCoordinator", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(provider, [key, coordinator]);
}
