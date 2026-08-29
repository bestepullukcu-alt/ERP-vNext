using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementGovernancePolicyPack;
using Diten.Platform.Application.Features.DocumentManagementRetention;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0029-FU31A — TenantShell governance policy pack API (GMG-QMS-SOP-0001). Thin controller; dispatches via
/// MediatR.
///
/// BOUNDARY: <c>preview</c> is a pure computation — it creates no policy and writes no history. <c>apply</c> is
/// additive and idempotent: it creates ONLY the missing default policies, never overwrites an existing one,
/// evaluates no retention subject and mutates no subject state; a repeat apply records a new append-only history
/// row with 0 created. There is no DELETE verb anywhere in this controller.
///
/// PERMISSIONS: FU29 seeded no dedicated governance-policy-pack key, so this reuses the nearest seeded retention
/// keys (view for read, manage for apply) per the FU29A attribution rules — no unseeded key is invented here.
/// Future recommendation: platform.document-management.governance-policy-pack.view / .apply / .manage.
///
/// TenantId is never read from the client; it is resolved server-side from the tenant context.
/// </summary>
[ApiController]
[Route("api/v1/document-management/governance-policy-pack")]
[Authorize]
public sealed class DocumentManagementGovernancePolicyPackController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public DocumentManagementGovernancePolicyPackController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    /// <summary>Computes what an apply would create / skip / conflict on. Writes nothing.</summary>
    [HttpGet("default/preview")]
    [HasPermission(DocumentRetentionPermissions.RetentionView)]
    public async Task<IActionResult> Preview(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new PreviewGovernancePolicyPackQuery(CorrelationId), ct));

    /// <summary>Creates only the missing default policies and records an append-only application-history row.</summary>
    [HttpPost("default/apply")]
    [HasPermission(DocumentRetentionPermissions.RetentionManage)]
    public async Task<IActionResult> Apply(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ApplyGovernancePolicyPackCommand(CorrelationId), ct));

    [HttpGet("applications")]
    [HasPermission(DocumentRetentionPermissions.RetentionView)]
    public async Task<IActionResult> Applications(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetGovernancePolicyPackApplicationsQuery(CorrelationId), ct));

    [HttpGet("applications/{id:guid}")]
    [HasPermission(DocumentRetentionPermissions.RetentionView)]
    public async Task<IActionResult> ApplicationDetail(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetGovernancePolicyPackApplicationByIdQuery(id, CorrelationId), ct));

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId) ? HttpContext.TraceIdentifier : _correlationContext.CorrelationId!;
}
