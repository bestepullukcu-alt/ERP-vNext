using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Application.Features.Tenants.Commands;
using Diten.Platform.Application.Features.Tenants.Queries;
using Diten.Platform.Common.Tenancy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

/// <summary>
/// FU17-FU01 — tenant-admin self-service for the tenant's OWN login/security settings. Tenant-scoped like the
/// org directory (TenantResolutionMiddleware.IsTenantScopedOrgPath): the tenant is resolved from the JWT
/// tenant_id and a tenant_user actor is required. Reuses the existing Get/Update TenantLoginSettings handlers,
/// but the tenant id is taken from the resolved context (never from the route) — a tenant can only manage itself.
/// </summary>
[Route("api/platform/tenant-security")]
[Authorize]
public sealed class TenantSecuritySettingsController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;

    public TenantSecuritySettingsController(IMediator mediator, ITenantContext tenantContext)
    {
        _mediator = mediator;
        _tenantContext = tenantContext;
    }

    [HttpGet("login-settings")]
    [HasPermission("platform.tenant-security.read")]
    public async Task<IActionResult> GetLoginSettings(CancellationToken ct)
    {
        if (!TryResolveTenant(out var tenantId))
        {
            return CreateActionResultInstance(Response<TenantLoginSettingsDto>.Fail("Tenant context is required.", 403));
        }

        var result = await _mediator.Send(new GetTenantLoginSettingsQuery(tenantId), ct);
        return result is null
            ? CreateActionResultInstance(Response<TenantLoginSettingsDto>.Fail("Tenant login settings not found.", 404))
            : CreateActionResultInstance(Response<TenantLoginSettingsDto>.Success(result));
    }

    [HttpPut("login-settings")]
    [HasPermission("platform.tenant-security.manage")]
    public async Task<IActionResult> UpdateLoginSettings([FromBody] TenantLoginSettingsUpdateRequest request, CancellationToken ct)
    {
        if (!TryResolveTenant(out var tenantId))
        {
            return CreateActionResultInstance(Response<TenantLoginSettingsDto>.Fail("Tenant context is required.", 403));
        }

        var result = await _mediator.Send(new UpdateTenantLoginSettingsCommand(tenantId, request), ct);
        return result is null
            ? CreateActionResultInstance(Response<TenantLoginSettingsDto>.Fail("Tenant login settings not found.", 404))
            : CreateActionResultInstance(Response<TenantLoginSettingsDto>.Success(result));
    }

    private bool TryResolveTenant(out Guid tenantId)
    {
        tenantId = _tenantContext.IsResolved ? _tenantContext.TenantId : Guid.Empty;
        return _tenantContext is { IsResolved: true, IsPlatformContext: false } && tenantId != Guid.Empty;
    }
}
