using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact;
using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Commands;
using Diten.DevEnablementService.Application.Features.GoldenReferenceCompact.Queries;
using Diten.DevEnablementService.Infrastructure.Authorization;
using Diten.Shared.Core;
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

    /// <summary>
    /// The list, in two shapes (WP-UI-LIST-SERVER-01, BL-440 package 3).
    /// With ANY list parameter (start, length, search, orderBy, orderDir, draw, a filter) it is the server-mode page:
    /// `data = { items, total, filteredTotal }`, which the list factory translates for DataTables.
    /// With NONE it is the pre-server-mode answer, unchanged: `data = [ ...every row of the tenant... ]` — the MVC
    /// lookups proxy (Diten.Web GoldenReferenceCompactController.Lookups) reads that array to build the filter options.
    /// `draw` is accepted and not echoed: the factory stamps it back on the response itself.
    /// </summary>
    [HttpGet]
    [HasPermission("goldencompact.records.read")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? start,
        [FromQuery] int? length,
        [FromQuery] string? search,
        [FromQuery] string? orderBy,
        [FromQuery] string? orderDir,
        [FromQuery] int? draw,
        [FromQuery] string[]? status,
        [FromQuery] string[]? referenceType,
        [FromQuery] string[]? category,
        [FromQuery] string[]? owner,
        [FromQuery] int? priority,
        CancellationToken cancellationToken)
    {
        var isListRequest = start.HasValue || length.HasValue || search is not null || orderBy is not null || orderDir is not null
            || draw.HasValue || status is { Length: > 0 } || referenceType is { Length: > 0 } || category is { Length: > 0 }
            || owner is { Length: > 0 } || priority.HasValue;
        if (!isListRequest)
            return await WholeList(cancellationToken);

        var response = await _mediator.Send(new GetGoldenReferenceCompactListQuery(
            start, length, search, orderBy, orderDir, status, referenceType, category, owner, priority), cancellationToken);
        return CreateActionResultInstance(response);
    }

    private async Task<IActionResult> WholeList(CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetGoldenReferenceCompactListQuery(), cancellationToken);
        return CreateActionResultInstance(response.IsSuccessful
            ? Response<IReadOnlyList<GoldenReferenceCompactListItemDto>>.Success(response.Data!.Items, response.StatusCode)
            : Response<IReadOnlyList<GoldenReferenceCompactListItemDto>>.Fail(response.Errors, response.StatusCode));
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
    public Task<IActionResult> Export(CancellationToken cancellationToken) => WholeList(cancellationToken);

    // Reports summary = an aggregate read surface (reports.view). API-only for now (no frontend route yet), so it
    // is NOT a catalog page; the permission still exists system-wide via this gate.
    [HttpGet("reports/summary")]
    [HasPermission("goldencompact.reports.view")]
    public Task<IActionResult> ReportSummary(CancellationToken cancellationToken) => WholeList(cancellationToken);
}
