using Diten.ManufacturingService.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.ManufacturingService.Infrastructure.Middleware;

/// <summary>
/// Tenant + Legal-Entity çözümleme. Tenant: JWT claim (yoksa header); header ile token FARKLI tenant gösteriyorsa 400
/// (BL-323 deseni). Legal entity: çağıranın seçtiği <c>X-Legal-Entity-Id</c> (GET'te <c>legalEntityId</c> sorgusu da);
/// JWT'den OKUNMAZ, dev bypass YOK. Aitlik + aktiflik <see cref="LegalEntityValidationMiddleware"/>'da MDM'den kanıtlanır.
/// Cross-LE erişimi repository fail-closed 404 ile kapatır (MOD-0193 §8). Ret gövdesi BOM contract'ının Error şeklidir
/// (<c>{ error: { code, message, correlationId }, contractVersion }</c>) — tek servis, tek hata biçimi.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private const string TenantHeader = "X-Tenant-Id";
    private const string LegalEntityHeader = "X-Legal-Entity-Id";
    private const string TenantClaim = "tenant_id";
    private const string LegalEntityQuery = "legalEntityId";

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

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, ICorrelationContext correlation)
    {
        if (HttpMethods.IsOptions(context.Request.Method) || IsBypassPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var jwtTenant = ReadGuidClaim(context, TenantClaim);
        var headerTenant = ReadGuidHeader(context, TenantHeader);

        // Header ile token farklı tenant → malformed request → 400 (BL-323 deseni; sızıntı yok).
        if (jwtTenant.HasValue && headerTenant.HasValue && jwtTenant.Value != headerTenant.Value)
        {
            _logger.LogWarning(
                "Tenant mismatch in ManufacturingService. HeaderTenant={HeaderTenant} JwtTenant={JwtTenant} Path={Path}",
                headerTenant, jwtTenant, context.Request.Path);
            await WriteError(context, correlation, StatusCodes.Status400BadRequest, "Tenant mismatch",
                $"JWT tenant and '{TenantHeader}' must match.");
            return;
        }

        var resolvedTenant = jwtTenant ?? headerTenant;
        if (resolvedTenant is null && TryGetDevBypassGuid("TenantResolution:DevBypassTenantId", out var bypassTenant))
        {
            resolvedTenant = bypassTenant;
            _logger.LogWarning("TenantResolution dev bypass applied. Path={Path} TenantId={TenantId}",
                context.Request.Path, bypassTenant);
        }

        if (resolvedTenant is null)
        {
            _logger.LogWarning("Tenant context missing. Path={Path}", context.Request.Path);
            await WriteError(context, correlation, StatusCodes.Status400BadRequest, "Missing Tenant",
                $"'{TenantHeader}' header or JWT '{TenantClaim}' claim is required.");
            return;
        }

        // ── Legal-Entity: chosen by the caller, proven by MDM (MVP-1 pattern, MOD-0193 pack §8) ──────────────
        // The token carries no legal entity (the platform JWT has no such claim), and a dev bypass would make a page
        // look scoped when it is not — so neither is read. The caller names the legal entity it works in:
        //   • `X-Legal-Entity-Id` header, every request;
        //   • on GET only, `legalEntityId` query — a browser download (the shared list factory's export) cannot set a
        //     header. Both present → they must agree.
        // LegalEntityValidationMiddleware then proves the id belongs to this tenant and is ACTIVE (fail-closed).
        var headerLe = ReadGuidHeader(context, LegalEntityHeader, out var headerLeMalformed);
        Guid? queryLe = null;
        var queryLeMalformed = false;
        if (HttpMethods.IsGet(context.Request.Method) && context.Request.Query.TryGetValue(LegalEntityQuery, out var raw))
        {
            queryLeMalformed = raw.Count != 1 || !Guid.TryParse(raw[0], out var parsed) || parsed == Guid.Empty;
            queryLe = queryLeMalformed ? null : Guid.Parse(raw[0]!);
        }

        if (headerLeMalformed || queryLeMalformed)
        {
            await WriteError(context, correlation, StatusCodes.Status400BadRequest, "Invalid Legal-Entity",
                $"'{LegalEntityHeader}' (or '{LegalEntityQuery}' on GET) must be one non-empty UUID.", "LEGAL_ENTITY_REQUIRED");
            return;
        }

        if (headerLe.HasValue && queryLe.HasValue && headerLe.Value != queryLe.Value)
        {
            _logger.LogWarning("Legal-entity mismatch in ManufacturingService. HeaderLE={HeaderLe} QueryLE={QueryLe} Path={Path}",
                headerLe, queryLe, context.Request.Path);
            await WriteError(context, correlation, StatusCodes.Status400BadRequest, "Legal-entity mismatch",
                $"'{LegalEntityHeader}' and '{LegalEntityQuery}' must match.");
            return;
        }

        // FAIL-CLOSED: no legal entity → 400. Falling back to an empty one would pool every legal entity's records
        // under one key (a silent cross-LE leak).
        var resolvedLe = headerLe ?? queryLe;
        if (resolvedLe is null)
        {
            _logger.LogWarning("Legal-entity context missing. Path={Path}", context.Request.Path);
            await WriteError(context, correlation, StatusCodes.Status400BadRequest, "Missing Legal-Entity",
                $"'{LegalEntityHeader}' header is required (GET may use '{LegalEntityQuery}').", "LEGAL_ENTITY_REQUIRED");
            return;
        }

        tenantContext.SetTenant(resolvedTenant.Value, resolvedLe.Value);
        await _next(context);
    }

    private static Guid? ReadGuidClaim(HttpContext context, string claimType)
    {
        var claimValue = context.User.FindFirst(claimType)?.Value;
        return Guid.TryParse(claimValue, out var value) ? value : null;
    }

    private static Guid? ReadGuidHeader(HttpContext context, string header)
        => ReadGuidHeader(context, header, out _);

    private static Guid? ReadGuidHeader(HttpContext context, string header, out bool malformed)
    {
        malformed = false;
        if (!context.Request.Headers.TryGetValue(header, out var headerValue) || string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }

        var parsed = Guid.Empty;
        malformed = headerValue.Count != 1 || !Guid.TryParse(headerValue[0], out parsed) || parsed == Guid.Empty;
        return malformed ? null : parsed;
    }

    private static bool IsBypassPath(PathString path)
    {
        return path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
               || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteError(HttpContext context, ICorrelationContext correlation, int statusCode, string title, string detail,
        string code = "INVALID_REQUEST")
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new
        {
            error = new { code, message = $"{title}: {detail}", correlationId = correlation.CorrelationId },
            contractVersion = "v1"
        });
    }

    private bool TryGetDevBypassGuid(string configKey, out Guid value)
    {
        value = Guid.Empty;

        if (!_environment.IsDevelopment())
        {
            return false;
        }

        if (!_configuration.GetValue<bool>("TenantResolution:DevBypassEnabled"))
        {
            return false;
        }

        var raw = _configuration[configKey];
        if (!Guid.TryParse(raw, out value))
        {
            return false;
        }

        return true;
    }
}
