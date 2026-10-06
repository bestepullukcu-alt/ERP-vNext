using Diten.BuildingBlocks.ListExport;
using Diten.ManufacturingService.Api.Export;
using Diten.ManufacturingService.Application.Features.Boms;
using Diten.ManufacturingService.Application.Features.Boms.Commands;
using Diten.ManufacturingService.Application.Features.Boms.Queries;
using Diten.ManufacturingService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ManufacturingService.Api.Controllers;

/// <summary>
/// MOD-0193 BOM &amp; Routings API — <c>bom.openapi.yaml</c> (x-owner MOD-0193). Frozen v1.0.0: getCurrentBom,
/// getBomVersion, explodeBom. Additive v1.1.0: liste, taslak oluştur/düzenle/sil, yürürlüğe al, geçmiş. Tenant + LE
/// server-resolved (TenantResolutionMiddleware); gövdede yok. Her uç <c>[HasPermission("manufacturing.bom.*")]</c>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/bom")]
public sealed class BomController(IMediator mediator) : CustomBaseController
{
    /// <summary>
    /// listBomVersions (v1.1.0) — the platform server-mode list contract (BL-440): <c>start/length/search/orderBy/orderDir</c>
    /// + <c>status</c> (repeatable) and <c>itemId</c>; answers <c>{ data: { items, total, filteredTotal }, contractVersion }</c>,
    /// the shape the shared list factory reads.
    /// </summary>
    [HttpGet("versions")]
    [HasPermission(BomPermissions.Read)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? itemId, [FromQuery] string[]? status, [FromQuery] string? search, [FromQuery] string? orderBy, [FromQuery] string? orderDir,
        [FromQuery] int start = 0, [FromQuery] int length = 25, CancellationToken ct = default)
    {
        var response = await mediator.Send(new GetBomListQuery(itemId, status, search, orderBy, orderDir, start, length), ct);
        return response.IsSuccessful
            ? Ok(new { data = response.Data, contractVersion = ContractErrors.BomContractVersion })
            : CreateActionResultInstance(response);
    }

    /// <summary>
    /// exportBomVersions (v1.1.0) — BL-452: the list's own query (same validator, same filters, search and order), every
    /// matching row up to 50 000, the visible columns in screen order, headers in the request culture. More → 413.
    /// </summary>
    [HttpGet("versions/export")]
    [HasPermission(BomPermissions.Export)]
    public async Task<IActionResult> Export(
        [FromQuery] string? format, [FromQuery] string[]? columns, [FromQuery] Guid? itemId, [FromQuery] string[]? status,
        [FromQuery] string? search, [FromQuery] string? orderBy, [FromQuery] string? orderDir, CancellationToken ct)
    {
        if (!ListExportContract.TryParseFormat(format, out var fileFormat))
            return StatusCode(400, ContractError(ListExportContract.FormatInvalidCode, "format must be 'csv' or 'xlsx'."));
        if (!BomExportColumns.Set.TryResolve(columns, out var exportColumns, out var columnsError))
            return StatusCode(400, ContractError(ListExportContract.ColumnsInvalidCode, columnsError!));

        var response = await mediator.Send(new GetBomListQuery(itemId, status, search, orderBy, orderDir, ExportRowCap: ListExportContract.MaxRows), ct);
        if (!response.IsSuccessful)
            return CreateActionResultInstance(response);

        var matched = Math.Max(response.Data!.FilteredTotal, response.Data.Items.Count);
        if (matched > ListExportContract.MaxRows)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, ContractError(ListExportContract.TooLargeCode,
                $"{matched} BOM versions match; an export carries at most {ListExportContract.MaxRows}. Narrow the filter."));

        var culture = ListExportContract.ResolveCulture(Request.Headers.AcceptLanguage.ToString());
        var content = ListExportWriter.Write(fileFormat, exportColumns, response.Data.Items, culture, BomExportColumns.Label("Title", culture));
        return File(content, ListExportContract.ContentType(fileFormat), ListExportContract.FileName(BomExportColumns.Screen, DateTimeOffset.UtcNow, fileFormat));
    }

    [HttpPost("versions")]
    [HasPermission(BomPermissions.Create)]
    public async Task<IActionResult> Create([FromBody] CreateBomDraftRequest body, CancellationToken ct) =>
        CreateActionResultInstance(await mediator.Send(new CreateBomDraftCommand(body), ct));

    /// <summary>getBomVersion (frozen).</summary>
    [HttpGet("version/{bomVersionId:guid}")]
    [HasPermission(BomPermissions.Read)]
    public async Task<IActionResult> GetVersion(Guid bomVersionId, CancellationToken ct) =>
        CreateActionResultInstance(await mediator.Send(new GetBomVersionQuery(bomVersionId), ct));

    [HttpPut("version/{bomVersionId:guid}")]
    [HasPermission(BomPermissions.Update)]
    public async Task<IActionResult> Update(Guid bomVersionId, [FromBody] UpdateBomDraftRequest body, CancellationToken ct) =>
        CreateActionResultInstance(await mediator.Send(new UpdateBomDraftCommand(bomVersionId, body), ct));

    [HttpPost("version/{bomVersionId:guid}/release")]
    [HasPermission(BomPermissions.Release)]
    public async Task<IActionResult> Release(Guid bomVersionId, [FromBody] ReleaseBomVersionRequest body, CancellationToken ct) =>
        CreateActionResultInstance(await mediator.Send(new ReleaseBomVersionCommand(bomVersionId, body), ct));

    [HttpDelete("version/{bomVersionId:guid}")]
    [HasPermission(BomPermissions.Delete)]
    public async Task<IActionResult> Delete(Guid bomVersionId, [FromQuery] int rowVersion, CancellationToken ct) =>
        CreateActionResultInstance(await mediator.Send(new DeleteBomDraftCommand(bomVersionId, rowVersion), ct));

    [HttpGet("version/{bomVersionId:guid}/history")]
    [HasPermission(BomPermissions.Read)]
    public async Task<IActionResult> History(Guid bomVersionId, CancellationToken ct) =>
        CreateActionResultInstance(await mediator.Send(new GetBomHistoryQuery(bomVersionId), ct));

    /// <summary>explodeBom (frozen) — POST ama salt okuma.</summary>
    [HttpPost("explode")]
    [HasPermission(BomPermissions.Read)]
    public async Task<IActionResult> Explode([FromBody] ExplodeBomRequest body, CancellationToken ct) =>
        CreateActionResultInstance(await mediator.Send(new ExplodeBomQuery(body), ct));

    /// <summary>getCurrentBom (frozen). Sabit yollardan (versions, version/…, explode) sonra eşleşir: itemId bir uuid.</summary>
    [HttpGet("{itemId:guid}/current")]
    [HasPermission(BomPermissions.Read)]
    public async Task<IActionResult> GetCurrent(Guid itemId, [FromQuery] DateOnly? asOfDate, CancellationToken ct) =>
        CreateActionResultInstance(await mediator.Send(new GetCurrentBomQuery(itemId, asOfDate), ct));
}
