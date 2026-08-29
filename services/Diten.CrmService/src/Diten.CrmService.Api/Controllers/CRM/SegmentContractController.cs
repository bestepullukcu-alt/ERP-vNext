using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Segmentation.SegmentPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0167 FU02 contract surface: what this FU can do, what it deliberately cannot, the in-domain vocabulary, the
/// published ceilings, and the CLOSED attribute catalog exactly as the runtime enforces it.
/// <para>These two endpoints exist so a criteria editor never hardcodes an attribute, an operator or a required
/// parameter — a hardcoded list is a second source of truth, and it drifts silently.</para>
/// </summary>
[Authorize]
public sealed class SegmentContractController : CustomBaseController
{
    private readonly IMediator _mediator;

    public SegmentContractController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/segments/contract")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Contract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetSegmentContractQuery(), cancellationToken));

    [HttpGet("api/crm/segments/attribute-catalog")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> AttributeCatalog(CancellationToken cancellationToken)
        => CreateActionResultInstance(
            await _mediator.Send(new GetSegmentAttributeCatalogQuery(), cancellationToken));
}
