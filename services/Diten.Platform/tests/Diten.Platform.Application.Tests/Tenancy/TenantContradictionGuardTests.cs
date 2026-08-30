using System.Security.Claims;
using System.Text.Json;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tenancy;

/*
 * THE GUARD — TENANT CONTRADICTION IS REFUSED IN Diten.Platform.Common (BL-324, rule from BL-323 / DCP-004 §7.4).
 *
 * WHAT WAS WRONG. Diten.Platform.Common's TenantResolutionMiddleware read two tenant signals (the JWT tenant_id
 * claim and X-Tenant-Id) and on a disagreement logged "Tenant conflict. JWT tenant wins." and CARRIED ON. A warning
 * is not a refusal: the request kept running, having named two tenants, with the only trace a log line nobody reads.
 * This was the LAST of seven tenant-resolution sites in the repo still doing that.
 *
 * THE RULE (owner decision 2026-08-30, the same decision already applied in the gateway): if the token names a
 * tenant, every OTHER tenant signal must name the SAME one, or the request is refused 400 — BEFORE any access
 * judgement, because a request naming two tenants cannot be evaluated for access at all.
 *
 * ⚠ WHY THIS FILE LIVES IN Diten.Platform.Application.Tests. MEASURED: no test project in the repo referenced
 * Diten.Platform.Common, and Platform.Common has no test project of its own. Diten.Platform.API is the ONLY service
 * wiring this middleware (Program.cs), and Diten.Platform.Application.Tests ALREADY reaches Platform.Common
 * transitively (Application.Tests -> Diten.Platform.Application -> Diten.Platform.Common), so the behaviour could be
 * measured with NO new project and NO new ProjectReference. Standing up a fresh test project for one middleware
 * would have been more moving parts guarding the same fact.
 *
 * ⚠ NOT A VACUITY CHECK. A middleware that did nothing would answer 200, so "not 403" alone would pass against the
 * very defect this exists to catch. Every refusal case asserts the handler DID NOT RUN, and the pass-through
 * controls prove a middleware refusing everything would fail too.
 */
public sealed class TenantContradictionGuardTests
{
    private const string TenantHeader = "X-Tenant-Id";
    private const string TenantPath = "/api/users";
    private const string PersonalizationPath = "/api/personalization/preferences";

    /// <summary>
    /// A tenant-scoped /api/platform/* group — IsTenantScopedOrgPath routes it down the TENANT branch specifically
    /// so a platform_admin token is answered 403 there (MOD-0288).
    /// </summary>
    private const string TenantScopedOrgPath = "/api/platform/organization-units";

    /// <summary>
    /// MEASURED: PlatformLoginCommandHandler stamps every platform token with this tenant_id, which is why the
    /// contradiction check and the actor_type 403 can meet on the same request at all.
    /// </summary>
    private static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    // (a) — token and header name the SAME tenant.
    [Fact]
    public async Task Jwt_and_matching_header_pass_through()
    {
        var tenant = Guid.NewGuid();
        var run = await RunAsync(TenantPath, jwtTenant: tenant, headerTenant: tenant);

        Assert.True(run.HandlerRan, "a self-consistent request was refused");
        Assert.Equal(StatusCodes.Status200OK, run.StatusCode);
        Assert.Equal(tenant, run.ResolvedTenant);
    }

    // (b) — token A, header B, on the ordinary tenant path.
    [Fact]
    public async Task Jwt_contradicted_by_header_is_refused_400_on_the_tenant_path()
    {
        var run = await RunAsync(TenantPath, jwtTenant: Guid.NewGuid(), headerTenant: Guid.NewGuid());

        Assert.False(run.HandlerRan, "a contradicting request reached the handler");
        Assert.Equal(StatusCodes.Status400BadRequest, run.StatusCode);
        Assert.Equal("Tenant mismatch", run.Title);
        Assert.Equal(["header"], run.ConflictingSignals);
        Assert.Null(run.ResolvedTenant);
    }

    // (c) — the SECOND call site. The personalization branch resolves the tenant separately, and a rule applied at
    // only one of the two call sites is the drift this whole round exists to end.
    [Fact]
    public async Task Jwt_contradicted_by_header_is_refused_400_on_the_personalization_path()
    {
        var run = await RunAsync(PersonalizationPath, jwtTenant: Guid.NewGuid(), headerTenant: Guid.NewGuid());

        Assert.False(run.HandlerRan, "a contradicting personalization request reached the handler");
        Assert.Equal(StatusCodes.Status400BadRequest, run.StatusCode);
        Assert.Equal("Tenant mismatch", run.Title);
        Assert.Equal(["header"], run.ConflictingSignals);
    }

    /*
     * (d) AND (e) ARE ONE MEASUREMENT IN TWO HALVES, AND NEITHER MEANS ANYTHING ALONE.
     *
     * The owner decision reorders the contradiction refusal AHEAD of the actor_type 403. On IsTenantScopedOrgPath
     * routes that 403 is DESIGNED behaviour — platform_admin is deliberately rejected there — so the only honest
     * way to state what changed is to pin BOTH: the 403 still happens when there is nothing to contradict (d), and
     * ONLY a contradicting header turns it into a 400 (e).
     *
     * ⚠ (d) alone would stay green even with the contradiction check deleted entirely; (e) alone would not show
     * that the designed 403 survived. Do not split them.
     */

    // (d) — platform_admin, NO header: no contradiction, so the designed 403 is UNCHANGED.
    [Fact]
    public async Task Platform_admin_without_a_header_still_gets_the_designed_403_on_a_tenant_scoped_org_path()
    {
        var run = await RunAsync(TenantScopedOrgPath, jwtTenant: PlatformTenantId, actorType: "platform_admin");

        Assert.False(run.HandlerRan, "a platform_admin reached a tenant-only org endpoint");
        Assert.Equal(StatusCodes.Status403Forbidden, run.StatusCode);
        Assert.Equal("Forbidden Actor", run.Title);
        Assert.Null(run.ConflictingSignals);
    }

    // (e) — platform_admin with a CONTRADICTING header: 400 now, where it was 403 before. The accepted change.
    [Fact]
    public async Task Platform_admin_with_a_contradicting_header_is_refused_400_instead_of_403()
    {
        var run = await RunAsync(
            TenantScopedOrgPath,
            jwtTenant: PlatformTenantId,
            headerTenant: Guid.NewGuid(),
            actorType: "platform_admin");

        Assert.False(run.HandlerRan);
        Assert.Equal(StatusCodes.Status400BadRequest, run.StatusCode);
        Assert.Equal("Tenant mismatch", run.Title);
        Assert.Equal(["header"], run.ConflictingSignals);
        Assert.NotEqual(StatusCodes.Status403Forbidden, run.StatusCode);
    }

    /// <summary>
    /// (f) — WITHOUT A TOKEN THERE IS NO CONTRADICTION. Nothing authenticated has named a tenant, so the header is
    /// simply the only signal there is and today's precedence is preserved exactly.
    /// </summary>
    [Fact]
    public async Task No_token_means_no_contradiction_and_the_header_is_used()
    {
        var headerTenant = Guid.NewGuid();
        var run = await RunAsync(TenantPath, jwtTenant: null, headerTenant: headerTenant);

        Assert.True(run.HandlerRan, "a request with no token was refused as a contradiction");
        Assert.Equal(StatusCodes.Status200OK, run.StatusCode);
        Assert.Equal(headerTenant, run.ResolvedTenant);
    }

    /// <summary>
    /// (g) — THE DEV BYPASS FILLS AN ABSENCE, IT DOES NOT RECONCILE TWO NAMED TENANTS. Ordering the conflict check
    /// after the bypass would have let development quietly resolve a request the rule refuses.
    /// </summary>
    [Fact]
    public async Task Dev_bypass_fills_a_missing_tenant_but_never_a_contradicting_one()
    {
        var bypassTenant = Guid.NewGuid();

        var missing = await RunAsync(TenantPath, jwtTenant: null, devBypassTenant: bypassTenant);
        Assert.True(missing.HandlerRan, "the dev bypass stopped filling a missing tenant");
        Assert.Equal(bypassTenant, missing.ResolvedTenant);

        var contradicting = await RunAsync(
            TenantPath,
            jwtTenant: Guid.NewGuid(),
            headerTenant: Guid.NewGuid(),
            devBypassTenant: bypassTenant);

        Assert.False(contradicting.HandlerRan, "the dev bypass swallowed a contradiction");
        Assert.Equal(StatusCodes.Status400BadRequest, contradicting.StatusCode);
        Assert.Equal("Tenant mismatch", contradicting.Title);
        Assert.Equal(["header"], contradicting.ConflictingSignals);
    }

    /// <summary>
    /// (h, first half) — THE ADMIN BRANCH IS UNTOUCHED. It rejects X-Tenant-Id outright with its own 400 BEFORE any
    /// tenant is resolved, so the new refusal must not reach it and must not relabel its answer.
    /// </summary>
    [Fact]
    public async Task Admin_branch_still_answers_Invalid_Tenant_Header_not_Tenant_mismatch()
    {
        var run = await RunAsync(
            "/api/admin/tenants",
            jwtTenant: PlatformTenantId,
            headerTenant: Guid.NewGuid(),
            actorType: "platform_admin");

        Assert.False(run.HandlerRan);
        Assert.Equal(StatusCodes.Status400BadRequest, run.StatusCode);
        Assert.Equal("Invalid Tenant Header", run.Title);
        Assert.Null(run.ConflictingSignals);
    }

    /// <summary>(h, second half) — a bypass path is answered before any tenant signal is read at all.</summary>
    [Theory]
    [InlineData("/health")]
    [InlineData("/api/internal/module-manifest")]
    public async Task Bypass_paths_are_untouched_even_when_the_signals_contradict(string path)
    {
        var run = await RunAsync(path, jwtTenant: Guid.NewGuid(), headerTenant: Guid.NewGuid());

        Assert.True(run.HandlerRan, $"BL-324 started refusing the bypass path {path}");
        Assert.Equal(StatusCodes.Status200OK, run.StatusCode);
    }

    /// <summary>
    /// THE MISSING CASE IS STILL "MISSING", NOT "MISMATCH" — the reason the contradiction could not be reported as
    /// a null tenant, which is what the old <c>Guid?</c> return would have forced.
    /// </summary>
    [Fact]
    public async Task No_tenant_signal_at_all_is_still_answered_Missing_Tenant()
    {
        var run = await RunAsync(TenantPath, jwtTenant: null);

        Assert.False(run.HandlerRan);
        Assert.Equal(StatusCodes.Status400BadRequest, run.StatusCode);
        Assert.Equal("Missing Tenant", run.Title);
        Assert.Null(run.ConflictingSignals);
    }

    /// <summary>
    /// The refusal body is the SAME SHAPE the gateway answers, including <c>conflictingSignals</c> as an ARRAY even
    /// though this middleware has only one signal — so the frontend does not learn two services separately.
    /// </summary>
    [Fact]
    public async Task The_refusal_body_matches_the_gateway_shape()
    {
        var run = await RunAsync(TenantPath, jwtTenant: Guid.NewGuid(), headerTenant: Guid.NewGuid());

        Assert.Equal("application/problem+json", run.ContentType);
        Assert.Equal("Tenant mismatch", run.Title);
        Assert.Equal(400, run.Body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(run.Body.GetProperty("detail").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(run.Body.GetProperty("traceId").GetString()));
        Assert.Equal(JsonValueKind.Array, run.Body.GetProperty("conflictingSignals").ValueKind);
    }

    private static async Task<Run> RunAsync(
        string path,
        Guid? jwtTenant = null,
        Guid? headerTenant = null,
        string actorType = "tenant_user",
        Guid? devBypassTenant = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        if (jwtTenant.HasValue)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenant_id", jwtTenant.Value.ToString()), new Claim("actor_type", actorType)],
                "test"));
        }

        if (headerTenant.HasValue)
        {
            context.Request.Headers[TenantHeader] = headerTenant.Value.ToString();
        }

        var settings = new Dictionary<string, string?>();
        if (devBypassTenant.HasValue)
        {
            settings["TenantResolution:DevBypassEnabled"] = "true";
            settings["TenantResolution:DevBypassTenantId"] = devBypassTenant.Value.ToString();
        }

        var ran = false;
        var tenantContext = new TenantContext();
        var middleware = new TenantResolutionMiddleware(
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            NullLogger<TenantResolutionMiddleware>.Instance,
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            // Only the bypass cases run as Development; everywhere else the bypass must not stand in for a tenant
            // and quietly turn a refusal into a pass.
            new StubEnvironment(devBypassTenant.HasValue ? Environments.Development : Environments.Production));

        await middleware.InvokeAsync(context, tenantContext);

        context.Response.Body.Position = 0;
        var raw = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var body = string.IsNullOrWhiteSpace(raw) ? default(JsonElement?) : JsonDocument.Parse(raw).RootElement;

        string[]? signals = null;
        if (body?.TryGetProperty("conflictingSignals", out var signalsElement) == true
            && signalsElement.ValueKind == JsonValueKind.Array)
        {
            signals = signalsElement.EnumerateArray().Select(x => x.GetString()!).ToArray();
        }

        return new Run(
            ran,
            context.Response.StatusCode,
            context.Response.ContentType,
            body ?? default,
            body?.TryGetProperty("title", out var title) == true ? title.GetString() : null,
            signals,
            tenantContext is { IsResolved: true, IsPlatformContext: false } ? tenantContext.TenantId : null);
    }

    private sealed record Run(
        bool HandlerRan,
        int StatusCode,
        string? ContentType,
        JsonElement Body,
        string? Title,
        string[]? ConflictingSignals,
        Guid? ResolvedTenant);

    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Diten.Platform.Application.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
