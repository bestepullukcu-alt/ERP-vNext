using Diten.InventoryService.Api.CycleCounting;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.CountEvidenceExports.Commands;
using Diten.InventoryService.Application.Features.CountEvidenceExports.Queries;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.CycleCounting;

/// <summary>
/// WMS-COUNT-BUNDLE draft §4.4 (DL-5, binding in DL-5; 1.1.0 additive) — the Evidence Pack under <c>/api/warehouse</c> (Q-15 = A; pack §3 r4 02/F-5:
/// the base is the controller's, the template names the whole path segment). GET / POST only. Every action carries its
/// <c>cycle-count.evidence.*</c> key (AC-44); the scope is the token's tenant + the selected company — another company's or tenant's count or export
/// is 404. The file's language is the request's <c>Accept-Language</c> (its first tag's primary subtag, one of the 7, else en — pack §4.5); the Web
/// proxy sends its page language. No business rule here.
/// </summary>
[Route("api/warehouse")]
[Authorize]
public sealed class CountEvidenceExportsController : CustomBaseController
{
    /// <summary>The body members of <c>createCountEvidenceExport</c> (draft §4.4) — any other member is 400.</summary>
    private static readonly HashSet<string> ExportMembers = new(StringComparer.Ordinal) { "idempotencyKey", "format" };

    private readonly IMediator _mediator;

    public CountEvidenceExportsController(IMediator mediator) => _mediator = mediator;

    [HttpPost("cycle-counts/{countId}/evidence-exports")]
    [HasPermission(CycleCountPermissions.EvidenceExport)]
    public async Task<IActionResult> Create(string countId, CancellationToken ct)
    {
        if (!CycleCountSupport.TryParseId(countId, out var id))
            return ContractFailure(StatusCodes.Status404NotFound, CycleCountErrorCodes.NotFound, "Cycle count not found.");
        var read = await CycleCountRequestReader.ReadAsync(Request.Body, ExportMembers, b => new ExportBody(b.String("format"), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (CycleCountSupport.KeyError(Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(), body.Key, out var key) is { } keyError)
            return Invalid(keyError);
        var language = Request.GetTypedHeaders().AcceptLanguage.OrderByDescending(l => l.Quality ?? 1).Select(l => l.Value.Value).FirstOrDefault();
        return CreateActionResultInstance(await _mediator.Send(
            new CreateCountEvidenceExportCommand(id, body.Format, key, CycleCountEvidenceText.Normal(language)), ct));
    }

    [HttpGet("count-evidence-exports")]
    [HasPermission(CycleCountPermissions.EvidenceRead)]
    public async Task<IActionResult> List([FromQuery] string? countId, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetCountEvidenceExportListQuery(countId, page, pageSize), ct));

    [HttpGet("count-evidence-exports/{exportId}/file")]
    [HasPermission(CycleCountPermissions.EvidenceRead)]
    public async Task<IActionResult> File(string exportId, CancellationToken ct)
    {
        if (!CycleCountSupport.TryParseId(exportId, out var id))
            return ContractFailure(StatusCodes.Status404NotFound, CycleCountErrorCodes.EvidenceNotFound, "Evidence export not found.");
        var result = await _mediator.Send(new GetCountEvidenceExportFileQuery(id), ct);
        if (!result.IsSuccessful || result.Data is not { } file) return CreateActionResultInstance(result);
        Response.Headers["X-Content-SHA256"] = file.Sha256;
        return File(file.Content, file.ContentType, file.FileName);
    }

    private sealed record ExportBody(string? Format, string? Key);

    private IActionResult Invalid(string? message)
        => ContractFailure(StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest, message ?? "The request is malformed.");
}
