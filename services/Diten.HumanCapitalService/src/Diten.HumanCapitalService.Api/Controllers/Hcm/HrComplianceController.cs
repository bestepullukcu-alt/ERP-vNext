using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.HrCompliance;
using Diten.HumanCapitalService.Application.Features.HrCompliance.Commands;
using Diten.HumanCapitalService.Application.Features.HrCompliance.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/hr-compliance")]
[Authorize]
public sealed class HrComplianceController : CustomBaseController
{
    private readonly IMediator _mediator;

    public HrComplianceController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(HrComplianceGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHrComplianceReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(HrComplianceGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHrComplianceReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(HrComplianceGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] HrComplianceReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateHrComplianceReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(HrComplianceGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateHrComplianceReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(HrComplianceGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteHrComplianceReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(HrComplianceGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHrComplianceAuditMetadataQuery(id), ct));
}
