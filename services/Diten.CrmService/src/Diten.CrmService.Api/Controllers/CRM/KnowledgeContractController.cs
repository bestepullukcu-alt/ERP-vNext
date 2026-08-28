using Diten.CrmService.Application.Features.Knowledge.Contract;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.KnowledgePermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0162 FU02 — Knowledge contract surface (feature flags, in-domain vocabulary, supported filters, permissions,
/// reason codes, limitations). Canonical under <c>/api/crm/knowledge/contract</c>; exposed through the dedicated
/// <c>knowledge</c> ocelot routes. Permissions run on the documented fallback until MOD-0162-FU02-RBAC lands.
/// </summary>
[Authorize]
public sealed class KnowledgeContractController : CustomBaseController
{
    private readonly IMediator _mediator;

    public KnowledgeContractController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("api/crm/knowledge/contract")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> GetContract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetKnowledgeContractQuery(), cancellationToken));
}
