using Diten.Platform.API.Controllers.Common;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Navigation;
using Diten.Platform.Application.Features.Navigation.Queries;
using Diten.Platform.Common.Tenancy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

/// <summary>
/// MOD-0285 — runtime navigation loader. Tenant-scoped like the org directory (see TenantResolutionMiddleware
/// IsTenantScopedOrgPath): the tenant is resolved from the JWT tenant_id and a tenant_user actor is required.
/// No per-page <see cref="HasPermissionAttribute"/>: every tenant_user may fetch its own menu and the frontend
/// applies the per-item permission filter (Perms.Has) — the existing AccessGovernance/Organization pattern.
/// </summary>
[Route("api/platform/navigation")]
[Authorize]
public sealed class NavigationController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;

    public NavigationController(IMediator mediator, ITenantContext tenantContext)
    {
        _mediator = mediator;
        _tenantContext = tenantContext;
    }

    [HttpGet("menu")]
    public async Task<IActionResult> GetMenu(CancellationToken ct)
    {
        // Defence in depth: the middleware already resolves a tenant_user into a tenant context, but never
        // serve a menu under the platform scope or an unresolved/empty tenant (would cross tenant boundaries).
        if (!_tenantContext.IsResolved || _tenantContext.IsPlatformContext || _tenantContext.TenantId == Guid.Empty)
        {
            return CreateActionResultInstance(
                Response<IReadOnlyList<NavigationModuleGroupDto>>.Fail("Tenant context is required.", 403));
        }

        var response = await _mediator.Send(new GetTenantNavigationMenuQuery(_tenantContext.TenantId), ct);
        return CreateActionResultInstance(response);
    }
}
