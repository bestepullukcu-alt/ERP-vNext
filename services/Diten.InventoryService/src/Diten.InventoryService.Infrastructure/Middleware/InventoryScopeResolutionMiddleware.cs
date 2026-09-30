using System.Security.Claims;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Infrastructure.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Infrastructure.Middleware;

/// <summary>
/// Resolves the caller scope for MOD-0173 — pack §8, which DIFFERS from the repo's TenantResolutionMiddleware
/// family on purpose, so this is not one of them:
/// <list type="bullet">
/// <item>Scope comes from the SIGNED JWT only: exactly one UUID each of <c>tenant_id</c>, <c>legal_entity_id</c>,
/// <c>sub</c>. No header-only fallback and no dev bypass — a header can confirm the token, never replace it.</item>
/// <item><c>X-Tenant-Id</c> present and ≠ token tenant → <b>403</b> (the family answers 400).</item>
/// <item><c>X-Legal-Entity-Id</c> (contract <c>LegalEntityOverride</c>) present and ≠ token LE → <b>403
/// CROSS_LE_FORBIDDEN</b>; multi-LE override is not granted in S1 (ASSUMPTION-0173-03).</item>
/// <item><c>tenantId</c> / <c>legalEntityId</c> in the query → <b>400 INVALID_REQUEST</b> (the body is checked by the
/// request reader).</item>
/// </list>
/// Runs after authentication and before authorization; an unauthenticated request passes through untouched so
/// authorization answers 401 first.
/// </summary>
public sealed class InventoryScopeResolutionMiddleware
{
    private const string TenantClaim = "tenant_id";
    private const string LegalEntityClaim = "legal_entity_id";
    private const string SubjectClaim = "sub";

    private static readonly string[] ForbiddenQueryKeys = ["tenantId", "legalEntityId", "tenant_id", "legal_entity_id"];

    private readonly RequestDelegate _next;
    private readonly ILogger<InventoryScopeResolutionMiddleware> _logger;

    public InventoryScopeResolutionMiddleware(RequestDelegate next, ILogger<InventoryScopeResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (HttpMethods.IsOptions(context.Request.Method)
            || !context.Request.Path.StartsWithSegments(InventoryContract.BasePath, StringComparison.OrdinalIgnoreCase)
            || context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var tenant = SingleUuid(context.User, TenantClaim);
        var legalEntity = SingleUuid(context.User, LegalEntityClaim);
        var subject = SingleUuid(context.User, SubjectClaim, ClaimTypes.NameIdentifier);
        if (tenant is null || legalEntity is null || subject is null)
        {
            _logger.LogWarning("Inventory scope refused: token lacks a single UUID tenant_id/legal_entity_id/sub. Path={Path}", context.Request.Path);
            await ContractErrorWriter.WriteAsync(context, StatusCodes.Status403Forbidden, null,
                "The access token must carry exactly one UUID tenant_id, legal_entity_id and sub.");
            return;
        }

        if (!HeaderMatches(context, InventoryContract.TenantHeader, tenant.Value))
        {
            _logger.LogWarning("Inventory scope refused: X-Tenant-Id differs from the token tenant. Path={Path}", context.Request.Path);
            await ContractErrorWriter.WriteAsync(context, StatusCodes.Status403Forbidden, null,
                $"{InventoryContract.TenantHeader} does not match the token tenant.");
            return;
        }

        if (!HeaderMatches(context, InventoryContract.LegalEntityOverrideHeader, legalEntity.Value))
        {
            _logger.LogWarning("Inventory scope refused: X-Legal-Entity-Id differs from the token legal entity. Path={Path}", context.Request.Path);
            await ContractErrorWriter.WriteAsync(context, StatusCodes.Status403Forbidden, InventoryErrorCodes.CrossLeForbidden,
                "Access to another legal entity is forbidden.");
            return;
        }

        if (ForbiddenQueryKeys.Any(k => context.Request.Query.Keys.Contains(k, StringComparer.OrdinalIgnoreCase)))
        {
            await ContractErrorWriter.WriteAsync(context, StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest,
                "tenantId/legalEntityId must not be sent: scope is taken from the access token.");
            return;
        }

        tenantContext.SetScope(tenant.Value, legalEntity.Value, subject.Value);
        await _next(context);
    }

    /// <summary>Exactly one claim across the given types, holding a non-empty UUID.</summary>
    private static Guid? SingleUuid(ClaimsPrincipal user, params string[] claimTypes)
    {
        var values = user.Claims.Where(c => claimTypes.Contains(c.Type, StringComparer.Ordinal)).Select(c => c.Value).ToList();
        return values.Count == 1 && Guid.TryParse(values[0], out var value) && value != Guid.Empty ? value : null;
    }

    /// <summary>Absent header → matches (the token decides). Present → must be exactly one value equal to the claim.</summary>
    private static bool HeaderMatches(HttpContext context, string header, Guid claim)
    {
        if (!context.Request.Headers.TryGetValue(header, out var values) || values.Count == 0)
        {
            return true;
        }

        return values.Count == 1 && Guid.TryParse(values[0], out var parsed) && parsed == claim;
    }
}
