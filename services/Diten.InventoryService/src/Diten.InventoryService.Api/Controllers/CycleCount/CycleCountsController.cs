using Diten.InventoryService.Api.CycleCounting;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts;
using Diten.InventoryService.Application.Features.CycleCounts.Commands;
using Diten.InventoryService.Application.Features.CycleCounts.Queries;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.CycleCounting;

/// <summary>
/// WMS-COUNT-BUNDLE v1 (draft §4.1) — MOD-0181 counts under <c>/api/warehouse</c> (Q-15 = A; pack §3 r4 02/F-5: the base is the controller's,
/// the template names the whole path segment). GET / POST only (F-1 = A: update, cancel and bulk delete are POST commands; no PUT, no DELETE —
/// AC-51). Every action carries its <c>cycle-count.counts.*</c> key; the scope is the token's tenant + the selected company (another company's or
/// tenant's count is 404). No business rule here: the body is parsed, the key / If-Match headers read, and the handler decides.
/// </summary>
[Route("api/warehouse")]
[Authorize]
public sealed class CycleCountsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public CycleCountsController(IMediator mediator) => _mediator = mediator;

    // ── reads ────────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("cycle-counts")]
    [HasPermission(CycleCountPermissions.CountsRead)]
    public async Task<IActionResult> List([FromQuery] string[]? status, [FromQuery] string? warehouseId, [FromQuery] string? q, [FromQuery] string? page,
        [FromQuery] string? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetCycleCountListQuery(status ?? [], warehouseId, q, page, pageSize), ct));

    /// <summary>DL-4 (pack §8.13, AC-40): the dashboard's KPI cards, counted at read time.</summary>
    [HttpGet("cycle-counts/dashboard")]
    [HasPermission(CycleCountPermissions.CountsRead)]
    public async Task<IActionResult> Dashboard([FromQuery] string? warehouseId, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetCycleCountDashboardQuery(warehouseId), ct));

    [HttpGet("cycle-counts/{countId}")]
    [HasPermission(CycleCountPermissions.CountsRead)]
    public async Task<IActionResult> Get(string countId, CancellationToken ct)
        => CycleCountSupport.TryParseId(countId, out var id) ? WithETag(await _mediator.Send(new GetCycleCountByIdQuery(id), ct)) : NotFound();

    [HttpGet("cycle-counts/{countId}/lines")]
    [HasPermission(CycleCountPermissions.CountsRead)]
    public async Task<IActionResult> Lines(string countId, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => CycleCountSupport.TryParseId(countId, out var id)
            ? CreateActionResultInstance(await _mediator.Send(new GetCycleCountLineListQuery(id, page, pageSize, Holds(CycleCountPermissions.VariancesRead)), ct))
            : NotFound();

    [HttpGet("cycle-counts/{countId}/events")]
    [HasPermission(CycleCountPermissions.CountsRead)]
    public async Task<IActionResult> Events(string countId, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => CycleCountSupport.TryParseId(countId, out var id)
            ? CreateActionResultInstance(await _mediator.Send(new GetCycleCountEventListQuery(id, page, pageSize, Holds(CycleCountPermissions.VariancesRead)), ct))
            : NotFound();

    // ── writes ───────────────────────────────────────────────────────────────────────────────────────────

    [HttpPost("cycle-counts")]
    [HasPermission(CycleCountPermissions.CountsCreate)]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var read = await CycleCountRequestReader.ReadAsync(Request.Body, CycleCountRequestReader.FormMembers,
            b => new FormBody(b.Form(), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return WithETag(await _mediator.Send(new CreateCycleCountCommand(body.Form, key), ct));
    }

    [HttpPost("cycle-counts/{countId}/update")]
    [HasPermission(CycleCountPermissions.CountsUpdate)]
    public async Task<IActionResult> Update(string countId, CancellationToken ct)
    {
        if (!CycleCountSupport.TryParseId(countId, out var id)) return NotFound();
        var read = await CycleCountRequestReader.ReadAsync(Request.Body, CycleCountRequestReader.FormMembers,
            b => new FormBody(b.Form(), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new UpdateCycleCountCommand(id, body.Form, key, version), ct));
    }

    [HttpPost("cycle-counts/bulk-delete")]
    [HasPermission(CycleCountPermissions.CountsBulkDelete)]
    public async Task<IActionResult> BulkDelete(CancellationToken ct)
    {
        var read = await CycleCountRequestReader.ReadAsync(Request.Body, CycleCountRequestReader.BulkDeleteMembers,
            b => new BulkBody(b.BulkItems(), b.String("reason"), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return CreateActionResultInstance(await _mediator.Send(new BulkDeleteCycleCountCommand(body.Items, body.Reason, key), ct));
    }

    [HttpPost("cycle-counts/{countId}/release")]
    [HasPermission(CycleCountPermissions.CountsUpdate)]
    public async Task<IActionResult> Release(string countId, CancellationToken ct)
    {
        if (!CycleCountSupport.TryParseId(countId, out var id)) return NotFound();
        var read = await CycleCountRequestReader.ReadAsync(Request.Body, CycleCountRequestReader.KeyMembers, b => new KeyBody(b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new ReleaseCycleCountCommand(id, key, version), ct));
    }

    [HttpPost("cycle-counts/{countId}/cancel")]
    [HasPermission(CycleCountPermissions.CountsCancel)]
    public async Task<IActionResult> Cancel(string countId, CancellationToken ct)
    {
        if (!CycleCountSupport.TryParseId(countId, out var id)) return NotFound();
        var read = await CycleCountRequestReader.ReadAsync(Request.Body, CycleCountRequestReader.ReasonMembers,
            b => new ReasonBody(b.String("reason"), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new CancelCycleCountCommand(id, body.Reason, key, version), ct));
    }

    // ── DL-2: entry, found stock, submit (draft §4.2; pack §8.3, §8.4) ─────────────────────────────────────

    [HttpPost("cycle-counts/{countId}/lines")]
    [HasPermission(CycleCountPermissions.CountsRecord)]
    public async Task<IActionResult> AddLine(string countId, CancellationToken ct)
    {
        if (!CycleCountSupport.TryParseId(countId, out var id)) return NotFound();
        var read = await CycleCountRequestReader.ReadAsync(Request.Body, CycleCountRequestReader.FoundLineMembers,
            b => new FoundLineBody(b.FoundLine(), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return WithLineETag(await _mediator.Send(new AddCountLineCommand(id, body.Line, key), ct));
    }

    [HttpPost("cycle-counts/{countId}/lines/{lineId}/count")]
    [HasPermission(CycleCountPermissions.CountsRecord)]
    public async Task<IActionResult> RecordLine(string countId, string lineId, CancellationToken ct)
    {
        if (!CycleCountSupport.TryParseId(countId, out var id)) return NotFound();
        if (!CycleCountSupport.TryParseId(lineId, out var line))
            return ContractFailure(StatusCodes.Status404NotFound, CycleCountErrorCodes.LineNotFound, "Cycle count line not found.");
        var read = await CycleCountRequestReader.ReadAsync(Request.Body, CycleCountRequestReader.EntryMembers,
            b => new EntryBody(b.Entry(), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required (the ETag of the line).");
        return WithLineETag(await _mediator.Send(new RecordCountLineCommand(id, line, body.Entry, key, version), ct));
    }

    [HttpPost("cycle-counts/{countId}/submit")]
    [HasPermission(CycleCountPermissions.CountsRecord)]
    public async Task<IActionResult> Submit(string countId, CancellationToken ct)
    {
        if (!CycleCountSupport.TryParseId(countId, out var id)) return NotFound();
        var read = await CycleCountRequestReader.ReadAsync(Request.Body, CycleCountRequestReader.KeyMembers, b => new KeyBody(b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return WithETag(await _mediator.Send(new SubmitCycleCountCommand(id, key, version), ct));
    }

    // ── plumbing ──────────────────────────────────────────────────────────────────────────────────────────

    private sealed record FormBody(CycleCountFormInput Form, string? Key);

    private sealed record BulkBody(IReadOnlyList<CycleCountBulkItemInput> Items, string? Reason, string? Key);

    private sealed record KeyBody(string? Key);

    private sealed record ReasonBody(string? Reason, string? Key);

    private sealed record EntryBody(CycleCountEntryInput Entry, string? Key);

    private sealed record FoundLineBody(CycleCountFoundLineInput Line, string? Key);

    /// <summary>A key the signed token carries (the blind rule's "variances.read holder", pack §8.2 rule 5) — never read from the body.</summary>
    private bool Holds(string permission)
        => User.Claims.Any(c => c.Type == "permission" && string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));

    private IActionResult WithETag(Response<CycleCountDto> response)
    {
        if (response.IsSuccessful && response.Data is not null) Response.Headers.ETag = $"W/\"{response.Data.Version}\"";
        return CreateActionResultInstance(response);
    }

    private IActionResult WithLineETag(Response<CycleCountLineDto> response)
    {
        if (response.IsSuccessful && response.Data is not null) Response.Headers.ETag = $"W/\"{response.Data.Version}\"";
        return CreateActionResultInstance(response);
    }

    private string? KeyError(string? body, out string key)
        => CycleCountSupport.KeyError(Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(), body, out key);

    private int? IfMatch()
    {
        var header = Request.Headers[InventoryContract.IfMatchHeader].ToString();
        return string.IsNullOrWhiteSpace(header) ? null : CycleCountSupport.ExpectedVersion(header);
    }

    private IActionResult Invalid(string? message)
        => ContractFailure(StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest, message ?? "The request is malformed.");

    private new IActionResult NotFound() => ContractFailure(StatusCodes.Status404NotFound, CycleCountErrorCodes.NotFound, "Cycle count not found.");
}
