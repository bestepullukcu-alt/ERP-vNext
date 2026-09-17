using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.ReferenceExchange;
using Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Commands;
using Diten.TalentEcosystemService.Application.Features.ReferenceExchange.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/tep-reference-exchange")]
[Authorize]
public sealed class ReferenceExchangeController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ReferenceExchangeController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(ReferenceExchangePermissions.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetReferenceExchangeReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(ReferenceExchangePermissions.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetReferenceExchangeReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(ReferenceExchangePermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] ReferenceExchangeReadinessRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateReferenceExchangeReadinessCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(ReferenceExchangePermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ReferenceExchangeReadinessRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateReferenceExchangeReadinessCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission(ReferenceExchangePermissions.Manage)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveReferenceExchangeReadinessCommand(id), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(ReferenceExchangePermissions.Evaluate)]
    public async Task<IActionResult> Evaluate(Guid id, [FromBody] EvaluateReferenceExchangeReadinessRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateReferenceExchangeReadinessCommand(id, request), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(ReferenceExchangePermissions.AuditRead)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetReferenceExchangeAuditMetadataQuery(id), ct));
}
