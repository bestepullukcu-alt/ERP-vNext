using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.RehireRecommendations;
using Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Commands;
using Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/tep-rehire-recommendations")]
[Authorize]
public sealed class RehireRecommendationsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public RehireRecommendationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(RehireRecommendationPermissions.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetRehireRecommendationReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(RehireRecommendationPermissions.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetRehireRecommendationReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(RehireRecommendationPermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] RehireRecommendationReadinessRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateRehireRecommendationReadinessCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(RehireRecommendationPermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] RehireRecommendationReadinessRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateRehireRecommendationReadinessCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission(RehireRecommendationPermissions.Manage)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveRehireRecommendationReadinessCommand(id), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(RehireRecommendationPermissions.Evaluate)]
    public async Task<IActionResult> Evaluate(Guid id, [FromBody] EvaluateRehireRecommendationReadinessRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateRehireRecommendationReadinessCommand(id, request), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(RehireRecommendationPermissions.AuditRead)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetRehireRecommendationAuditMetadataQuery(id), ct));
}
