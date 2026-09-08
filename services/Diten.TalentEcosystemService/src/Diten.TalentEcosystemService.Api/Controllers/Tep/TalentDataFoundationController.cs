using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.TalentDataFoundation;
using Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Commands;
using Diten.TalentEcosystemService.Application.Features.TalentDataFoundation.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/talent-data-foundation")]
[Authorize]
public sealed class TalentDataFoundationController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TalentDataFoundationController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(TalentDataFoundationGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTalentDataFoundationReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(TalentDataFoundationGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTalentDataFoundationReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(TalentDataFoundationGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] TalentDataFoundationReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateTalentDataFoundationReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(TalentDataFoundationGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateTalentDataFoundationReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(TalentDataFoundationGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteTalentDataFoundationReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(TalentDataFoundationGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTalentDataFoundationAuditMetadataQuery(id), ct));
}
