using System.Net;
using System.Text;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Services.Auth;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.TenantOrganization;

// MOD-0288-FU01 focused tests. Mirrors MdmLegalEntityReferenceValidatorTests.
//
// ⚠ CORRECTED 2026-08-28. This file used to say X-Tenant-Id was "handled by the shared TenantPropagationHandler
// (registered in DI) and covered by its own behavior". Both halves were false: the handler never added the header
// (IHttpClientFactory caches its chain in its own scope, so the handler's request-scoped ITenantContext is never
// resolved), and nothing here covered it — these tests build a bare HttpClient and never go through the factory,
// which is precisely why the defect survived. The validator now writes the header itself and the tests below pin
// that, including the pin that fails if anyone moves it back into a handler.
public sealed class AuthServiceUserReferenceValidatorTests
{
    private static readonly Guid UserId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
    private static readonly Guid TenantId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid TargetTenantId = Guid.Parse("30000000-0000-0000-0000-000000000003");

    // The platform login realm (PlatformLoginCommandHandler.PlatformTenantId) — a login realm, never a customer.
    private static readonly Guid PlatformSentinelTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Validate_returns_success_for_matching_referenceable_user()
    {
        var validator = CreateValidator(JsonResponse($$"""
            {"data":{"userId":"{{UserId}}","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
            """));

        var response = await validator.ValidateAsync(UserId);

        Assert.True(response.IsSuccessful);
        Assert.Equal(UserId, response.Data!.UserId);
        Assert.True(response.Data.Referenceable);
    }

    [Fact]
    public async Task Validate_fails_closed_on_http_non_success()
    {
        var validator = CreateValidator(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"data":null,"statusCode":404,"isSuccessful":false,"errors":["Not found"]}""", Encoding.UTF8, "application/json")
        });

        var response = await validator.ValidateAsync(UserId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_on_network_exception()
    {
        var validator = CreateValidator(_ => throw new HttpRequestException("network down"));

        var response = await validator.ValidateAsync(UserId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_on_timeout()
    {
        var validator = CreateValidator(_ => throw new TaskCanceledException("timeout"));

        var response = await validator.ValidateAsync(UserId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_on_malformed_json()
    {
        var validator = CreateValidator(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not-json", Encoding.UTF8, "application/json")
        });

        var response = await validator.ValidateAsync(UserId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_on_null_payload()
    {
        var validator = CreateValidator(JsonResponse("""{"data":null,"statusCode":200,"isSuccessful":true,"errors":[]}"""));

        var response = await validator.ValidateAsync(UserId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_on_user_id_mismatch()
    {
        var otherId = Guid.NewGuid();
        var validator = CreateValidator(JsonResponse($$"""
            {"data":{"userId":"{{otherId}}","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
            """));

        var response = await validator.ValidateAsync(UserId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_on_referenceable_false()
    {
        var validator = CreateValidator(JsonResponse($$"""
            {"data":{"userId":"{{UserId}}","referenceable":false},"statusCode":200,"isSuccessful":true,"errors":[]}
            """));

        var response = await validator.ValidateAsync(UserId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_when_envelope_not_successful()
    {
        var validator = CreateValidator(JsonResponse($$"""
            {"data":{"userId":"{{UserId}}","referenceable":true},"statusCode":404,"isSuccessful":false,"errors":["Not found"]}
            """));

        var response = await validator.ValidateAsync(UserId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_preserves_caller_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var validator = CreateValidator(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return JsonResponse("{}");
        });

        await Assert.ThrowsAsync<TaskCanceledException>(() => validator.ValidateAsync(UserId, cts.Token));
    }

    [Fact]
    public async Task Validate_forwards_bearer_token()
    {
        HttpRequestMessage? captured = null;
        var validator = CreateValidator((request, _) =>
        {
            captured = request;
            return Task.FromResult(JsonResponse($$"""
                {"data":{"userId":"{{UserId}}","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
                """));
        });

        await validator.ValidateAsync(UserId);

        Assert.NotNull(captured);
        Assert.NotNull(captured!.Headers.Authorization);
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme);
    }


    // ── Tenancy on the wire (2026-08-28) ──────────────────────────────────────
    //
    // These four are the guards for the defect that TenantPropagationHandler hid: the header was silently absent,
    // so AuthService resolved the caller's JWT tenant alone. For a platform caller that is the login SENTINEL realm,
    // where the record does not exist — an existing reference reported as "not found", with nothing logged.

    /// <summary>(a) A tenant user's call states that user's own tenant.</summary>
    [Fact]
    public async Task Tenant_context_puts_the_callers_own_tenant_on_the_wire()
    {
        HttpRequestMessage? captured = null;
        var validator = CreateValidator(
            (request, _) =>
            {
                captured = request;
                return Task.FromResult(JsonResponse($$"""
                    {"data":{"userId":"{{UserId}}","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
                    """));
            },
            TenantContextResolvedFor(TenantId));

        var response = await validator.ValidateAsync(UserId);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(captured);
        Assert.Equal(TenantId.ToString(), Assert.Single(captured!.Headers.GetValues("X-Tenant-Id")));
    }

    /// <summary>
    /// (b) A platform call states the TARGET tenant, never the sentinel realm its own token carries. Sending the
    /// sentinel is the wrong question; sending it alongside a differing JWT tenant is a hard 400 from MDM, which
    /// makes no exception for a platform actor because it never reads actor_type at all.
    /// </summary>
    [Fact]
    public async Task Platform_context_puts_the_target_tenant_on_the_wire_and_never_the_sentinel()
    {
        HttpRequestMessage? captured = null;
        var validator = CreateValidator(
            (request, _) =>
            {
                captured = request;
                return Task.FromResult(JsonResponse($$"""
                    {"data":{"userId":"{{UserId}}","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
                    """));
            },
            new PlatformActingForTenant(PlatformSentinelTenantId, TargetTenantId));

        var response = await validator.ValidateAsync(UserId);

        Assert.True(response.IsSuccessful);
        Assert.NotNull(captured);
        var sent = Assert.Single(captured!.Headers.GetValues("X-Tenant-Id"));
        Assert.Equal(TargetTenantId.ToString(), sent);
        Assert.NotEqual(PlatformSentinelTenantId.ToString(), sent);
    }

    /// <summary>
    /// (c) With no tenant to name, the call is NOT made and the reference fails closed. Asking about an
    /// unnamed tenant would be answered about whichever tenant the token happens to carry, which is how a real
    /// record came back "not found".
    /// </summary>
    [Theory]
    [InlineData(false)] // unresolved context
    [InlineData(true)]  // platform context with no declared target tenant (SetPlatformContext(Guid.Empty))
    public async Task No_tenant_to_name_means_no_call_and_a_closed_reference(bool platformWithoutTarget)
    {
        var called = false;
        ITenantContext context = platformWithoutTarget
            // A platform admin who has declared no target tenant — every TenantResolutionMiddleware platform
            // branch reaches here today, via SetPlatformContext(Guid.Empty).
            ? new PlatformActingForTenant(PlatformSentinelTenantId, Guid.Empty)
            : new TenantContext();

        var validator = CreateValidator(
            (_, _) =>
            {
                called = true;
                return Task.FromResult(JsonResponse($$"""
                    {"data":{"userId":"{{UserId}}","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
                    """));
            },
            context);

        var response = await validator.ValidateAsync(UserId);

        Assert.False(called);
        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    /// <summary>
    /// (d) The header is written by THIS class, from the request scope — not by a DelegatingHandler. The client
    /// below has no handler chain at all beyond the stub, so if anyone moves tenancy back into
    /// TenantPropagationHandler this fails. That is the whole point: the handler cannot see the request scope
    /// (IHttpClientFactory caches its chain in its own scope), so the move would be silent in production and
    /// invisible to every other test here. Precedent:
    /// HttpWorkItemBridgeTests.The_tenant_header_and_the_callers_own_bearer_token_reach_the_module.
    /// </summary>
    [Fact]
    public async Task Tenant_header_is_written_by_the_validator_and_not_by_a_delegating_handler()
    {
        HttpRequestMessage? captured = null;
        var stubOnly = new HttpClient(new StubHttpMessageHandler((request, _) =>
        {
            captured = request;
            return Task.FromResult(JsonResponse($$"""
                {"data":{"userId":"{{UserId}}","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
                """));
        }));

        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.Request.Headers.Authorization = "Bearer caller-token";

        var validator = new AuthServiceUserReferenceValidator(
            stubOnly,
            Options.Create(new AuthServiceOptions { BaseUrl = "http://auth.local" }),
            accessor,
            TenantContextResolvedFor(TenantId));

        await validator.ValidateAsync(UserId);

        Assert.NotNull(captured);
        Assert.Equal(TenantId.ToString(), Assert.Single(captured!.Headers.GetValues("X-Tenant-Id")));

        // The HUMAN's token travels with it: the module authorises the person, not Platform.
        Assert.Equal("caller-token", captured.Headers.Authorization?.Parameter);
    }

    private static AuthServiceUserReferenceValidator CreateValidator(HttpResponseMessage response) =>
        CreateValidator(_ => response);

    private static AuthServiceUserReferenceValidator CreateValidator(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        CreateValidator((request, _) => Task.FromResult(responseFactory(request)));

    private static AuthServiceUserReferenceValidator CreateValidator(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) =>
        CreateValidator(responseFactory, TenantContextResolvedFor(TenantId));

    private static AuthServiceUserReferenceValidator CreateValidator(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory,
        ITenantContext tenantContext)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(responseFactory));
        var options = Options.Create(new AuthServiceOptions { BaseUrl = "http://auth.local" });
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        accessor.HttpContext.Request.Headers.Authorization = "Bearer token";

        return new AuthServiceUserReferenceValidator(httpClient, options, accessor, tenantContext);
    }

    private static ITenantContext TenantContextResolvedFor(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        return context;
    }

    /// <summary>
    /// A platform context where the sentinel and the target are DIFFERENT values.
    ///
    /// ⚠ The production <c>TenantContext</c> cannot express this: <c>SetPlatformContext(target)</c> assigns
    /// <c>TenantId</c> AND <c>TargetTenantId</c> the same value, so the sentinel never enters the tenant context at
    /// all — it lives only in the token's <c>tenant_id</c> claim. That is exactly why this double exists: without
    /// it, "reads TargetTenantId" and "reads TenantId" are indistinguishable, and a revert to the wrong field
    /// would pass every test. MEASURED 2026-08-28; reported to CONTROL TOWER as an open question.
    /// </summary>
    private sealed class PlatformActingForTenant : ITenantContext
    {
        public PlatformActingForTenant(Guid sentinelTenantId, Guid? targetTenantId)
        {
            TenantId = sentinelTenantId;
            TargetTenantId = targetTenantId;
        }

        public Guid TenantId { get; }
        public bool IsResolved => true;
        public bool IsPlatformContext => true;
        public Guid? TargetTenantId { get; }

        public void SetTenant(Guid tenantId) => throw new NotSupportedException();
        public void SetPlatformContext(Guid targetTenantId) => throw new NotSupportedException();
        public void ClearTenant() => throw new NotSupportedException();
    }


    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _responseFactory(request, cancellationToken);
    }
}
