using Diten.HumanCapitalService.Api.Controllers.Common;
using Diten.HumanCapitalService.Api.Security;
using Diten.HumanCapitalService.Application.Features.HrDocumentation;
using Diten.HumanCapitalService.Application.Features.HrDocumentation.Commands;
using Diten.HumanCapitalService.Application.Features.HrDocumentation.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HumanCapitalService.Api.Controllers.Hcm;

[Route("api/hr-documentation")]
[Authorize]
public sealed class HrDocumentationController : CustomBaseController
{
    private readonly IMediator _mediator;

    public HrDocumentationController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(HrDocumentationGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHrDocumentationReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(HrDocumentationGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHrDocumentationReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(HrDocumentationGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] HrDocumentationReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateHrDocumentationReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(HrDocumentationGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateHrDocumentationReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(HrDocumentationGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteHrDocumentationReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(HrDocumentationGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetHrDocumentationAuditMetadataQuery(id), ct));
}
