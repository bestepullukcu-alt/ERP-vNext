using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/tenants/{tenantId:guid}/commercial/module-entitlements")]
[Authorize(Policy = "PlatformActor")]
public sealed class TenantModuleEntitlementsController : CustomBaseController
{
    private const string ViewPermission = "platform.tenants.commercial.subscription.view";
    private const string AssignPermission = "platform.tenants.commercial.subscription.assign";

    private readonly IMediator _mediator;

    public TenantModuleEntitlementsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission(ViewPermission)]
    public async Task<IActionResult> Get(Guid tenantId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTenantModuleEntitlementsQuery(tenantId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("effective-access/{moduleCode}")]
    [HasPermission(ViewPermission)]
    public async Task<IActionResult> GetEffectiveAccess(Guid tenantId, string moduleCode, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTenantModuleEffectiveAccessQuery(tenantId, moduleCode), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("visible-modules")]
    [HasPermission(ViewPermission)]
    public async Task<IActionResult> GetVisibleModules(Guid tenantId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTenantVisibleModulesQuery(tenantId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("available-modules")]
    [HasPermission(AssignPermission)]
    public async Task<IActionResult> GetAvailableModules(Guid tenantId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTenantAvailableModulesForAssignmentQuery(tenantId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    [HasPermission(AssignPermission)]
    public async Task<IActionResult> Add(Guid tenantId, [FromBody] TenantModuleEntitlementRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new AddTenantModuleEntitlementCommand(tenantId, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{entitlementId:guid}/enable")]
    [HasPermission(AssignPermission)]
    public async Task<IActionResult> Enable(Guid tenantId, Guid entitlementId, [FromBody] byte[]? rowVersion, CancellationToken ct)
    {
        var response = await _mediator.Send(new EnableTenantModuleEntitlementCommand(tenantId, entitlementId, rowVersion), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("disable")]
    [HasPermission(AssignPermission)]
    public async Task<IActionResult> Disable(Guid tenantId, [FromBody] DisableTenantModuleEntitlementRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new DisableTenantModuleEntitlementCommand(tenantId, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPatch("{entitlementId:guid}/expiry")]
    [HasPermission(AssignPermission)]
    public async Task<IActionResult> UpdateExpiry(Guid tenantId, Guid entitlementId, [FromBody] UpdateTenantModuleEntitlementExpiryRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new UpdateTenantModuleEntitlementExpiryCommand(tenantId, entitlementId, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{entitlementId:guid}/manual-override")]
    [HasPermission(AssignPermission)]
    public async Task<IActionResult> RemoveManualOverride(Guid tenantId, Guid entitlementId, [FromBody] RemoveTenantManualModuleOverrideRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new RemoveTenantManualModuleOverrideCommand(tenantId, entitlementId, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("refresh-projection")]
    [HasPermission(AssignPermission)]
    public async Task<IActionResult> RefreshProjection(Guid tenantId, CancellationToken ct)
    {
        var response = await _mediator.Send(new RefreshTenantModuleEntitlementProjectionCommand(tenantId), ct);
        return CreateActionResultInstance(response);
    }
}
