using Diten.CrmService.Application.Features.Knowledge.Path.Contract;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.Path.KnowledgePathPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0162 FU04 — KnowledgePath contract surface (feature flags, in-domain vocabulary, supported filters, limits,
/// permissions, reason codes, limitations). Canonical under <c>/api/crm/knowledge/path/contract</c>; exposed through the
/// existing <c>knowledge</c> ocelot wildcard. Permissions run on the documented DEV-ONLY fallback until FU04-RBAC lands.
/// </summary>
[Authorize]
public sealed class KnowledgePathContractController : CustomBaseController
{
    private readonly IMediator _mediator;

    public KnowledgePathContractController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/knowledge/path/contract")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> GetContract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetKnowledgePathContractQuery(), cancellationToken));
}
