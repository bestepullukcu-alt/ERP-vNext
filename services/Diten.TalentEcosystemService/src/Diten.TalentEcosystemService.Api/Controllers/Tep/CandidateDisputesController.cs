using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.CandidateDisputes;
using Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Commands;
using Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/tep-candidate-disputes")]
[Authorize]
public sealed class CandidateDisputesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public CandidateDisputesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(CandidateDisputePermissions.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCandidateDisputeReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(CandidateDisputePermissions.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCandidateDisputeReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(CandidateDisputePermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] CandidateDisputeReadinessRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateCandidateDisputeReadinessCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(CandidateDisputePermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CandidateDisputeReadinessRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateCandidateDisputeReadinessCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission(CandidateDisputePermissions.Manage)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveCandidateDisputeReadinessCommand(id), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(CandidateDisputePermissions.Evaluate)]
    public async Task<IActionResult> Evaluate(Guid id, [FromBody] EvaluateCandidateDisputeReadinessRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateCandidateDisputeReadinessCommand(id, request), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(CandidateDisputePermissions.AuditRead)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetCandidateDisputeAuditMetadataQuery(id), ct));
}
