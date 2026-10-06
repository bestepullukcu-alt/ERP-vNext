using System.Security.Claims;
using Diten.BuildingBlocks.ListExport;
using Diten.MdmService.Api.Export;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.Brand;
using Diten.MdmService.Application.Features.Brand.Commands;
using Diten.MdmService.Application.Features.Brand.Queries;
using Diten.MdmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.MdmService.Api.Controllers;

/// <summary>
/// MOD-0290-FU02 — Brand master. MOD-0290 is the Source of Truth; CRM / Knowledge / Campaign / Frequency
/// consume BrandId by reference only.
///
/// There is deliberately NO DELETE action: FU01 §3 forbids hard delete, so the verb does not exist at the
/// controller, the command layer, or the gateway route. Archiving is a POST to <c>{brandId}/archive</c>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/mdm/brands")]
public sealed class BrandsController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly IRequestCorrelation _correlation;

    public BrandsController(IMediator mediator, IRequestCorrelation correlation)
    {
        _mediator = mediator;
        _correlation = correlation;
    }

    [HttpGet]
    [HasPermission("mdm.brands.read")]
    public async Task<IActionResult> GetList([FromQuery] GetBrandListQuery query, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(query, cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// WP-MDM-BRAND-ACCEPT-01 FIX2 (item 1, BL-452) — THE FILE IS THE SCREEN: <c>GET api/mdm/brands/export?format=csv|xlsx
    /// &amp;columns=…</c> plus the register's own search / orderBy / orderDir / brandStatus / includeArchived. The same
    /// filters and the same 400 as the list; every matching row up to 50 000 (more is 413 EXPORT_TOO_LARGE, no partial
    /// file). Its own key, mdm.brands.export (brand-product-master): reading a brand does not mean taking all of them away.
    /// </summary>
    [HttpGet("export")]
    [HasPermission("mdm.brands.export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? format,
        [FromQuery] string[]? columns,
        [FromQuery] string? search,
        [FromQuery] string? orderBy,
        [FromQuery] string? orderDir,
        [FromQuery] string? brandStatus,
        [FromQuery] bool includeArchived,
        CancellationToken cancellationToken)
    {
        if (!ListExportContract.TryParseFormat(format, out var fileFormat))
            return ExportRefusal(400, "format must be 'csv' or 'xlsx'.", ListExportContract.FormatInvalidCode);
        if (!BrandExportColumns.Set.TryResolve(columns, out var exportColumns, out var columnsError))
            return ExportRefusal(400, columnsError!, ListExportContract.ColumnsInvalidCode);

        var response = await _mediator.Send(new GetBrandExportQuery(
            new GetBrandListQuery
            {
                Search = search, OrderBy = orderBy, OrderDir = orderDir, BrandStatus = brandStatus, IncludeArchived = includeArchived
            },
            ListExportContract.MaxRows), cancellationToken);
        if (!response.IsSuccessful)
            return CreateActionResultInstance(response);

        var matched = Math.Max(response.Data!.TotalCount, response.Data.Items.Count);
        if (matched > ListExportContract.MaxRows)
            return ExportRefusal(StatusCodes.Status413PayloadTooLarge,
                $"{matched} records match; an export carries at most {ListExportContract.MaxRows}. Narrow the filter.", ListExportContract.TooLargeCode);

        var culture = ListExportContract.ResolveCulture(Request.Headers.AcceptLanguage.ToString());
        var content = ListExportWriter.Write(fileFormat, exportColumns, response.Data.Items, culture, BrandExportColumns.Label("Title", culture));
        return File(content, ListExportContract.ContentType(fileFormat),
            ListExportContract.FileName(BrandExportColumns.Screen, DateTimeOffset.UtcNow, fileFormat));
    }

    // The refusal body every list-export service speaks (isSuccessful, statusCode, errors, errorCodes[{ code }]).
    private ObjectResult ExportRefusal(int statusCode, string message, string code) => StatusCode(statusCode, new
    {
        data = (object?)null,
        statusCode,
        isSuccessful = false,
        errors = new[] { message },
        errorCodes = new[] { new { code } }
    });

    [HttpGet("{brandId:guid}")]
    [HasPermission("mdm.brands.read")]
    public async Task<IActionResult> GetById(Guid brandId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetBrandByIdQuery(brandId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>Read-only brand → products relation for the Brand detail Products tab.</summary>
    [HttpGet("{brandId:guid}/products")]
    [HasPermission("mdm.brands.read")]
    public async Task<IActionResult> GetProducts(Guid brandId, [FromQuery] bool includeArchived, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetBrandProductsQuery(brandId, includeArchived), cancellationToken);
        return CreateActionResultInstance(response);
    }

    // TenantId is absent from BrandWriteRequest by design and is resolved from the tenant context, so a caller
    // cannot influence it even by sending one.
    [HttpPost]
    [HasPermission("mdm.brands.create")]
    public async Task<IActionResult> Create([FromBody] BrandWriteRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new CreateBrandCommand(request, Actor, CorrelationId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPut("{brandId:guid}")]
    [HasPermission("mdm.brands.update")]
    public async Task<IActionResult> Update(Guid brandId, [FromBody] BrandWriteRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new UpdateBrandCommand(brandId, request, Actor, CorrelationId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{brandId:guid}/archive")]
    [HasPermission("mdm.brands.archive")]
    public async Task<IActionResult> Archive(Guid brandId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ArchiveBrandCommand(brandId, Actor, CorrelationId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    /// <summary>
    /// WP-MDM-BRAND-ACCEPT-01 FIX1 (item 1) — ties the brand's history entry to the request. FIX2 (M2) — the request's ONE
    /// normalised id (Guid "D"): the caller's X-Correlation-Id when it is a Guid, else a fresh one — the same value the
    /// central audit event carries.
    /// </summary>
    private string CorrelationId => _correlation.CorrelationId.ToString("D");

    /// <summary>Audit identity only — authorization is enforced by [HasPermission], never by this value.</summary>
    private string? Actor =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? User.Identity?.Name;
}
