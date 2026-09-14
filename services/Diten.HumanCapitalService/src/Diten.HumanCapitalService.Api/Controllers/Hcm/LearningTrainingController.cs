using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.LearningTraining;
using Diten.HumanCapitalService.Application.Features.LearningTraining.Commands;
using Diten.HumanCapitalService.Application.Features.LearningTraining.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/learning-training")]
[Authorize]
public sealed class LearningTrainingController : CustomBaseController
{
    private readonly IMediator _mediator;

    public LearningTrainingController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(LearningTrainingGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetLearningTrainingReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(LearningTrainingGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetLearningTrainingReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(LearningTrainingGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] LearningTrainingReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateLearningTrainingReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(LearningTrainingGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateLearningTrainingReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(LearningTrainingGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteLearningTrainingReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(LearningTrainingGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetLearningTrainingAuditMetadataQuery(id), ct));
}
