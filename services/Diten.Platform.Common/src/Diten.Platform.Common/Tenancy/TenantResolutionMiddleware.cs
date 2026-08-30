using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Common.Tenancy;

public sealed class TenantResolutionMiddleware
{
    private const string TenantHeader = "X-Tenant-Id";

    /// <summary>
    /// The machine-read name of the contradicting signal in the refusal body. Deliberately NOT a ReasonCode and
    /// NOT bridged to the resx layer: it is a routing signal for the caller, not screen text.
    /// </summary>
    private const string HeaderSignal = "header";
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (HttpMethods.IsOptions(context.Request.Method) || IsBypassPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // /api/platform-auth/* (login, change-password/forced, forgot-password, reset-password)
        // is the platform identity surface. AuthService enforces its own authorization.
        // Tenant resolution must not gate it on tenant_user actor types.
        // See master-plan §1.3 (platform/admin paths bypass tenant resolution).
        if (IsPlatformAuthPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var actorType = context.User.FindFirst("actor_type")?.Value;
        if (IsPersonalizationPath(context.Request.Path))
        {
            if (IsPlatformPersonalizationRequest(context.Request))
            {
                tenantContext.SetPlatformContext(Guid.Empty);
                await _next(context);
                return;
            }

            if (IsPlatformActor(actorType))
            {
                if (context.Request.Headers.ContainsKey(TenantHeader))
                {
                    await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Invalid Tenant Header", $"'{TenantHeader}' is not allowed for platform personalization requests.");
                    return;
                }

                tenantContext.SetPlatformContext(Guid.Empty);
                await _next(context);
                return;
            }

            var personalizationJwtTenant = ReadJwtTenant(context);
            var personalizationHeaderTenant = ReadHeaderTenant(context);
            var personalizationResolution = ResolveTenant(personalizationJwtTenant, personalizationHeaderTenant, context);

            // BEFORE the dev bypass (which fills an ABSENT tenant, it does not reconcile two named ones) and
            // BEFORE the actor_type 403 below — see the rule comment on ResolveTenant.
            if (personalizationResolution.IsConflict)
            {
                await WriteTenantMismatch(context, personalizationResolution);
                return;
            }

            var personalizationTenant = personalizationResolution.TenantId;
            if (personalizationTenant is null && TryGetDevelopmentBypassTenant(out var personalizationBypassTenant))
            {
                personalizationTenant = personalizationBypassTenant;
                _logger.LogWarning(
                    "TenantResolution dev bypass applied. Path={Path} TenantId={TenantId}",
                    context.Request.Path,
                    personalizationBypassTenant);
            }

            if (personalizationTenant is null)
            {
                await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Missing Tenant", $"'{TenantHeader}' header or JWT tenant_id claim is required for tenant personalization endpoints.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(actorType) && !string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase))
            {
                await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Forbidden Actor", "Tenant personalization endpoints require tenant_user tokens.");
                return;
            }

            tenantContext.SetTenant(personalizationTenant.Value);
            await _next(context);
            return;
        }

        if (IsAdminPath(context.Request.Path))
        {
            if (context.Request.Headers.ContainsKey(TenantHeader))
            {
                await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Invalid Tenant Header", $"'{TenantHeader}' is not allowed on admin endpoints.");
                return;
            }

            if (!IsPlatformActor(actorType))
            {
                await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Forbidden Actor", "Platform admin or partner admin token is required.");
                return;
            }

            tenantContext.SetPlatformContext(Guid.Empty);
            await _next(context);
            return;
        }

        var jwtTenant = ReadJwtTenant(context);
        var headerTenant = ReadHeaderTenant(context);
        var resolution = ResolveTenant(jwtTenant, headerTenant, context);

        // BEFORE the dev bypass and BEFORE the actor_type 403 below. THIS ORDERING IS THE OWNER DECISION:
        // IsTenantScopedOrgPath routes some /api/platform/* groups here to be answered 403 for a platform actor,
        // and a CONTRADICTING X-Tenant-Id on those paths is now answered 400 instead. The designed 403 is not
        // lost — with no header there is no contradiction and it still answers 403.
        if (resolution.IsConflict)
        {
            await WriteTenantMismatch(context, resolution);
            return;
        }

        var resolvedTenant = resolution.TenantId;
        if (resolvedTenant is null && TryGetDevelopmentBypassTenant(out var bypassTenant))
        {
            resolvedTenant = bypassTenant;
            _logger.LogWarning(
                "TenantResolution dev bypass applied. Path={Path} TenantId={TenantId}",
                context.Request.Path,
                bypassTenant);
        }

        if (resolvedTenant is null)
        {
            if (IsPublicAuthPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            _logger.LogWarning("Tenant context missing. Path={Path}", context.Request.Path);
            await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Missing Tenant", $"'{TenantHeader}' header or JWT tenant_id claim is required.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(actorType) && !string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase))
        {
            await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Forbidden Actor", "Tenant endpoints require tenant_user tokens.");
            return;
        }

        tenantContext.SetTenant(resolvedTenant.Value);
        await _next(context);
    }

    /*
     * BL-324 — THE CONTRADICTION IS REFUSED BEFORE ACCESS IS JUDGED (owner decision 2026-08-30, the same decision
     * and the same reasoning already applied in the gateway). If the token names a tenant and `X-Tenant-Id` names a
     * DIFFERENT one, the request is malformed and is refused 400. It is not an access verdict: a request that names
     * two tenants cannot be evaluated for access at all, so the refusal is ordered BEFORE the actor_type 403.
     *
     * This file previously logged "Tenant conflict. JWT tenant wins." and CARRIED ON. A warning is not a refusal.
     *
     * WITHOUT A TOKEN THERE IS NO CONTRADICTION — nothing authenticated has named a tenant, so a header alone is
     * just the only signal there is, and today's `jwtTenant ?? headerTenant` precedence is preserved exactly.
     *
     * ⚠ THE ONE DELIBERATE BEHAVIOUR CHANGE. IsTenantScopedOrgPath (below) routes /api/platform/organization-units,
     * /positions, /position-assignments, /navigation, /tenant-security and /working-calendars/overrides down the
     * TENANT branch specifically so a platform_admin token is answered 403 there. Platform tokens always carry
     * PlatformTenantId (…0001), so a platform_admin hitting those paths with a CONTRADICTING X-Tenant-Id now gets
     * 400 instead of 403. With no header there is no contradiction and the designed 403 is unchanged. Both halves
     * are measured — see TenantContradictionGuardTests in Diten.Platform.Application.Tests.
     *
     * ⚠ There is no subdomain signal in this middleware (unlike the gateway), so `conflictingSignals` is today
     * always ["header"]. It stays an ARRAY anyway, so the refusal body is the same shape the gateway answers and
     * the frontend does not have to learn two services separately.
     */
    private TenantResolution ResolveTenant(Guid? jwtTenant, Guid? headerTenant, HttpContext context)
    {
        if (jwtTenant.HasValue && headerTenant.HasValue && jwtTenant.Value != headerTenant.Value)
        {
            _logger.LogWarning(
                "Tenant contradiction refused. Signals={Signals} HeaderTenant={HeaderTenant} JwtTenant={JwtTenant} Path={Path}",
                HeaderSignal,
                headerTenant,
                jwtTenant,
                context.Request.Path);

            // The refusal status travels WITH the result, exactly as it does in the gateway: the two call sites
            // below cannot then drift apart on how a contradiction is answered, and the 400 is visible at the
            // point the decision is made rather than only inside the writer.
            return TenantResolution.Conflict([HeaderSignal], StatusCodes.Status400BadRequest);
        }

        return TenantResolution.Resolved(jwtTenant ?? headerTenant);
    }

    /// <summary>
    /// The outcome of tenant resolution. A CONTRADICTION IS ITS OWN OUTCOME — it cannot be expressed as a null
    /// tenant, because the caller would then answer "Missing Tenant" for a request that named two of them.
    /// </summary>
    private readonly record struct TenantResolution(
        Guid? TenantId,
        IReadOnlyList<string>? ConflictingSignals,
        int RefusalStatusCode)
    {
        public static TenantResolution Resolved(Guid? tenantId) => new(tenantId, null, 0);

        public static TenantResolution Conflict(IReadOnlyList<string> conflictingSignals, int refusalStatusCode)
            => new(null, conflictingSignals, refusalStatusCode);

        public bool IsConflict => ConflictingSignals is { Count: > 0 };
    }

    private static Guid? ReadJwtTenant(HttpContext context)
    {
        var claimValue = context.User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(claimValue, out var tenantId) ? tenantId : null;
    }

    private static Guid? ReadHeaderTenant(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(TenantHeader, out var headerValue) || string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }
        return Guid.TryParse(headerValue, out var tenantId) ? tenantId : null;
    }

    private static bool IsBypassPath(PathString path)
    {
        return path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/hangfire", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/internal", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/lookups", StringComparison.OrdinalIgnoreCase)
               || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublicAuthPath(PathString path)
    {
        return path.StartsWithSegments("/api/platform-auth/login", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdminPath(PathString path)
    {
        return (path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments("/api/platform", StringComparison.OrdinalIgnoreCase))
               && !IsTenantScopedOrgPath(path);
    }

    // MOD-0288 — the Organization/Position directory is tenant-scoped data managed by tenant admins
    // (like Users/Roles), so these /api/platform/* groups are treated as TENANT endpoints: the tenant
    // is resolved (JWT tenant_id / X-Tenant-Id) into ITenantContext and the tenant_user actor is required.
    // (Tenant-only: platform_admin tokens are intentionally rejected here.)
    private static bool IsTenantScopedOrgPath(PathString path)
    {
        return path.StartsWithSegments("/api/platform/organization-units", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/platform/positions", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/platform/position-assignments", StringComparison.OrdinalIgnoreCase)
               // MOD-0285 — runtime navigation menu is the tenant's own entitled-module nav; same tenant-scoped
               // treatment (tenant resolved from JWT tenant_id, tenant_user required, platform_admin rejected).
               || path.StartsWithSegments("/api/platform/navigation", StringComparison.OrdinalIgnoreCase)
               // FU17-FU01 — tenant-admin self-service security settings (the tenant manages its OWN login policy).
               || path.StartsWithSegments("/api/platform/tenant-security", StringComparison.OrdinalIgnoreCase)
               // Working Calendar — the OVERRIDE layer is the tenant's own calendar data, authored by tenant
               // admins. Only this sub-path is tenant-scoped: the parent /api/platform/working-calendars is the COUNTRY
               // layer and must stay platform-admin-only, so this check is deliberately narrower than the prefixes above.
               || path.StartsWithSegments("/api/platform/working-calendars/overrides", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlatformAuthPath(PathString path)
    {
        return path.StartsWithSegments("/api/platform-auth", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPersonalizationPath(PathString path)
    {
        return path.StartsWithSegments("/api/personalization", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlatformPersonalizationRequest(HttpRequest request)
    {
        var moduleKey = request.Query["moduleKey"].FirstOrDefault();
        return string.Equals(moduleKey, "Platform", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlatformActor(string? actorType)
    {
        return string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase)
               || string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The contradiction refusal. Same title, status and body shape as the gateway answers, INCLUDING
    /// `conflictingSignals` as an array even though this middleware only ever has one signal to report.
    /// </summary>
    private static async Task WriteTenantMismatch(HttpContext context, TenantResolution resolution)
    {
        var conflictingSignals = resolution.ConflictingSignals!;
        context.Response.StatusCode = resolution.RefusalStatusCode;
        context.Response.ContentType = "application/problem+json";
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = "Tenant mismatch",
            status = resolution.RefusalStatusCode,
            detail = $"The authenticated token and '{TenantHeader}' name different tenants. The request names two tenants and cannot be evaluated.",
            traceId = context.TraceIdentifier,
            conflictingSignals
        });
        await context.Response.WriteAsync(json);
    }

    private static async Task WriteProblemDetails(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            title,
            status = statusCode,
            detail,
            traceId = context.TraceIdentifier
        });
        await context.Response.WriteAsync(json);
    }

    private bool TryGetDevelopmentBypassTenant(out Guid tenantId)
    {
        tenantId = Guid.Empty;

        if (!_environment.IsDevelopment())
        {
            return false;
        }

        var bypassEnabledRaw = _configuration["TenantResolution:DevBypassEnabled"];
        if (!bool.TryParse(bypassEnabledRaw, out var bypassEnabled) || !bypassEnabled)
        {
            return false;
        }

        var rawTenant = _configuration["TenantResolution:DevBypassTenantId"];
        if (!Guid.TryParse(rawTenant, out tenantId))
        {
            _logger.LogWarning(
                "TenantResolution dev bypass enabled but TenantId is invalid. ConfigValue={ConfigValue}",
                rawTenant);
            return false;
        }

        return true;
    }
}
