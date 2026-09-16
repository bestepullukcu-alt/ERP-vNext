using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.PerformanceReviews;
using Diten.HumanCapitalService.Application.Features.PerformanceReviews.Commands;
using Diten.HumanCapitalService.Application.Features.PerformanceReviews.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/performance-reviews")]
[Authorize]
public sealed class PerformanceReviewsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PerformanceReviewsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(PerformanceReviewGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPerformanceReviewReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(PerformanceReviewGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPerformanceReviewReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(PerformanceReviewGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] PerformanceReviewReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreatePerformanceReviewReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(PerformanceReviewGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluatePerformanceReviewReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(PerformanceReviewGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeletePerformanceReviewReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(PerformanceReviewGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetPerformanceReviewAuditMetadataQuery(id), ct));
}
