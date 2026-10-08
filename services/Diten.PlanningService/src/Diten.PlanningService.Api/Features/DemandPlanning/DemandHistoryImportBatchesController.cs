using System.Security.Claims;
using System.Text.Json.Serialization;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning.HistoryImports;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PlanningService.Api.Features.DemandPlanning;

[Route("api/v2/demand/history-import-batches")]
public sealed class DemandHistoryImportBatchesController : CustomBaseController
{
    private readonly ISender _sender;
    public DemandHistoryImportBatchesController(ISender sender) => _sender = sender;

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(1_200_000)]
    [HasPermission("demand.history-imports.import")]
    public async Task<IActionResult> Create([FromForm] CreateHistoryImportForm form,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? selectedLegalEntityHint,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(out var tenantId, out var actorId)) return Forbid();
        if (Request.Form.Keys.Any(key =>
            key.Equals("LegalEntityId", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("TenantId", StringComparison.OrdinalIgnoreCase)))
            return BadRequest(Response<HistoryImportBatchResult>.Fail(
                "Tenant and LegalEntity cannot be supplied in import form data."));
        if (form.File is null || form.File.Length == 0 ||
            !string.Equals(Path.GetExtension(form.File.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(Response<HistoryImportBatchResult>.Fail("A nonempty CSV file is required."));
        if (form.File.Length > 1_048_576)
            return StatusCode(413, Response<HistoryImportBatchResult>.Fail("Import file exceeds 1 MiB.", 413));
        await using var stream = new MemoryStream();
        await form.File.CopyToAsync(stream, cancellationToken);
        var command = new CreateDemandHistoryImportBatchCommand(
            tenantId, actorId, selectedLegalEntityHint ?? Guid.Empty, form.SourceSystem,
            Path.GetFileName(form.File.FileName), form.ScopeFrom, form.ScopeThrough,
            form.WarehouseScope ?? [], stream.ToArray(), idempotencyKey ?? string.Empty);
        var response = await _sender.Send(command, cancellationToken);
        if (response.StatusCode == 201 && response.Data is not null)
            return CreatedAtAction(nameof(GetOwn), new { batchId = response.Data.BatchId }, response);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{batchId:guid}")]
    [HasPermission("demand.history-imports.import")]
    public async Task<IActionResult> GetOwn(Guid batchId,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? selectedLegalEntityHint,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(out var tenantId, out var actorId)) return Forbid();
        var response = await _sender.Send(new GetOwnDemandHistoryImportBatchQuery(
            tenantId, actorId, batchId, selectedLegalEntityHint ?? Guid.Empty), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{batchId:guid}/review")]
    [HasPermission("demand.history-imports.review")]
    public async Task<IActionResult> GetForReview(Guid batchId,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? selectedLegalEntityHint,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(out var tenantId, out var actorId)) return Forbid();
        var response = await _sender.Send(new GetReviewHistoryImportBatchQuery(
            tenantId, actorId, batchId, selectedLegalEntityHint ?? Guid.Empty,
            User.HasClaim("permission", "demand.history-imports.review")), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{batchId:guid}/review")]
    [HasPermission("demand.history-imports.review")]
    public async Task<IActionResult> Review(Guid batchId,
        [FromBody] ReviewHistoryImportForm form,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? selectedLegalEntityHint,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(out var tenantId, out var actorId)) return Forbid();
        var response = await _sender.Send(new ReviewHistoryImportBatchCommand(
            tenantId, actorId, batchId, selectedLegalEntityHint ?? Guid.Empty,
            User.HasClaim("permission", "demand.history-imports.review"),
            form.Decision, form.Reason, idempotencyKey ?? string.Empty), cancellationToken);
        return CreateActionResultInstance(response);
    }

    private bool TryGetScope(out Guid tenantId, out Guid actorId)
    {
        tenantId = HttpContext.Items["DemandPlanning.TenantId"] is Guid scope ? scope : Guid.Empty;
        actorId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return tenantId != Guid.Empty && Guid.TryParse(claim, out actorId) && actorId != Guid.Empty;
    }
}

public sealed class CreateHistoryImportForm
{
    public string SourceSystem { get; set; } = string.Empty;
    public DateOnly ScopeFrom { get; set; }
    public DateOnly ScopeThrough { get; set; }
    public List<string> WarehouseScope { get; set; } = [];
    public IFormFile? File { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ReviewHistoryImportForm
{
    public Diten.PlanningService.Domain.Features.DemandPlanning.ImportBatchReviewState Decision { get; set; }
    public string Reason { get; set; } = string.Empty;
}

