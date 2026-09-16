using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.MentorshipRecommendationNetwork;
using Diten.TalentEcosystemService.Application.Features.MentorshipRecommendationNetwork.Commands;
using Diten.TalentEcosystemService.Application.Features.MentorshipRecommendationNetwork.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/mentorship-recommendation-network")]
[Authorize]
public sealed class MentorshipRecommendationNetworkController : CustomBaseController
{
    private readonly IMediator _mediator;

    public MentorshipRecommendationNetworkController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(MentorshipRecommendationNetworkGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetMentorshipRecommendationNetworkReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(MentorshipRecommendationNetworkGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetMentorshipRecommendationNetworkReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(MentorshipRecommendationNetworkGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] MentorshipRecommendationNetworkReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateMentorshipRecommendationNetworkReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(MentorshipRecommendationNetworkGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateMentorshipRecommendationNetworkReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(MentorshipRecommendationNetworkGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteMentorshipRecommendationNetworkReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(MentorshipRecommendationNetworkGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetMentorshipRecommendationNetworkAuditMetadataQuery(id), ct));
}
