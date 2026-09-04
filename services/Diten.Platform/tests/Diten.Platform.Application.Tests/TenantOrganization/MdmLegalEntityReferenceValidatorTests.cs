using System.Net;
using System.Text;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure.Services.Mdm;
using Diten.Platform.Infrastructure.Services.Http;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.TenantOrganization;

public sealed class MdmLegalEntityReferenceValidatorTests
{
    private static readonly Guid LegalEntityId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid TargetTenantId = Guid.Parse("30000000-0000-0000-0000-000000000003");

    // The platform login realm (PlatformLoginCommandHandler.PlatformTenantId). It is a login realm, not a
    // customer: it must never be what a reference check asks MDM about.
    private static readonly Guid PlatformSentinelTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Validate_returns_success_for_matching_active_referenceable_response()
    {
        var validator = CreateValidator(JsonResponse($$"""
            {"data":{"legalEntityId":"{{LegalEntityId}}","legalName":"Legal","displayName":"Legal","lifecycleState":"ACTIVE","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
            """));

        var response = await validator.ValidateAsync(LegalEntityId);

        Assert.True(response.IsSuccessful);
        Assert.Equal(LegalEntityId, response.Data!.LegalEntityId);
    }

    [Fact]
    public async Task Validate_fails_closed_on_network_exception()
    {
        var validator = CreateValidator(_ => throw new HttpRequestException("network down"));

        var response = await validator.ValidateAsync(LegalEntityId);

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

        var response = await validator.ValidateAsync(LegalEntityId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_on_null_payload()
    {
        var validator = CreateValidator(JsonResponse("""{"data":null,"statusCode":200,"isSuccessful":true,"errors":[]}"""));

        var response = await validator.ValidateAsync(LegalEntityId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Validate_fails_closed_on_legal_entity_id_mismatch()
    {
        var otherId = Guid.NewGuid();
        var validator = CreateValidator(JsonResponse($$"""
            {"data":{"legalEntityId":"{{otherId}}","legalName":"Legal","displayName":"Legal","lifecycleState":"ACTIVE","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
            """));

        var response = await validator.ValidateAsync(LegalEntityId);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Theory]
    [InlineData("DRAFT", true)]
    [InlineData("ARCHIVED", true)]
    [InlineData("ACTIVE", false)]
    public async Task Validate_fails_closed_on_non_active_or_non_referenceable_payload(string lifecycleState, bool referenceable)
    {
        var validator = CreateValidator(JsonResponse($$"""
            {"data":{"legalEntityId":"{{LegalEntityId}}","legalName":"Legal","displayName":"Legal","lifecycleState":"{{lifecycleState}}","referenceable":{{referenceable.ToString().ToLowerInvariant()}}},"statusCode":200,"isSuccessful":true,"errors":[]}
            """));

        var response = await validator.ValidateAsync(LegalEntityId);

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

        await Assert.ThrowsAsync<TaskCanceledException>(() => validator.ValidateAsync(LegalEntityId, cts.Token));
    }


    // ── Tenancy on the wire (2026-08-28) ──────────────────────────────────────
    //
    // These four are the guards for the defect that a shared tenant-propagation DelegatingHandler hid (detached
    // in BL-311, class deleted in BL-316): the header was silently absent,
    // so MDM resolved the caller's JWT tenant alone. For a platform caller that is the login SENTINEL realm,
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
                    {"data":{"legalEntityId":"{{LegalEntityId}}","legalName":"Legal","displayName":"Legal","lifecycleState":"ACTIVE","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
                    """));
            },
            TenantContextResolvedFor(TenantId));

        var response = await validator.ValidateAsync(LegalEntityId);

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
                    {"data":{"legalEntityId":"{{LegalEntityId}}","legalName":"Legal","displayName":"Legal","lifecycleState":"ACTIVE","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
                    """));
            },
            new PlatformActingForTenant(PlatformSentinelTenantId, TargetTenantId));

        var response = await validator.ValidateAsync(LegalEntityId);

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
                    {"data":{"legalEntityId":"{{LegalEntityId}}","legalName":"Legal","displayName":"Legal","lifecycleState":"ACTIVE","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
                    """));
            },
            context);

        var response = await validator.ValidateAsync(LegalEntityId);

        Assert.False(called);
        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    /// <summary>
    /// (d) The header is written by THIS class, from the request scope — not by a DelegatingHandler. The client
    /// below has no handler chain at all beyond the stub, so if anyone moves tenancy back into
    /// a DelegatingHandler this fails. That is the whole point: such a handler cannot see the request scope
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
                {"data":{"legalEntityId":"{{LegalEntityId}}","legalName":"Legal","displayName":"Legal","lifecycleState":"ACTIVE","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
                """));
        }));

        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.Request.Headers.Authorization = "Bearer caller-token";

        var validator = new MdmLegalEntityReferenceValidator(
            stubOnly,
            Options.Create(new MdmServiceOptions { BaseUrl = "http://mdm.local" }),
            accessor,
            TenantContextResolvedFor(TenantId),
            new RecordingLogger<MdmLegalEntityReferenceValidator>());

        await validator.ValidateAsync(LegalEntityId);

        Assert.NotNull(captured);
        Assert.Equal(TenantId.ToString(), Assert.Single(captured!.Headers.GetValues("X-Tenant-Id")));

        // The HUMAN's token travels with it: the module authorises the person, not Platform.
        Assert.Equal("caller-token", captured.Headers.Authorization?.Parameter);
    }


    /// <summary>
    /// (e) The refusal says WHICH refusal it is. "We could not name a tenant" and "the module did not answer"
    /// were the same 404 with the same sentence until 2026-08-28, so an operator asking "why was it not found?"
    /// got no answer from any log. The reader's sentence is deliberately unchanged — this is an operator signal,
    /// not a new user string, and it is kept OFF Response.ReasonCode precisely so it does not become one
    /// (reason_code feeds the frontend resx bridge and would owe seven translations).
    /// </summary>
    [Theory]
    [InlineData(false, "tenant-context-unresolved")]
    [InlineData(true, "platform-context-without-target-tenant")]
    public async Task A_call_skipped_for_want_of_a_tenant_names_its_own_reason(bool platformWithoutTarget, string expectedReason)
    {
        var called = false;
        var logger = new RecordingLogger<MdmLegalEntityReferenceValidator>();
        ITenantContext context = platformWithoutTarget
            ? new PlatformActingForTenant(PlatformSentinelTenantId, Guid.Empty)
            : new TenantContext();

        var validator = CreateValidator(
            (_, _) =>
            {
                called = true;
                return Task.FromResult(JsonResponse($$"""
                    {"data":{"legalEntityId":"{{LegalEntityId}}","legalName":"Legal","displayName":"Legal","lifecycleState":"ACTIVE","referenceable":true},"statusCode":200,"isSuccessful":true,"errors":[]}
                    """));
            },
            context,
            logger);

        var response = await validator.ValidateAsync(LegalEntityId);

        Assert.False(called);
        Assert.False(response.IsSuccessful);

        var line = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Contains(expectedReason, line.Message, StringComparison.Ordinal);

        // The user-facing sentence is untouched: this is an operator signal only, and carries no reason_code
        // that the frontend resx bridge would then owe a translation for.
        Assert.Equal("Legal Entity is not referenceable.", Assert.Single(response.Errors));
        Assert.Null(response.ReasonCode);
    }

    /// <summary>Captures what an operator would actually see, message already formatted.</summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    private static MdmLegalEntityReferenceValidator CreateValidator(HttpResponseMessage response) =>
        CreateValidator(_ => response);

    private static MdmLegalEntityReferenceValidator CreateValidator(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        CreateValidator((request, _) => Task.FromResult(responseFactory(request)));

    private static MdmLegalEntityReferenceValidator CreateValidator(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) =>
        CreateValidator(responseFactory, TenantContextResolvedFor(TenantId));

    private static MdmLegalEntityReferenceValidator CreateValidator(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory,
        ITenantContext tenantContext,
        ILogger<MdmLegalEntityReferenceValidator>? logger = null)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(responseFactory));
        var options = Options.Create(new MdmServiceOptions { BaseUrl = "http://mdm.local" });
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        accessor.HttpContext.Request.Headers.Authorization = "Bearer token";

        return new MdmLegalEntityReferenceValidator(
            httpClient, options, accessor, tenantContext,
            logger ?? new RecordingLogger<MdmLegalEntityReferenceValidator>());
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
