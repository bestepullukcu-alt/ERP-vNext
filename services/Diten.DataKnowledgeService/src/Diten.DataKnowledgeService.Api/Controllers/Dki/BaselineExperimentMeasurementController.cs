using Diten.DataKnowledgeService.Api.Controllers.Common;
using Diten.DataKnowledgeService.Api.Security;
using Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement;
using Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement.Commands;
using Diten.DataKnowledgeService.Application.Features.BaselineExperimentMeasurement.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.DataKnowledgeService.Api.Controllers.Dki;

[Route("api/baseline-experiment-measurement")]
[Authorize]
public sealed class BaselineExperimentMeasurementController : CustomBaseController
{
    private readonly IMediator _mediator;

    public BaselineExperimentMeasurementController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(BaselineExperimentMeasurementGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetBaselineExperimentMeasurementReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(BaselineExperimentMeasurementGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetBaselineExperimentMeasurementReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(BaselineExperimentMeasurementGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] BaselineExperimentMeasurementReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateBaselineExperimentMeasurementReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(BaselineExperimentMeasurementGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateBaselineExperimentMeasurementReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(BaselineExperimentMeasurementGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteBaselineExperimentMeasurementReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(BaselineExperimentMeasurementGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetBaselineExperimentMeasurementAuditMetadataQuery(id), ct));
}
