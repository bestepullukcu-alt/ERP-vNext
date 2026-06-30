using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Commands;
using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Queries;
using Diten.DevEnablementService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.DevEnablementService.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/golden-reference-compact")]
public sealed class GoldenReferenceCompactController : CustomBaseController
{
    private readonly IMediator _mediator;

    public GoldenReferenceCompactController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [HasPermission("goldencompact.records.read")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetGoldenReferenceCompactListQuery(), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{id:guid}")]
    [HasPermission("goldencompact.records.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetGoldenReferenceCompactByIdQuery(id), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPost]
    [HasPermission("goldencompact.records.create")]
    public async Task<IActionResult> Create([FromBody] CreateGoldenReferenceCompactCommand command, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{id:guid}")]
    [HasPermission("goldencompact.records.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGoldenReferenceCompactCommand command, CancellationToken cancellationToken)
    {
        command.Id = id;
        var response = await _mediator.Send(command, cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("goldencompact.records.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new DeleteGoldenReferenceCompactCommand(id), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpDelete("bulk")]
    [HasPermission("goldencompact.records.delete")]
    public async Task<IActionResult> BulkDelete([FromBody] List<Guid> ids, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new BulkDeleteGoldenReferenceCompactCommand(ids), cancellationToken);
        return CreateActionResultInstance(response);
    }

    // Golden Compact is the richer demo tenant module: beyond CRUD it showcases two extra gated capabilities.
    // Export = the list payload behind the DataTable export toolbar button (records.export). Both reuse the list
    // query (the client formats/aggregates) — real, enforced endpoints so the permissions auto-register (A1) and
    // are not fabricated catalog entries.
    [HttpGet("export")]
    [HasPermission("goldencompact.records.export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetGoldenReferenceCompactListQuery(), cancellationToken);
        return CreateActionResultInstance(response);
    }

    // Reports summary = an aggregate read surface (reports.view). API-only for now (no frontend route yet), so it
    // is NOT a catalog page; the permission still exists system-wide via this gate.
    [HttpGet("reports/summary")]
    [HasPermission("goldencompact.reports.view")]
    public async Task<IActionResult> ReportSummary(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetGoldenReferenceCompactListQuery(), cancellationToken);
        return CreateActionResultInstance(response);
    }
}
