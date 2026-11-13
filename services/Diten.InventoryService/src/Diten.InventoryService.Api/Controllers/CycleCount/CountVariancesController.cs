using Diten.InventoryService.Api.CycleCounting;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CountVariances;
using Diten.InventoryService.Application.Features.CountVariances.Commands;
using Diten.InventoryService.Application.Features.CountVariances.Queries;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.CycleCounting;

/// <summary>
/// WMS-COUNT-BUNDLE v1 (draft §4.3, DL-3) — the variance decisions, the recount, the correction recovery and the company settings under
/// <c>/api/warehouse</c> (Q-15 = A; pack §3 r4 02/F-5: the base is the controller's, the template names the whole path segment). GET / POST only, with
/// ONE named exception: <c>PUT count-settings</c> (F-1 = A; <c>LedgerImmutabilityArchitectureTests</c> takes out exactly
/// <c>CountVariancesController:count-settings</c>). Every action carries its <c>cycle-count.*</c> key; the scope is the token's tenant + the selected
/// company (another company's or tenant's line / count is 404). No business rule here.
/// </summary>
[Route("api/warehouse")]
[Authorize]
public sealed class CountVariancesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public CountVariancesController(IMediator mediator) => _mediator = mediator;

    // ── Variance Trace (pack §2.4, §8.13) ────────────────────────────────────────────────────────────────

    [HttpGet("count-variances")]
    [HasPermission(CycleCountPermissions.VariancesRead)]
    public async Task<IActionResult> List([FromQuery] string? source, [FromQuery] string? warehouseId, [FromQuery] string? from, [FromQuery] string? to,
        [FromQuery] string[]? status, [FromQuery] string? skuId, [FromQuery] string? lotId, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetCountVarianceListQuery(source, warehouseId, from, to, status ?? [], skuId, lotId, page, pageSize), ct));

    [HttpGet("count-variances/{lineId}")]
    [HasPermission(CycleCountPermissions.VariancesRead)]
    public async Task<IActionResult> Get(string lineId, CancellationToken ct)
        => CycleCountSupport.TryParseId(lineId, out var id) ? CreateActionResultInstance(await _mediator.Send(new GetCountVarianceByIdQuery(id), ct)) : LineNotFound();

    // ── decisions (pack §8.5) ────────────────────────────────────────────────────────────────────────────

    [HttpPost("count-variances/{lineId}/approve")]
    [HasPermission(CycleCountPermissions.VariancesApprove)]
    public async Task<IActionResult> Approve(string lineId, CancellationToken ct)
    {
        if (!CycleCountSupport.TryParseId(lineId, out var id)) return LineNotFound();
        var read = await CycleCountRequestReader.ReadAsync(Request.Body, CycleCountRequestReader.KeyMembers, b => new KeyBody(b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required (the ETag of the line).");
        return WithLineETag(await _mediator.Send(new ApproveCountVarianceCommand(id, key, version, ReadsVariances()), ct));
    }

    [HttpPost("count-variances/{lineId}/reject")]
    [HasPermission(CycleCountPermissions.VariancesReject)]
    public async Task<IActionResult> Reject(string lineId, CancellationToken ct)
    {
        if (!CycleCountSupport.TryParseId(lineId, out var id)) return LineNotFound();
        var read = await CycleCountRequestReader.ReadAsync(Request.Body, CycleCountRequestReader.ReasonMembers,
            b => new ReasonBody(b.String("reason"), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required (the ETag of the line).");
        return WithLineETag(await _mediator.Send(new RejectCountVarianceCommand(id, body.Reason, key, version, ReadsVariances()), ct));
    }

    [HttpPost("cycle-counts/{countId}/recount")]
    [HasPermission(CycleCountPermissions.VariancesRecount)]
    public async Task<IActionResult> Recount(string countId, CancellationToken ct)
    {
        if (!CycleCountSupport.TryParseId(countId, out var id))
            return ContractFailure(StatusCodes.Status404NotFound, CycleCountErrorCodes.NotFound, "Cycle count not found.");
        var read = await CycleCountRequestReader.ReadAsync(Request.Body, CycleCountRequestReader.RecountMembers,
            b => new RecountBody(b.Recount(), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required (the ETag of the count).");
        return WithCountETag(await _mediator.Send(new RequestCountRecountCommand(id, body.Input, key, version), ct));
    }

    // ── correction recovery (pack §8.6, §8.7, F-12) ─────────────────────────────────────────────────────

    [HttpPost("count-variances/{lineId}/retry")]
    [HasPermission(CycleCountPermissions.VariancesPost)]
    public async Task<IActionResult> Retry(string lineId, CancellationToken ct)
    {
        if (!CycleCountSupport.TryParseId(lineId, out var id)) return LineNotFound();
        var read = await CycleCountRequestReader.ReadAsync(Request.Body, CycleCountRequestReader.KeyMembers, b => new KeyBody(b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required (the ETag of the line).");
        return WithLineETag(await _mediator.Send(new RetryCountAdjustmentCommand(id, key, version, ReadsVariances()), ct));
    }

    [HttpPost("count-variances/{lineId}/resolve-integrity")]
    [HasPermission(CycleCountPermissions.VariancesResolve)]
    public async Task<IActionResult> ResolveIntegrity(string lineId, CancellationToken ct)
    {
        if (!CycleCountSupport.TryParseId(lineId, out var id)) return LineNotFound();
        var read = await CycleCountRequestReader.ReadAsync(Request.Body, CycleCountRequestReader.ResolveMembers,
            b => new ResolveBody(b.String("decision"), b.String("reason"), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required (the ETag of the line).");
        return WithLineETag(await _mediator.Send(new ResolveCountAdjustmentIntegrityCommand(id, body.Decision, body.Reason, key, version, ReadsVariances()), ct));
    }

    // ── company settings (pack §4.4; AC-49, AC-51) ──────────────────────────────────────────────────────

    [HttpGet("count-settings")]
    [HasPermission(CycleCountPermissions.SettingsRead)]
    public async Task<IActionResult> Settings(CancellationToken ct) => WithSettingsETag(await _mediator.Send(new GetCycleCountSettingsQuery(), ct));

    /// <summary>The ONE PUT of MOD-0181 (F-1 = A, pack §5): the company's settings record, master-data-like, never stock.</summary>
    [HttpPut("count-settings")]
    [HasPermission(CycleCountPermissions.SettingsConfigure)]
    public async Task<IActionResult> UpdateSettings(CancellationToken ct)
    {
        var read = await CycleCountRequestReader.ReadAsync(Request.Body, CycleCountRequestReader.SettingsMembers,
            b => new SettingsBody(b.Settings(), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required (the ETag of the settings; W/\"0\" while the defaults hold).");
        return WithSettingsETag(await _mediator.Send(new UpdateCycleCountSettingsCommand(body.Input, key, version), ct));
    }

    // ── plumbing ──────────────────────────────────────────────────────────────────────────────────────────

    private sealed record KeyBody(string? Key);

    private sealed record ReasonBody(string? Reason, string? Key);

    private sealed record ResolveBody(string? Decision, string? Reason, string? Key);

    private sealed record RecountBody(CycleCountRecountInput Input, string? Key);

    private sealed record SettingsBody(CycleCountSettingsInput Input, string? Key);

    /// <summary>The blind rule's "variances.read holder" (pack §8.2 rule 5) — a key the signed token carries, never the body.</summary>
    private bool ReadsVariances()
        => User.Claims.Any(c => c.Type == "permission" && string.Equals(c.Value, CycleCountPermissions.VariancesRead, StringComparison.OrdinalIgnoreCase));

    private IActionResult WithLineETag(Response<CycleCountLineDto> response)
    {
        if (response.IsSuccessful && response.Data is not null) Response.Headers.ETag = $"W/\"{response.Data.Version}\"";
        return CreateActionResultInstance(response);
    }

    private IActionResult WithCountETag(Response<CycleCountDto> response)
    {
        if (response.IsSuccessful && response.Data is not null) Response.Headers.ETag = $"W/\"{response.Data.Version}\"";
        return CreateActionResultInstance(response);
    }

    private IActionResult WithSettingsETag(Response<CycleCountSettingsDto> response)
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

    private IActionResult LineNotFound() => ContractFailure(StatusCodes.Status404NotFound, CycleCountErrorCodes.LineNotFound, "Cycle count line not found.");
}
