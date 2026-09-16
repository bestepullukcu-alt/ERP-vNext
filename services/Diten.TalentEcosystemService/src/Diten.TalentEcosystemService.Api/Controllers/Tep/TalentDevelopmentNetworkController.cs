using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork;
using Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork.Commands;
using Diten.TalentEcosystemService.Application.Features.TalentDevelopmentNetwork.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/talent-development-network")]
[Authorize]
public sealed class TalentDevelopmentNetworkController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TalentDevelopmentNetworkController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(TalentDevelopmentNetworkGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTalentDevelopmentNetworkReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(TalentDevelopmentNetworkGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTalentDevelopmentNetworkReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(TalentDevelopmentNetworkGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] TalentDevelopmentNetworkReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateTalentDevelopmentNetworkReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(TalentDevelopmentNetworkGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateTalentDevelopmentNetworkReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(TalentDevelopmentNetworkGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteTalentDevelopmentNetworkReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(TalentDevelopmentNetworkGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetTalentDevelopmentNetworkAuditMetadataQuery(id), ct));
}
