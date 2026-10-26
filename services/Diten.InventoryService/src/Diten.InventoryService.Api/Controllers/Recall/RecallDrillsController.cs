using Diten.InventoryService.Api.Recall;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall.Drills;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.Recall;

/// <summary>
/// RECALL-BUNDLE 1.4.0 (WP-0177-R5, pack §4.11 / §4.12 / §8.10, KR-G) — the drills: plans (list / create / update), runs (list / get / start /
/// step / prepare result / sign result / cancel). Keys <c>recall.drills.read</c> / <c>.manage</c> / <c>.run</c>. No body accepts a time member
/// (the reader refuses unknown members — the server clock stamps start and end). No business rule here.
/// </summary>
[Route("api/recall")]
[Authorize]
public sealed class RecallDrillsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public RecallDrillsController(IMediator mediator) => _mediator = mediator;

    // ── plans ────────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("drill-plans")]
    [HasPermission(RecallPermissions.DrillsRead)]
    public async Task<IActionResult> ListDrillPlans(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new ListRecallDrillPlansQuery(), ct));

    [HttpPost("drill-plans")]
    [HasPermission(RecallPermissions.DrillsManage)]
    public async Task<IActionResult> CreateDrillPlan(CancellationToken ct)
    {
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.DrillPlanMembers, PlanBody, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return WithETag(await _mediator.Send(new CreateRecallDrillPlanCommand(body.Input, key), ct), p => p.Version);
    }

    [HttpPut("drill-plans/{planId}")]
    [HasPermission(RecallPermissions.DrillsManage)]
    public async Task<IActionResult> UpdateDrillPlan(string planId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(planId, out var id)) return DrillNotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.DrillPlanMembers, PlanBody, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(id, WithETag(await _mediator.Send(new UpdateRecallDrillPlanCommand(id, body.Input, key, version), ct), p => p.Version), ct);
    }

    private static PlanRead PlanBody(RecallRequestReader.Body b) => new(
        new RecallDrillPlanInput(b.String("name"), b.Enum<RecallDrillFrequency>("frequency"), b.OptionalInt("customDays"), b.Enum<RecallDrillScopeKind>("scopeKind"),
            b.Date("nextDueOn"), b.Uuid("ownerUserId"), b.OptionalBool("active")),
        b.String("idempotencyKey"));

    private sealed record PlanRead(RecallDrillPlanInput Input, string? Key);

    // ── runs ─────────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("drill-runs")]
    [HasPermission(RecallPermissions.DrillsRead)]
    public async Task<IActionResult> ListDrillRuns([FromQuery] string? status, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? page,
        [FromQuery] string? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new ListRecallDrillRunsQuery(status, from, to, page, pageSize), ct));

    [HttpGet("drill-runs/{runId}")]
    [HasPermission(RecallPermissions.DrillsRead)]
    public async Task<IActionResult> GetDrillRun(string runId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(runId, out var id)) return DrillNotFound();
        return await CompanyChecked(id, WithETag(await _mediator.Send(new GetRecallDrillRunQuery(id), ct), r => r.Version), ct);
    }

    [HttpPost("drill-runs")]
    [HasPermission(RecallPermissions.DrillsRun)]
    public async Task<IActionResult> StartDrill(CancellationToken ct)
    {
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.StartDrillMembers, b => new
        {
            PlanId = b.Uuid("planId"),
            Mode = b.Enum<RecallDrillSelectionMode>("selectionMode"),
            LotId = b.OptionalUuid("lotId"),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return await CompanyChecked(body.PlanId, WithETag(await _mediator.Send(new StartRecallDrillCommand(body.PlanId, body.Mode, body.LotId, key), ct), r => r.Version), ct);
    }

    [HttpPost("drill-runs/{runId}/steps/{step}")]
    [HasPermission(RecallPermissions.DrillsRun)]
    public async Task<IActionResult> CompleteDrillStep(string runId, string step, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(runId, out var id) || !RecallWire.TryParseName<RecallDrillStep>(step, out var drillStep)) return DrillNotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.KeyMembers, b => new KeyRead(b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(id, WithETag(await _mediator.Send(new CompleteRecallDrillStepCommand(id, drillStep, key, version), ct), r => r.Version), ct);
    }

    [HttpPost("drill-runs/{runId}/pending-result")]
    [HasPermission(RecallPermissions.DrillsRun)]
    public async Task<IActionResult> PrepareDrillResult(string runId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(runId, out var id)) return DrillNotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.DrillNoteMembers,
            b => new { Note = b.String("resultNote"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(id, WithETag(await _mediator.Send(new PrepareRecallDrillResultCommand(id, body.Note, key, version), ct), p => p.Version), ct);
    }

    [HttpPost("drill-runs/{runId}/result")]
    [HasPermission(RecallPermissions.DrillsRun)]
    public async Task<IActionResult> SignDrillResult(string runId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(runId, out var id)) return DrillNotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.DrillResultMembers,
            b => new { Note = b.String("resultNote"), SignatureId = b.String("signatureId"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(id, WithETag(await _mediator.Send(new SignRecallDrillResultCommand(id, body.Note, body.SignatureId, key, version), ct), r => r.Version), ct);
    }

    [HttpPost("drill-runs/{runId}/cancel")]
    [HasPermission(RecallPermissions.DrillsRun)]
    public async Task<IActionResult> CancelDrill(string runId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(runId, out var id)) return DrillNotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.ReasonMembers,
            b => new { Reason = b.String("reason"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(id, WithETag(await _mediator.Send(new CancelRecallDrillCommand(id, body.Reason, key, version), ct), r => r.Version), ct);
    }

    private sealed record KeyRead(string? Key);

    // ── plumbing (the RecallController pattern) ───────────────────────────────────────────────────────────

    /// <summary>A plan / run id of ANOTHER company of the caller's tenant answers 403 CROSS_LE_FORBIDDEN; another tenant's stays 404.</summary>
    private async Task<IActionResult> CompanyChecked(Guid id, IActionResult result, CancellationToken ct)
    {
        if (result is ObjectResult { StatusCode: StatusCodes.Status404NotFound }
            && await HttpContext.RequestServices.GetRequiredService<IRecallDrillRepository>().ExistsInAnotherCompanyAsync(id, ct))
        {
            return ContractFailure(StatusCodes.Status403Forbidden, InventoryErrorCodes.CrossLeForbidden, "The record belongs to another company.");
        }

        return result;
    }

    private IActionResult WithETag<T>(Response<T> response, Func<T, int> version)
    {
        if (response.IsSuccessful && response.Data is not null)
        {
            Response.Headers.ETag = $"W/\"{version(response.Data)}\"";
        }

        return CreateActionResultInstance(response);
    }

    private string? KeyError(string? body, out string key)
        => RecallSupport.KeyError(Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(), body, out key);

    private int? IfMatch()
    {
        var header = Request.Headers[InventoryContract.IfMatchHeader].ToString();
        return string.IsNullOrWhiteSpace(header) ? null : RecallSupport.ExpectedVersion(header);
    }

    private IActionResult Invalid(string? message)
        => ContractFailure(StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest, message ?? "The request is malformed.");

    private IActionResult DrillNotFound() => ContractFailure(StatusCodes.Status404NotFound, RecallErrorCodes.DrillNotFound, "Drill plan / run not found.");
}
