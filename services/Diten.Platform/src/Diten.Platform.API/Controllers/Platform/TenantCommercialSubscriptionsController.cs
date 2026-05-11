using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/tenants/{tenantId:guid}/commercial/subscription")]
[Authorize(Policy = "PlatformActor")]
public sealed class TenantCommercialSubscriptionsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TenantCommercialSubscriptionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("platform.tenants.commercial.subscription.view")]
    public async Task<IActionResult> GetCurrent(Guid tenantId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTenantCommercialSubscriptionQuery(tenantId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("history")]
    [HasPermission("platform.tenants.commercial.subscription.history.view")]
    public async Task<IActionResult> GetHistory(Guid tenantId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTenantSubscriptionHistoryQuery(tenantId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{subscriptionId:guid}")]
    [HasPermission("platform.tenants.commercial.subscription.view")]
    public async Task<IActionResult> GetDetail(Guid tenantId, Guid subscriptionId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTenantSubscriptionDetailQuery(tenantId, subscriptionId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("active")]
    [HasPermission("platform.tenants.commercial.subscription.view")]
    public async Task<IActionResult> HasActive(Guid tenantId, CancellationToken ct)
    {
        var response = await _mediator.Send(new HasTenantActiveSubscriptionQuery(tenantId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("entitlements")]
    [HasPermission("platform.tenants.commercial.subscription.view")]
    public async Task<IActionResult> GetEntitlements(Guid tenantId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTenantSubscriptionEntitlementSnapshotQuery(tenantId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    [HasPermission("platform.tenants.commercial.subscription.assign")]
    public async Task<IActionResult> Assign(Guid tenantId, [FromBody] AssignPlanToTenantRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new AssignPlanToTenantCommand(tenantId, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{subscriptionId:guid}/activate")]
    [HasPermission("platform.tenants.commercial.subscription.activate")]
    public async Task<IActionResult> Activate(Guid tenantId, Guid subscriptionId, [FromBody] ActivateTenantSubscriptionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new ActivateTenantSubscriptionCommand(tenantId, subscriptionId, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{subscriptionId:guid}/cancel")]
    [HasPermission("platform.tenants.commercial.subscription.cancel")]
    public async Task<IActionResult> Cancel(Guid tenantId, Guid subscriptionId, [FromBody] CancelTenantSubscriptionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new CancelTenantSubscriptionCommand(tenantId, subscriptionId, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{subscriptionId:guid}/renew")]
    [HasPermission("platform.tenants.commercial.subscription.renew")]
    public async Task<IActionResult> Renew(Guid tenantId, Guid subscriptionId, [FromBody] RenewTenantSubscriptionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new RenewTenantSubscriptionCommand(tenantId, subscriptionId, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{subscriptionId:guid}/expire")]
    [HasPermission("platform.tenants.commercial.subscription.expire")]
    public async Task<IActionResult> Expire(Guid tenantId, Guid subscriptionId, [FromBody] byte[]? rowVersion, CancellationToken ct)
    {
        var response = await _mediator.Send(new ExpireTenantSubscriptionCommand(tenantId, subscriptionId, rowVersion), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{subscriptionId:guid}/suspend")]
    [HasPermission("platform.tenants.commercial.subscription.suspend")]
    public async Task<IActionResult> Suspend(Guid tenantId, Guid subscriptionId, [FromBody] SuspendTenantSubscriptionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new SuspendTenantSubscriptionCommand(tenantId, subscriptionId, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{subscriptionId:guid}/reactivate")]
    [HasPermission("platform.tenants.commercial.subscription.reactivate")]
    public async Task<IActionResult> Reactivate(Guid tenantId, Guid subscriptionId, [FromBody] ReactivateTenantSubscriptionRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new ReactivateTenantSubscriptionCommand(tenantId, subscriptionId, request), ct);
        return CreateActionResultInstance(response);
    }
}
