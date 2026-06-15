using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.Quotas;
using Diten.Platform.Application.Features.Quotas.Commands;
using Diten.Platform.Application.Features.Quotas.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Platform;

[ApiController]
[Route("api/platform/tenants/{tenantId:guid}/quotas")]
[Authorize(Policy = "PlatformActor")]
public sealed class QuotasController : CustomBaseController
{
    private readonly IMediator _mediator;

    public QuotasController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("platform.tenants.quotas.read")]
    public async Task<IActionResult> GetAll(Guid tenantId, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTenantQuotaStatusQuery(tenantId), ct);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{quotaKey}")]
    [HasPermission("platform.tenants.quotas.read")]
    public async Task<IActionResult> GetByKey(Guid tenantId, string quotaKey, CancellationToken ct)
    {
        var response = await _mediator.Send(new GetTenantQuotaStatusByKeyQuery(tenantId, quotaKey), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("initialize")]
    [HasPermission("platform.tenants.quotas.manage")]
    public async Task<IActionResult> Initialize(Guid tenantId, [FromBody] InitializeTenantQuotasRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new InitializeTenantQuotasCommand(tenantId, request), ct);
        return CreateActionResultInstance(response);
    }

    [HttpPost("sync-limits")]
    [HasPermission("platform.tenants.quotas.manage")]
    public async Task<IActionResult> SyncLimits(Guid tenantId, [FromBody] SyncTenantQuotaLimitsRequest request, CancellationToken ct)
    {
        var response = await _mediator.Send(new SyncTenantQuotaLimitsFromSubscriptionCommand(tenantId, request), ct);
        return CreateActionResultInstance(response);
    }
}
