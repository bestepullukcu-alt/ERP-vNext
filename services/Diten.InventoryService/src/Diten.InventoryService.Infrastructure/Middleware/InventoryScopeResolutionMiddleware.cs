using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Infrastructure.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Infrastructure.Middleware;

/// <summary>
/// Resolves the caller scope for MOD-0173 — pack §8, which DIFFERS from the repo's TenantResolutionMiddleware
/// family on purpose, so this is not one of them:
/// <list type="bullet">
/// <item>Tenant and user come from the SIGNED JWT only: exactly one UUID each of <c>tenant_id</c> and <c>sub</c>. No dev
/// bypass.</item>
/// <item>Legal entity, two paths (pack §19 waiver W-0173-02):
///   <list type="number">
///   <item>The token HAS <c>legal_entity_id</c> → it decides; <c>X-Legal-Entity-Id</c>, if sent, must equal it, else
///   <b>403 CROSS_LE_FORBIDDEN</b> (unchanged since S1). Audit source <c>token</c>.</item>
///   <item>The token has NO such claim (AuthService issues none today) → <c>X-Legal-Entity-Id</c> (one UUID) is
///   REQUIRED, absent/invalid → <b>403</b> "select a company", no data. The id is verified with MDM
///   (<c>lookup-validation</c>, caller's token) to be an ACTIVE legal entity of the caller's tenant: unknown, inactive or
///   another tenant's → <b>403 CROSS_LE_FORBIDDEN</b>; MDM refuses the caller → <b>403 CROSS_LE_FORBIDDEN</b> (reason
///   logged); MDM unreachable / 5xx / timeout → <b>503 LEGAL_ENTITY_MASTER_UNAVAILABLE</b>, nothing read or written.
///   Audit source <c>header-unverified-membership</c> — the user↔LE membership is NOT checked (no such data exists:
///   the accepted risk of the waiver).</item>
///   </list></item>
/// <item><c>X-Tenant-Id</c> present and ≠ token tenant → <b>403</b> (the family answers 400).</item>
/// <item><c>tenantId</c> / <c>legalEntityId</c> in the query → <b>400 INVALID_REQUEST</b> (the body is checked by the
/// request reader). A legal entity is never accepted from a query or a body.</item>
/// <item>MOD-0173-FU02 §8: <c>/api/locations</c> is covered by the SAME rules (one rule set). One exception, from the
/// frozen LOCATION v1 contract: <c>GET /api/locations?legalEntityId=</c> (<c>listLocations</c>) passes here, and the
/// handler accepts it only when it equals the resolved company (else 403 CROSS_LE_FORBIDDEN, ASSUMPTION-LOC-06).</item>
/// </list>
/// A positive MDM answer is cached for at most <see cref="MembershipCacheLifetime"/>, keyed by tenant + legal entity +
/// a hash of the caller's bearer token, so it cannot carry from one user (or one token) to another; refusals and
/// failures are never cached. Runs after authentication and before authorization; an unauthenticated request passes
/// through untouched so authorization answers 401 first.
/// </summary>
public sealed class InventoryScopeResolutionMiddleware
{
    public static readonly TimeSpan MembershipCacheLifetime = TimeSpan.FromSeconds(60);

    private const string TenantClaim = "tenant_id";
    private const string LegalEntityClaim = "legal_entity_id";
    private const string SubjectClaim = "sub";

    private static readonly string[] ForbiddenQueryKeys = ["tenantId", "legalEntityId", "tenant_id", "legal_entity_id"];

    /// <summary>
    /// Every base path this middleware scopes (MOD-0173 ledger + MOD-0173-FU02 locations + MOD-0174 trace + MOD-0175 quarantine + MOD-0176
    /// expiry, WP-0176-F1: without it an <c>/api/expiry</c> request would reach a handler with an unresolved scope).
    /// </summary>
    public static readonly IReadOnlyList<string> ScopedBasePaths =
        [InventoryContract.BasePath, InventoryContract.LocationsBasePath, Application.Features.Traceability.TraceContract.BasePath,
         Application.Features.Quarantine.QuarantineContract.BasePath, Application.Features.Expiry.ExpiryContract.BasePath];

    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<InventoryScopeResolutionMiddleware> _logger;

    public InventoryScopeResolutionMiddleware(RequestDelegate next, IMemoryCache cache, ILogger<InventoryScopeResolutionMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ILegalEntityMasterClient legalEntities)
    {
        if (HttpMethods.IsOptions(context.Request.Method)
            || !ScopedBasePaths.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase))
            || context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var tenant = SingleUuid(context.User, TenantClaim);
        var subject = SingleUuid(context.User, SubjectClaim, ClaimTypes.NameIdentifier);
        var legalEntityClaims = context.User.Claims.Count(c => c.Type == LegalEntityClaim);
        var claimLegalEntity = SingleUuid(context.User, LegalEntityClaim);
        // A claim that is present but not exactly one UUID is a malformed token, not a claim-less one.
        if (tenant is null || subject is null || (legalEntityClaims > 0 && claimLegalEntity is null))
        {
            _logger.LogWarning("Inventory scope refused: token lacks a single UUID tenant_id/sub (or carries a malformed legal_entity_id). Path={Path}", context.Request.Path);
            await ContractErrorWriter.WriteAsync(context, StatusCodes.Status403Forbidden, null,
                "The access token must carry exactly one UUID tenant_id and sub, and at most one UUID legal_entity_id.");
            return;
        }

        if (!HeaderMatches(context, InventoryContract.TenantHeader, tenant.Value))
        {
            _logger.LogWarning("Inventory scope refused: X-Tenant-Id differs from the token tenant. Path={Path}", context.Request.Path);
            await ContractErrorWriter.WriteAsync(context, StatusCodes.Status403Forbidden, null,
                $"{InventoryContract.TenantHeader} does not match the token tenant.");
            return;
        }

        if (ForbiddenQueryKeys.Any(k => context.Request.Query.Keys.Contains(k, StringComparer.OrdinalIgnoreCase) && !IsFrozenListFilter(context, k)))
        {
            await ContractErrorWriter.WriteAsync(context, StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest,
                "tenantId/legalEntityId must not be sent: scope is taken from the access token.");
            return;
        }

        // ── 1. The token names the legal entity: it decides; a header may only confirm it. ──
        if (claimLegalEntity is not null)
        {
            if (!HeaderMatches(context, InventoryContract.LegalEntityOverrideHeader, claimLegalEntity.Value))
            {
                _logger.LogWarning("Inventory scope refused: X-Legal-Entity-Id differs from the token legal entity. Path={Path}", context.Request.Path);
                await ContractErrorWriter.WriteAsync(context, StatusCodes.Status403Forbidden, InventoryErrorCodes.CrossLeForbidden,
                    "Access to another legal entity is forbidden.");
                return;
            }

            tenantContext.SetScope(tenant.Value, claimLegalEntity.Value, subject.Value, LegalEntitySources.Token);
            await _next(context);
            return;
        }

        // ── 2. Claim-less token (W-0173-02): the company comes from the header and is verified with MDM. ──
        var headerLegalEntity = SingleHeaderUuid(context, InventoryContract.LegalEntityOverrideHeader);
        if (headerLegalEntity is null && IsSubjectProvider(context))
        {
            // MOD-0175 Q3: MOD-0022 calls the subject provider with the signer's token + X-Tenant-Id only. No company is
            // selected: the scope stays unresolved here and the provider sets it from the record's own company
            // (tenant + user from this same signed token; a company that IS named must match the record).
            await _next(context);
            return;
        }

        if (headerLegalEntity is null)
        {
            await ContractErrorWriter.WriteAsync(context, StatusCodes.Status403Forbidden, null,
                $"Select a company: the access token carries no legal entity, so {InventoryContract.LegalEntityOverrideHeader} (one UUID) is required.");
            return;
        }

        LegalEntityMembership membership;
        var cacheKey = CacheKey(context, tenant.Value, headerLegalEntity.Value);
        if (_cache.TryGetValue(cacheKey, out bool cachedActive) && cachedActive)
        {
            membership = LegalEntityMembership.Active;
        }
        else
        {
            try
            {
                membership = await legalEntities.ValidateAsync(headerLegalEntity.Value, context.RequestAborted);
            }
            catch (LegalEntityMasterUnavailableException ex)
            {
                _logger.LogWarning(ex, "Inventory scope refused: MDM legal-entity master unavailable. Path={Path}", context.Request.Path);
                await ContractErrorWriter.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, InventoryErrorCodes.LegalEntityMasterUnavailable,
                    "The legal-entity master could not confirm the selected company. Nothing was read or changed; retry later.");
                return;
            }

            if (membership == LegalEntityMembership.Active)
            {
                _cache.Set(cacheKey, true, MembershipCacheLifetime);
            }
        }

        if (membership != LegalEntityMembership.Active)
        {
            _logger.LogWarning(
                "Inventory scope refused: X-Legal-Entity-Id {LegalEntityId} is not an active legal entity of the caller's tenant (MDM: {Membership}). Path={Path}",
                headerLegalEntity.Value, membership, context.Request.Path);
            await ContractErrorWriter.WriteAsync(context, StatusCodes.Status403Forbidden, InventoryErrorCodes.CrossLeForbidden,
                "The selected company is not an active legal entity of your tenant.");
            return;
        }

        tenantContext.SetScope(tenant.Value, headerLegalEntity.Value, subject.Value, LegalEntitySources.HeaderUnverifiedMembership);
        await _next(context);
    }

    /// <summary>MOD-0175 Q3: <c>GET /api/quarantine/esign-subjects/…</c>; MOD-0176 F3: <c>GET /api/expiry/esign-subjects/…</c> (the MOD-0022
    /// subject providers, read-only).</summary>
    private static bool IsSubjectProvider(HttpContext context)
        => HttpMethods.IsGet(context.Request.Method)
           && (context.Request.Path.StartsWithSegments(Application.Features.Quarantine.QuarantineContract.BasePath + "/esign-subjects", StringComparison.OrdinalIgnoreCase)
               || context.Request.Path.StartsWithSegments(Application.Features.Expiry.ExpiryContract.BasePath + "/esign-subjects", StringComparison.OrdinalIgnoreCase));

    /// <summary>The frozen LOCATION v1 <c>listLocations</c> filter <c>legalEntityId</c> on <c>GET /api/locations</c> only.</summary>
    private static bool IsFrozenListFilter(HttpContext context, string key)
        => string.Equals(key, "legalEntityId", StringComparison.Ordinal)
           && HttpMethods.IsGet(context.Request.Method)
           && context.Request.Path.Value?.TrimEnd('/').Equals(InventoryContract.LocationsBasePath, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Exactly one claim across the given types, holding a non-empty UUID.</summary>
    private static Guid? SingleUuid(ClaimsPrincipal user, params string[] claimTypes)
    {
        var values = user.Claims.Where(c => claimTypes.Contains(c.Type, StringComparer.Ordinal)).Select(c => c.Value).ToList();
        return values.Count == 1 && Guid.TryParse(values[0], out var value) && value != Guid.Empty ? value : null;
    }

    /// <summary>Exactly one header value holding a non-empty UUID, else null.</summary>
    private static Guid? SingleHeaderUuid(HttpContext context, string header)
        => context.Request.Headers.TryGetValue(header, out var values) && values.Count == 1
           && Guid.TryParse(values[0], out var parsed) && parsed != Guid.Empty
            ? parsed
            : null;

    /// <summary>Absent header → matches (the token decides). Present → must be exactly one value equal to the claim.</summary>
    private static bool HeaderMatches(HttpContext context, string header, Guid claim)
    {
        if (!context.Request.Headers.TryGetValue(header, out var values) || values.Count == 0)
        {
            return true;
        }

        return values.Count == 1 && Guid.TryParse(values[0], out var parsed) && parsed == claim;
    }

    /// <summary>tenant + legal entity + SHA-256 of the bearer: one token's positive answer never serves another token.</summary>
    private static string CacheKey(HttpContext context, Guid tenant, Guid legalEntity)
    {
        var bearer = context.Request.Headers.Authorization.ToString();
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bearer)));
        return $"inventory-le-membership:{tenant:D}:{legalEntity:D}:{tokenHash}";
    }
}
