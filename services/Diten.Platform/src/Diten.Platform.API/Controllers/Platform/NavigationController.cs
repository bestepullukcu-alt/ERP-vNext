using System.Text.Json;
using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Navigation;
using Diten.Platform.Application.Features.Navigation.Commands;
using Diten.Platform.Application.Features.Navigation.Queries;
using Diten.Platform.Common.Tenancy;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

/// <summary>
/// MOD-0285 — runtime navigation loader. Tenant-scoped like the org directory (see TenantResolutionMiddleware
/// IsTenantScopedOrgPath): the tenant is resolved from the JWT tenant_id and a tenant_user actor is required.
/// GET <c>menu</c> carries no <see cref="HasPermissionAttribute"/>: every tenant_user may fetch its OWN menu and the
/// frontend applies the per-item permission filter (Perms.Has) — the existing AccessGovernance/Organization pattern.
/// FIX-MENU-SETTINGS-ACCESS — but GET/PUT <c>preferences</c> mutate/read the tenant-WIDE sidebar, so those two are
/// gated per-action behind <c>platform.tenant-navigation.manage</c> (broken-access-control fix).
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

    // FEAT-TENANT-NAV-PREFS — the tenant's current sidebar preferences (Stage-2 UI reads this to render editor state).
    // FIX-MENU-SETTINGS-ACCESS — preferences READ/WRITE change the tenant-WIDE sidebar, so both are gated behind the
    // tenant self-service manage permission; only GET menu (a user rendering its OWN menu) stays open.
    [HttpGet("preferences")]
    [HasPermission("platform.tenant-navigation.manage")]
    public async Task<IActionResult> GetPreferences(CancellationToken ct)
    {
        if (!_tenantContext.IsResolved || _tenantContext.IsPlatformContext || _tenantContext.TenantId == Guid.Empty)
        {
            return CreateActionResultInstance(
                Response<TenantNavPreferencesDto>.Fail("Tenant context is required.", 403));
        }

        var response = await _mediator.Send(new GetTenantNavPreferencesQuery(_tenantContext.TenantId), ct);
        return CreateActionResultInstance(response);
    }

    // FEAT-TENANT-NAV-PREFS / -DOMAINS — replaces the tenant's ENTIRE preference set. Accepts the new
    // { modules:[...], domains:[...] } shape AND the legacy module-only array (backward compatible). Non-entitled /
    // unknown modules are ignored.
    [HttpPut("preferences")]
    [HasPermission("platform.tenant-navigation.manage")]
    public async Task<IActionResult> ReplacePreferences([FromBody] JsonElement body, CancellationToken ct)
    {
        if (!_tenantContext.IsResolved || _tenantContext.IsPlatformContext || _tenantContext.TenantId == Guid.Empty)
        {
            return CreateActionResultInstance(Response<NoContent>.Fail("Tenant context is required.", 403));
        }

        var (modules, domains) = ParsePreferencesBody(body);
        var response = await _mediator.Send(
            new ReplaceTenantNavPreferencesCommand(_tenantContext.TenantId, modules, domains),
            ct);
        return CreateActionResultInstance(response);
    }

    private static readonly JsonSerializerOptions BodyJsonOptions = new(JsonSerializerDefaults.Web);

    private static (IReadOnlyList<TenantNavPreferenceDto> Modules, IReadOnlyList<TenantNavDomainPreferenceDto> Domains) ParsePreferencesBody(JsonElement body)
    {
        // Legacy: a bare array is the module-only preference set.
        if (body.ValueKind == JsonValueKind.Array)
        {
            var modules = body.Deserialize<List<TenantNavPreferenceDto>>(BodyJsonOptions) ?? new();
            return (modules, Array.Empty<TenantNavDomainPreferenceDto>());
        }

        // New: { modules:[...], domains:[...] }.
        if (body.ValueKind == JsonValueKind.Object)
        {
            var wrapper = body.Deserialize<TenantNavPreferencesDto>(BodyJsonOptions);
            return (
                wrapper?.Modules ?? Array.Empty<TenantNavPreferenceDto>(),
                wrapper?.Domains ?? Array.Empty<TenantNavDomainPreferenceDto>());
        }

        return (Array.Empty<TenantNavPreferenceDto>(), Array.Empty<TenantNavDomainPreferenceDto>());
    }
}
