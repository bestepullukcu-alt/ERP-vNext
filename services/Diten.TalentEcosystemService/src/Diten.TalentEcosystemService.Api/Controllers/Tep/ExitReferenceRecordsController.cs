using Diten.TalentEcosystemService.Api.Controllers.Common;
using Diten.TalentEcosystemService.Api.Security;
using Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords;
using Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Commands;
using Diten.TalentEcosystemService.Application.Features.ExitReferenceRecords.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.TalentEcosystemService.Api.Controllers.Tep;

[Route("api/tep-exit-reference-records")]
[Authorize]
public sealed class ExitReferenceRecordsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ExitReferenceRecordsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [HasPermission(ExitReferenceRecordPermissions.Read)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetExitReferenceRecordListQuery(), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(ExitReferenceRecordPermissions.Read)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetExitReferenceRecordByIdQuery(id), ct));

    [HttpPost]
    [HasPermission(ExitReferenceRecordPermissions.Manage)]
    public async Task<IActionResult> Create([FromBody] ExitReferenceRecordRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new CreateExitReferenceRecordCommand(request), ct));

    [HttpPut("{id:guid}")]
    [HasPermission(ExitReferenceRecordPermissions.Manage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ExitReferenceRecordRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new UpdateExitReferenceRecordCommand(id, request), ct));

    [HttpPatch("{id:guid}/archive")]
    [HasPermission(ExitReferenceRecordPermissions.Manage)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new ArchiveExitReferenceRecordCommand(id), ct));

    [HttpPost("{id:guid}/evaluate")]
    [HasPermission(ExitReferenceRecordPermissions.Evaluate)]
    public async Task<IActionResult> Evaluate(Guid id, [FromBody] EvaluateExitReferenceRecordRequest request, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new EvaluateExitReferenceRecordCommand(id, request), ct));

    [HttpGet("{id:guid}/audit-metadata")]
    [HasPermission(ExitReferenceRecordPermissions.AuditRead)]
    public async Task<IActionResult> GetAuditMetadata(Guid id, CancellationToken ct) =>
        CreateActionResultInstance(await _mediator.Send(new GetExitReferenceRecordAuditMetadataQuery(id), ct));
}
