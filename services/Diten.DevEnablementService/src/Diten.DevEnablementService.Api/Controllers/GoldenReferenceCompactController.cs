using Diten.BuildingBlocks.ListExport;
using Diten.DevEnablementService.Api.Export;
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

    /// <summary>
    /// BL-452 package 1 — THE FILE IS THE SCREEN, the reference implementation of the list-export contract:
    /// <c>GET api/golden-reference-compact/export?format=csv|xlsx&amp;columns=…</c> plus the list's own search / orderBy /
    /// orderDir / filters. It sends the list's query (GetGoldenReferenceCompactListQuery) through the same validator, so a
    /// bad parameter is the same 400 and the rows are the ones the reader filtered — all of them, up to 50 000.
    /// <para>⚠ <c>start</c> and <c>length</c> are deliberately NOT parameters: the file is every matching row, not the page on
    /// screen. Sent anyway, they cannot bind.</para>
    /// <para>Until BL-452 this route answered the whole list as JSON (no caller in the repository; measured 2026-09-25). The
    /// gate stays goldencompact.records.export — the self-registered key the manifest already declares.</para>
    /// </summary>
    [HttpGet("export")]
    [HasPermission("goldencompact.records.export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? format,
        [FromQuery] string[]? columns,
        [FromQuery] string? search,
        [FromQuery] string? orderBy,
        [FromQuery] string? orderDir,
        [FromQuery] string[]? status,
        [FromQuery] string[]? referenceType,
        [FromQuery] string[]? category,
        [FromQuery] string[]? owner,
        [FromQuery] int? priority,
        CancellationToken cancellationToken)
    {
        if (!ListExportContract.TryParseFormat(format, out var fileFormat))
            return ExportRefusal(400, "format must be 'csv' or 'xlsx'.", ListExportContract.FormatInvalidCode);
        if (!GoldenCompactExportColumns.Set.TryResolve(columns, out var exportColumns, out var columnsError))
            return ExportRefusal(400, columnsError!, ListExportContract.ColumnsInvalidCode);

        var response = await _mediator.Send(new GetGoldenReferenceCompactListQuery(
            Search: search, OrderBy: orderBy, OrderDir: orderDir, Status: status, ReferenceType: referenceType,
            Category: category, Owner: owner, Priority: priority, ExportRowCap: ListExportContract.MaxRows), cancellationToken);
        if (!response.IsSuccessful)
            return CreateActionResultInstance(response);

        // Either the count exceeded the cap (nothing was read) or the read — asked for cap + 1 — brought more than the cap.
        var matched = Math.Max(response.Data!.FilteredTotal, response.Data.Items.Count);
        if (matched > ListExportContract.MaxRows)
            return ExportRefusal(StatusCodes.Status413PayloadTooLarge,
                $"{matched} records match; an export carries at most {ListExportContract.MaxRows}. Narrow the filter.", ListExportContract.TooLargeCode);

        var culture = ListExportContract.ResolveCulture(Request.Headers.AcceptLanguage.ToString());
        var content = ListExportWriter.Write(fileFormat, exportColumns, response.Data.Items, culture, GoldenCompactExportColumns.Label("Title", culture));
        return File(content, ListExportContract.ContentType(fileFormat),
            ListExportContract.FileName(GoldenCompactExportColumns.Screen, DateTimeOffset.UtcNow, fileFormat));
    }

    // This service's envelope carries no machine codes; an export refusal speaks the same body as AuthService's
    // (isSuccessful, statusCode, errors, errorCodes[{ code }]) so the list factory reads one shape from every service.
    private ObjectResult ExportRefusal(int statusCode, string message, string code) => StatusCode(statusCode, new
    {
        data = (object?)null,
        statusCode,
        isSuccessful = false,
        errors = new[] { message },
        errorCodes = new[] { new { code } }
    });

    // Reports summary = an aggregate read surface (reports.view). API-only for now (no frontend route yet), so it
    // is NOT a catalog page; the permission still exists system-wide via this gate.
    [HttpGet("reports/summary")]
    [HasPermission("goldencompact.reports.view")]
    public Task<IActionResult> ReportSummary(CancellationToken cancellationToken) => WholeList(cancellationToken);
}
