using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Diten.ApiGateway.Authentication;

namespace Diten.ApiGateway.Middleware;

public sealed class TenantResolutionMiddleware
{
    private const string TenantHeader = "X-Tenant-Id";

    // The names reported in `conflictingSignals`. ⚠ A ROUTING signal for the caller ("send me to the right host"
    // vs "sign in again"), NOT display text — it is deliberately not a reason code and is not bridged to resx.
    private const string HeaderSignal = "header";
    private const string SubdomainSignal = "subdomain";

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

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsOptions(context.Request.Method) || IsNonTenantPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // /api/platform-auth/* (login, change-password/forced, forgot-password, reset-password)
        // is the platform identity surface. AuthService enforces its own authorization.
        // The tenant-resolution middleware must not gate it on tenant_user actor types.
        // See master-plan §1.3 (platform/admin paths bypass tenant resolution).
        if (IsPlatformAuthPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Ensure the authentication handler has run and context.User is populated.
        // UseAuthentication() should have done this, but when the handler returns NoResult
        // (e.g. cookie-only token not promoted yet), context.User may be unauthenticated.
        await EnsureAuthenticatedUserAsync(context);

        var actorType = ReadActorType(context.User) ?? ReadActorTypeFromRequestToken(context);
        var host = context.Request.Host.Host;
        var isAdminHost = IsAdminHost(host);
        var isTenantHost = IsTenantHost(host);
        var isAuthLifecyclePath = IsAuthLifecyclePath(context.Request.Path);
        var jwtTenant = ReadJwtTenant(context.User) ?? ReadJwtTenantFromRequestToken(context);
        var headerTenant = ReadHeaderTenant(context.Request.Headers[TenantHeader]);
        var subdomainTenant = ReadSubdomainTenant(context.Request.Host.Host);

        if (isAdminHost && !IsAdminHostAllowedPath(context.Request.Path))
        {
            await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Forbidden Host/Path Combination", "Admin host can only call platform management endpoints.");
            return;
        }

        if (isTenantHost && IsAdminPath(context.Request.Path))
        {
            await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Forbidden Host/Path Combination", "Tenant hosts cannot access platform admin endpoints.");
            return;
        }

        if (IsPersonalizationPath(context.Request.Path))
        {
            if (IsPlatformPersonalizationRequest(context.Request))
            {
                context.Request.Headers.Remove(TenantHeader);
                PromoteAccessTokenCookieToBearer(context.Request);
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

                PromoteAccessTokenCookieToBearer(context.Request);
                await _next(context);
                return;
            }

            var personalizationResolution = Resolve(jwtTenant, headerTenant, subdomainTenant, context);
            if (personalizationResolution.IsConflict)
            {
                await WriteTenantMismatch(context, personalizationResolution);
                return;
            }

            // ⚠ The bypass is ordered AFTER the conflict check on purpose: it stands in for an ABSENT tenant, and
            // must never fill in — and thereby conceal — a request whose signals contradict each other.
            var personalizationTenant = personalizationResolution.TenantId;
            if (personalizationResolution.IsMissing && TryGetDevelopmentBypassTenant(out var personalizationBypassTenant))
            {
                personalizationTenant = personalizationBypassTenant;
                _logger.LogWarning(
                    "TenantResolution dev bypass applied in gateway. Path={Path} TenantId={TenantId}",
                    context.Request.Path,
                    personalizationBypassTenant);
            }

            if (personalizationTenant is null)
            {
                await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Missing Tenant", $"'{TenantHeader}' or JWT tenant_id claim is required for tenant personalization endpoints.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(actorType) && !string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase))
            {
                await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Forbidden Actor", "Tenant personalization endpoints require tenant_user tokens.");
                return;
            }

            context.Request.Headers[TenantHeader] = personalizationTenant.Value.ToString();
            context.Items[TenantHeader] = personalizationTenant.Value;
            PromoteAccessTokenCookieToBearer(context.Request);

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

            if (!IsPublicEndpoint(context.Request.Path) && !isAuthLifecyclePath && !IsPlatformActor(actorType))
            {
                await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Forbidden Actor", "Platform admin or partner admin token is required.");
                return;
            }

            await _next(context);
            return;
        }

        var resolution = Resolve(jwtTenant, headerTenant, subdomainTenant, context);
        if (resolution.IsConflict)
        {
            await WriteTenantMismatch(context, resolution);
            return;
        }

        var resolvedTenant = resolution.TenantId;
        if (resolution.IsMissing && TryGetDevelopmentBypassTenant(out var bypassTenant))
        {
            resolvedTenant = bypassTenant;
            _logger.LogWarning(
                "TenantResolution dev bypass applied in gateway. Path={Path} TenantId={TenantId}",
                context.Request.Path,
                bypassTenant);
        }

        if (resolvedTenant is null)
        {
            if (IsPublicEndpoint(context.Request.Path))
            {
                await _next(context);
                return;
            }

            await WriteProblemDetails(
                context,
                StatusCodes.Status400BadRequest,
                "Missing Tenant",
                $"'{TenantHeader}' or JWT tenant_id claim is required for tenant endpoints.");
            return;
        }

        if (!IsPublicEndpoint(context.Request.Path)
            && !isAuthLifecyclePath
            && !string.IsNullOrWhiteSpace(actorType)
            && !string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase))
        {
            await WriteProblemDetails(context, StatusCodes.Status403Forbidden, "Forbidden Actor", "Tenant endpoints require tenant_user tokens.");
            return;
        }

        context.Request.Headers[TenantHeader] = resolvedTenant.Value.ToString();
        context.Items[TenantHeader] = resolvedTenant.Value;

        await _next(context);
    }

    /*
     * BL-324 — ONE RULE FOR EVERY SIGNAL. If the token names a tenant, every OTHER tenant signal on the request
     * must name the SAME one; otherwise the request is malformed and is refused 400. The gateway previously
     * logged "JWT tenant wins" for the header AND for the subdomain and carried on, which produced a session
     * addressed at one tenant's host while operating on another's data.
     *
     * The rule is deliberately signal-agnostic: a per-signal exception is what gets forgotten the day a fourth
     * tenant signal appears.
     *
     * WITHOUT A TOKEN THERE IS NO CONTRADICTION — nothing authenticated has named a tenant, so header and
     * subdomain disagreeing is just precedence, and today's `header ?? subdomain` order is preserved exactly.
     *
     * ⚠ LOGIN HAS NO EXEMPTION, IT IS SIMPLY TOKENLESS — AND THAT IS AN ASSUMPTION, NOT A PROPERTY OF THIS FILE.
     * A login request that DID carry a token naming another tenant would be refused 400 here, and the user could
     * not recover, because signing in again is the request being refused. Two things outside this middleware keep
     * that from happening, and BOTH must hold:
     *
     *   1. The gateway receives login server-to-server from Diten.Web, with no Cookie header and no bearer
     *      (frontend/Diten.Web/Services/Auth/AuthGateway.cs:56-66 and :203-230).
     *   2. The auth cookie is HOST-ONLY — AuthCookieService.BuildCookieOptions never sets `Domain`
     *      (frontend/Diten.Web/Services/Auth/AuthCookieService.cs:21-31), so tenant A's token is never sent to
     *      tenant B's host at all.
     *
     * Assumption 2 is one added line away from being false, so it is GUARDED where it lives:
     *   frontend/Diten.Web.Tests/Auth/AuthCookieDomainScopeGuardTests.cs   ← breaks, and says why, if Domain is set
     * The refusal itself is measured by
     *   gateway/Diten.ApiGateway.Tests/TenantContradictionGuardTests.cs
     *     → Login_that_DOES_carry_a_token_is_refused_like_any_other_path
     */
    private TenantResolution Resolve(Guid? jwtTenant, Guid? headerTenant, Guid? subdomainTenant, HttpContext context)
    {
        // Fixed order — the refusal body is machine-read by the caller, which must be able to compare it.
        var conflictingSignals = new List<string>(2);

        if (jwtTenant.HasValue && headerTenant.HasValue && jwtTenant.Value != headerTenant.Value)
        {
            conflictingSignals.Add(HeaderSignal);
        }

        if (jwtTenant.HasValue && subdomainTenant.HasValue && jwtTenant.Value != subdomainTenant.Value)
        {
            conflictingSignals.Add(SubdomainSignal);
        }

        if (conflictingSignals.Count > 0)
        {
            _logger.LogWarning(
                "Tenant contradiction refused in gateway. Signals={Signals} Path={Path}",
                string.Join(',', conflictingSignals),
                context.Request.Path);

            return TenantResolution.Conflict(conflictingSignals, StatusCodes.Status400BadRequest);
        }

        return TenantResolution.Resolved(jwtTenant ?? headerTenant ?? subdomainTenant);
    }

    /// <summary>
    /// The outcome of tenant resolution. A CONTRADICTION IS ITS OWN OUTCOME — it cannot be expressed as a null
    /// tenant, because the caller would then answer "Missing Tenant" for a request that named two of them. The
    /// refusal status travels with the result so the two call sites cannot drift apart on how they answer it.
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

        public bool IsMissing => TenantId is null && !IsConflict;
    }

    private static Guid? ReadJwtTenant(ClaimsPrincipal user)
    {
        var raw = FindClaimValue(user, "tenant_id");
        return Guid.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static string? ReadActorType(ClaimsPrincipal user)
    {
        return FindClaimValue(user, "actor_type");
    }

    private string? ReadActorTypeFromRequestToken(HttpContext context)
    {
        var actorType = ReadClaimFromRequestToken(context, "actor_type");
        if (!string.IsNullOrWhiteSpace(actorType))
        {
            _logger.LogWarning(
                "actor_type resolved from raw token fallback (context.User did not contain the claim). Path={Path} ActorType={ActorType}",
                context.Request.Path,
                actorType);
        }

        return actorType;
    }

    private static Guid? ReadJwtTenantFromRequestToken(HttpContext context)
    {
        var raw = ReadClaimFromRequestToken(context, "tenant_id");
        return Guid.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static async Task EnsureAuthenticatedUserAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return;
        }

        var result = await context.AuthenticateAsync("Bearer");
        if (result.Succeeded && result.Principal is not null)
        {
            context.User = result.Principal;
        }
    }

    private static string? FindClaimValue(ClaimsPrincipal user, string claimType)
    {
        return user.Claims
            .FirstOrDefault(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static string? ReadClaimFromRequestToken(HttpContext context, string claimType)
    {
        var token = ReadBearerToken(context);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwt.Claims
                .FirstOrDefault(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))
                ?.Value;
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadBearerToken(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authorization) &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        return AuthTokenCookies.GetAccessToken(context.Request);
    }

    private static Guid? ReadHeaderTenant(string? headerValue)
    {
        return Guid.TryParse(headerValue, out var parsed) ? parsed : null;
    }

    private static Guid? ReadSubdomainTenant(string host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
        {
            return null;
        }

        return Guid.TryParse(parts[0], out var parsed) ? parsed : null;
    }

    private static bool IsNonTenantPath(PathString path)
    {
        return path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/lookups", StringComparison.OrdinalIgnoreCase)
               || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPublicEndpoint(PathString path)
    {
        return path.StartsWithSegments("/api/platform-auth/login", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/tenant-auth/login", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/tenant-auth/register", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/auth/refresh-token", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/auth/health", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAuthLifecyclePath(PathString path)
    {
        return path.StartsWithSegments("/api/auth/refresh-token", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/auth/logout", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdminPath(PathString path)
    {
        return (path.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase)
                || path.StartsWithSegments("/api/platform", StringComparison.OrdinalIgnoreCase))
               && !IsTenantScopedOrgPath(path);
    }

    // MOD-0288 — the Organization/Position directory is tenant-scoped data managed by tenant admins
    // (like Users/Roles), so these /api/platform/* groups are treated as TENANT endpoints: tenant
    // resolution runs, the tenant_user actor is required, and X-Tenant-Id is injected downstream.
    // (Tenant-only: platform_admin tokens are intentionally rejected here.)
    private static bool IsTenantScopedOrgPath(PathString path)
    {
        return path.StartsWithSegments("/api/platform/organization-units", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/platform/positions", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/platform/position-assignments", StringComparison.OrdinalIgnoreCase)
               // MOD-0285 — runtime navigation menu is the tenant's own entitled-module nav (tenant-scoped).
               || path.StartsWithSegments("/api/platform/navigation", StringComparison.OrdinalIgnoreCase)
               // FU17-FU01 — tenant-admin self-service security settings (tenant manages its OWN login policy).
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

    private static void PromoteAccessTokenCookieToBearer(HttpRequest request)
    {
        if (request.Headers.ContainsKey("Authorization"))
        {
            return;
        }

        var accessToken = AuthTokenCookies.GetAccessToken(request);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = $"Bearer {accessToken}";
        }
    }

    private static bool IsAdminHostAllowedPath(PathString path)
    {
        return IsAdminPath(path)
               || IsPersonalizationPath(path)
               || path.StartsWithSegments("/api/platform-auth", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlatformActor(string? actorType)
    {
        return string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase)
               || string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAdminHost(string host)
    {
        return host.Equals("admin.diten.tech", StringComparison.OrdinalIgnoreCase)
               || host.Equals("admin.localhost", StringComparison.OrdinalIgnoreCase)
               || host.StartsWith("admin.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTenantHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.StartsWith("127.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsAdminHost(host);
    }

    private static async Task WriteProblemDetails(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            title,
            status = statusCode,
            detail,
            traceId = context.TraceIdentifier
        });
    }

    /// <summary>
    /// The contradiction refusal: the shared problem+json shape plus `conflictingSignals`, which is what lets the
    /// caller tell "you are on the wrong host" from "your session belongs to another tenant" WITHOUT parsing the
    /// prose in `detail`. The prose is for humans reading logs; the array is the only machine-readable part.
    /// </summary>
    private static async Task WriteTenantMismatch(HttpContext context, TenantResolution resolution)
    {
        context.Response.StatusCode = resolution.RefusalStatusCode;
        await context.Response.WriteAsJsonAsync(
            new
            {
                title = "Tenant mismatch",
                status = resolution.RefusalStatusCode,
                detail = "The authenticated tenant and the request's other tenant signals name different tenants. "
                         + $"'{TenantHeader}' and the request host must match the token's tenant_id claim.",
                traceId = context.TraceIdentifier,
                conflictingSignals = resolution.ConflictingSignals
            },
            options: null,
            // ⚠ Passed to WriteAsJsonAsync rather than assigned to Response.ContentType first: the assignment is
            // OVERWRITTEN by the serializer, which is why the other refusals here still answer application/json.
            contentType: "application/problem+json");
    }

    private bool TryGetDevelopmentBypassTenant(out Guid tenantId)
    {
        tenantId = Guid.Empty;

        if (!_environment.IsDevelopment())
        {
            return false;
        }

        if (!_configuration.GetValue<bool>("TenantResolution:DevBypassEnabled"))
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
