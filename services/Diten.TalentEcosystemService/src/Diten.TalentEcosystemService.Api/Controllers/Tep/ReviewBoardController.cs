using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.ReviewBoard;
using Diten.TalentEcosystemService.Application.Features.ReviewBoard.Commands;
using Diten.TalentEcosystemService.Application.Features.ReviewBoard.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/tep-review-board")]
[Authorize]
public sealed class ReviewBoardController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ReviewBoardController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(ReviewBoardPermissions.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetReviewBoardCaseListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(ReviewBoardPermissions.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetReviewBoardCaseByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(ReviewBoardPermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] ReviewBoardCaseRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateReviewBoardCaseCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(ReviewBoardPermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ReviewBoardCaseRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateReviewBoardCaseCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission(ReviewBoardPermissions.Manage)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveReviewBoardCaseCommand(id), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(ReviewBoardPermissions.Review)]
    public async Task<IActionResult> Evaluate(Guid id, [FromBody] EvaluateReviewBoardCaseRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateReviewBoardCaseCommand(id, request), ct));

    [HttpPost("{id:guid}/review")]
    [HasPermission(ReviewBoardPermissions.Review)]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewBoardDecisionRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ReviewReviewBoardCaseCommand(id, request), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(ReviewBoardPermissions.AuditRead)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetReviewBoardCaseAuditMetadataQuery(id), ct));
}
