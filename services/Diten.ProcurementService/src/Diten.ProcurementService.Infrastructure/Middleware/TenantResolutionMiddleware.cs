using Diten.ProcurementService.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.ProcurementService.Infrastructure.Middleware;

/// <summary>
/// Tenant + Legal-Entity çözümleme. İkisi de SERVER-RESOLVED: önce JWT claim, sonra header; asla request payload'dan.
/// Header ile token FARKLI tenant/LE gösteriyorsa istek 400 ile reddedilir (payload contradiction, BL-323 deseni).
/// Cross-LE erişimi repository fail-closed 404 ile kapatır (MOD-0140 §8).
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private const string TenantHeader = "X-Tenant-Id";
    private const string LegalEntityHeader = "X-Legal-Entity-Id";
    private const string TenantClaim = "tenant_id";
    private const string LegalEntityClaim = "legal_entity_id";

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

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
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
                "Tenant mismatch in ProcurementService. HeaderTenant={HeaderTenant} JwtTenant={JwtTenant} Path={Path}",
                headerTenant, jwtTenant, context.Request.Path);
            await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Tenant mismatch",
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
            await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Missing Tenant",
                $"'{TenantHeader}' header or JWT '{TenantClaim}' claim is required.");
            return;
        }

        // ── Legal-Entity (server-resolved; claim → header → dev bypass) ──────────────
        var jwtLe = ReadGuidClaim(context, LegalEntityClaim);
        var headerLe = ReadGuidHeader(context, LegalEntityHeader);

        if (jwtLe.HasValue && headerLe.HasValue && jwtLe.Value != headerLe.Value)
        {
            _logger.LogWarning(
                "Legal-entity mismatch in ProcurementService. HeaderLE={HeaderLe} JwtLE={JwtLe} Path={Path}",
                headerLe, jwtLe, context.Request.Path);
            await WriteProblemDetails(context, StatusCodes.Status400BadRequest, "Legal-entity mismatch",
                $"JWT legal-entity and '{LegalEntityHeader}' must match.");
            return;
        }

        var resolvedLe = jwtLe ?? headerLe;
        if (resolvedLe is null && TryGetDevBypassGuid("TenantResolution:DevBypassLegalEntityId", out var bypassLe))
        {
            resolvedLe = bypassLe;
        }

        // LE zorunlu alan; scaffold aşamasında eksikse Guid.Empty ile tutarlı izolasyon (FAZ 2 fail-closed 400 sertleştirir).
        tenantContext.SetTenant(resolvedTenant.Value, resolvedLe ?? Guid.Empty);
        await _next(context);
    }

    private static Guid? ReadGuidClaim(HttpContext context, string claimType)
    {
        var claimValue = context.User.FindFirst(claimType)?.Value;
        return Guid.TryParse(claimValue, out var value) ? value : null;
    }

    private static Guid? ReadGuidHeader(HttpContext context, string header)
    {
        if (!context.Request.Headers.TryGetValue(header, out var headerValue) || string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }
        return Guid.TryParse(headerValue, out var value) ? value : null;
    }

    private static bool IsBypassPath(PathString path)
    {
        return path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
               || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
               || path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteProblemDetails(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(
            new
            {
                title,
                status = statusCode,
                detail,
                traceId = context.TraceIdentifier
            },
            options: null,
            contentType: "application/problem+json");
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
