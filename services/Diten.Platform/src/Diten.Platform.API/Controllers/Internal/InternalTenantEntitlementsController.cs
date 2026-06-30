using System.Security.Cryptography;
using System.Text;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

/// <summary>
/// FIX-2 — S2S read surface for AuthService's entitlement → role-permission reconcile. Returns a tenant's EFFECTIVE
/// entitled module codes (plan-derived + materialized, only those whose effective access is Active). Plan-derived
/// entitlement is virtual and emits no per-module events, so AuthService pulls the authoritative set from here at
/// tenant provisioning / plan change. Read-only; X-Internal-Api-Key gated like the other internal controllers.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/internal/tenants")]
public sealed class InternalTenantEntitlementsController : CustomBaseController
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private const string ActiveAccess = "Active";

    private readonly IMediator _mediator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalTenantEntitlementsController> _logger;

    public InternalTenantEntitlementsController(
        IMediator mediator,
        IConfiguration configuration,
        ILogger<InternalTenantEntitlementsController> logger)
    {
        _mediator = mediator;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("{tenantId:guid}/entitled-modules")]
    public async Task<IActionResult> GetEntitledModules(Guid tenantId, CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized())
        {
            _logger.LogWarning(
                "Internal entitled-modules request rejected. TenantId={TenantId} CorrelationId={CorrelationId}",
                tenantId,
                Request.Headers[CorrelationIdHeader].FirstOrDefault() ?? HttpContext.TraceIdentifier);
            return CreateActionResultInstance(Response<IReadOnlyList<string>>.Fail("Unauthorized.", 401));
        }

        var result = await _mediator.Send(new GetTenantModuleEntitlementsQuery(tenantId), ct);
        if (!result.IsSuccessful || result.Data is null)
        {
            return CreateActionResultInstance(Response<IReadOnlyList<string>>.Success(Array.Empty<string>()));
        }

        // Only modules the tenant can actually use right now (Active effective access), de-duplicated.
        var codes = result.Data
            .Where(r => string.Equals(r.EffectiveAccess, ActiveAccess, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.ModuleCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return CreateActionResultInstance(Response<IReadOnlyList<string>>.Success(codes));
    }

    // FIX-3 — richer surface for the catalog-key-driven sync: each effectively-Active entitled module plus the
    // permission keys it declares in the descriptor catalog. Lets AuthService grant a module's exact declared
    // permissions regardless of namespace (e.g. organization's platform.organization-units.* keys), instead of
    // relying on the Module==ModuleCode string convention. Same X-Internal-Api-Key gate.
    [HttpGet("{tenantId:guid}/entitled-modules-with-permissions")]
    public async Task<IActionResult> GetEntitledModulesWithPermissions(Guid tenantId, CancellationToken ct)
    {
        if (!IsInternalRequestAuthorized())
        {
            _logger.LogWarning(
                "Internal entitled-modules-with-permissions request rejected. TenantId={TenantId} CorrelationId={CorrelationId}",
                tenantId,
                Request.Headers[CorrelationIdHeader].FirstOrDefault() ?? HttpContext.TraceIdentifier);
            return CreateActionResultInstance(Response<IReadOnlyList<TenantEntitledModulePermissionsDto>>.Fail("Unauthorized.", 401));
        }

        var result = await _mediator.Send(new GetTenantEntitledModulePermissionsQuery(tenantId), ct);
        if (!result.IsSuccessful || result.Data is null)
        {
            return CreateActionResultInstance(
                Response<IReadOnlyList<TenantEntitledModulePermissionsDto>>.Success(Array.Empty<TenantEntitledModulePermissionsDto>()));
        }

        return CreateActionResultInstance(result);
    }

    private bool IsInternalRequestAuthorized()
    {
        var expected = _configuration["AuthService:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        if (!Request.Headers.TryGetValue(InternalApiKeyHeader, out var providedValues))
        {
            return false;
        }

        var provided = providedValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        return expectedBytes.Length == providedBytes.Length
               && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
