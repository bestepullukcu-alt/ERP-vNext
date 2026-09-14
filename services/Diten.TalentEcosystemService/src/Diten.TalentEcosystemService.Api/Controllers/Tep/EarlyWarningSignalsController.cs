using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals;
using Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals.Commands;
using Diten.TalentEcosystemService.Application.Features.EarlyWarningSignals.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/early-warning-signals")]
[Authorize]
public sealed class EarlyWarningSignalsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public EarlyWarningSignalsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(EarlyWarningSignalsGuard.ReadPermission)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEarlyWarningSignalsReadinessListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(EarlyWarningSignalsGuard.ReadPermission)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEarlyWarningSignalsReadinessByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(EarlyWarningSignalsGuard.ManagePermission)]
    public async Task<IActionResult> Create([FromBody] EarlyWarningSignalsReadinessCreateRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateEarlyWarningSignalsReadinessCommand(request), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(EarlyWarningSignalsGuard.EvaluatePermission)]
    public async Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateEarlyWarningSignalsReadinessCommand(id), ct));

    [HttpDelete("{id:guid}")]
    [HasPermission(EarlyWarningSignalsGuard.ManagePermission)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new DeleteEarlyWarningSignalsReadinessCommand(id), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(EarlyWarningSignalsGuard.AuditReadPermission)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetEarlyWarningSignalsAuditMetadataQuery(id), ct));
}
