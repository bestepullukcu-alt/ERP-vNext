using Diten.InventoryService.Api.Quarantine;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine.Commands;
using Diten.InventoryService.Application.Features.Quarantine.Handlers;
using Diten.InventoryService.Application.Features.Quarantine.Queries;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.InventoryService.Api.Controllers.Quarantine;

/// <summary>
/// QUARANTINE-BUNDLE — MOD-0175 (base <c>/api/quarantine</c>, contract <c>quarantine.openapi.yaml</c> 1.0.0). Slice Q1
/// implements: holds (list, get, "Karantinaya Al", "İncelemeye al"), decisions (draft, submit, get, re-apply), blocked stock
/// (list, get, "İncelemeyi tamamla", "Stoğu bloke et"), settings (get / put) and block reasons (list / create / put). Slice
/// Q2 adds the workflow operations (refresh, withdraw, template), Q4 the retest queue, Q5 the lot quality status. The by-source
/// list and the hold PATCH are frozen in the contract and not served yet (Q5 evidence O-Q5-1). No DELETE anywhere;
/// the only PUTs are the two master-data replacements the contract names (settings, block reasons). No business rule here.
/// </summary>
[Route("api/quarantine")]
[Authorize]
public sealed class QuarantineController : CustomBaseController
{
    private readonly IMediator _mediator;

    public QuarantineController(IMediator mediator) => _mediator = mediator;

    // ── holds ────────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("holds")]
    [HasPermission(QuarantinePermissions.HoldsRead)]
    public async Task<IActionResult> ListHolds(
        [FromQuery] string? q, [FromQuery] string? warehouseId, [FromQuery] string? stockStatus, [FromQuery] string? source, [FromQuery] string? status,
        [FromQuery] string? reviewState, [FromQuery] string? lateOnly, [FromQuery] string? lotId, [FromQuery] string? page, [FromQuery] string? pageSize,
        CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(
            new ListHoldsQuery(q, warehouseId, stockStatus, source, status, reviewState, lateOnly, lotId, page, pageSize), ct));

    [HttpGet("holds/{holdId}")]
    [HasPermission(QuarantinePermissions.HoldsRead)]
    public async Task<IActionResult> GetHold(string holdId, CancellationToken ct)
        => await CompanyChecked(holdId, WithETag(await _mediator.Send(new GetHoldQuery(holdId), ct), d => d.Version), ct);

    [HttpPost("holds")]
    [HasPermission(QuarantinePermissions.HoldsCreate)]
    public async Task<IActionResult> QuarantineStock(CancellationToken ct)
    {
        var read = await QuarantineRequestReader.ReadAsync(Request.Body, QuarantineRequestReader.QuarantineMembers, b => new
        {
            Mode = b.String("mode"),
            LotId = b.String("lotId"),
            Buckets = QuarantineRequestReader.Buckets(b),
            Reason = b.String("reason"),
            SignatureId = b.String("signatureId"),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return CreateActionResultInstance(await _mediator.Send(
            new QuarantineStockCommand(body.Mode ?? "", body.LotId, body.Buckets, body.Reason, body.SignatureId, key), ct));
    }

    [HttpPost("holds/{holdId}/review")]
    [HasPermission(QuarantinePermissions.HoldsReview)]
    public async Task<IActionResult> StartReview(string holdId, CancellationToken ct)
    {
        if (!QuarantineSupport.TryParseId(holdId, out var id)) return NotFoundContract("QUARANTINE_HOLD_NOT_FOUND", "Hold not found.");
        var read = await QuarantineRequestReader.ReadAsync(Request.Body, QuarantineRequestReader.ReviewMembers,
            b => new { Note = b.String("note"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(holdId, WithETag(await _mediator.Send(new StartReviewCommand(id, body.Note, key, version), ct), h => h.Version), ct);
    }

    // ── decisions ────────────────────────────────────────────────────────────────────────────────────────

    [HttpPost("decisions")]
    public async Task<IActionResult> CreateDecision(CancellationToken ct)
    {
        // §4.6: the key follows the kind (decisions.create / blocked.dispose / blocked.unblock) — checked here, like updateLot.
        if (!DecisionKeys.Any(Holds)) return Forbidden();
        var read = await QuarantineRequestReader.ReadAsync(Request.Body, QuarantineRequestReader.DecisionMembers, b => new
        {
            Kind = b.Enum<DecisionKind>("kind"),
            TargetId = b.Uuid("targetId"),
            Lines = QuarantineRequestReader.Lines(b),
            BlockReasonCode = b.String("blockReasonCode"),
            Reason = b.String("reason"),
            CoaChecked = b.Bool("coaChecked") ?? false,
            NewRetestDate = b.Date("newRetestDate"),
            Disposition = QuarantineRequestReader.Disposition(b),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);

        // §4.6: the key follows the kind (decisions.create / blocked.dispose / blocked.unblock).
        var kindKey = body.Kind switch
        {
            DecisionKind.Disposition => QuarantinePermissions.BlockedDispose,
            DecisionKind.Unblock => QuarantinePermissions.BlockedUnblock,
            _ => QuarantinePermissions.DecisionsCreate
        };
        if (!Holds(kindKey)) return Forbidden();

        return await CompanyChecked(body.TargetId.ToString("D"), CreateActionResultInstance(await _mediator.Send(new CreateDecisionCommand(body.Kind,
            body.TargetId, body.Lines, body.BlockReasonCode, body.Reason, body.CoaChecked, body.NewRetestDate, body.Disposition, key), ct)), ct);
    }

    [HttpPost("decisions/{decisionId}/submit")]
    public async Task<IActionResult> SubmitDecision(string decisionId, CancellationToken ct)
    {
        // §4.7: one of the three decision keys; the handler admits only the decision's own decider.
        if (!DecisionKeys.Any(Holds)) return Forbidden();
        if (!QuarantineSupport.TryParseId(decisionId, out var id)) return NotFoundContract("QUARANTINE_DECISION_NOT_FOUND", "Decision not found.");
        var read = await QuarantineRequestReader.ReadAsync(Request.Body, QuarantineRequestReader.SubmitMembers,
            b => new { SignatureId = b.String("signatureId"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return await CompanyChecked(decisionId, CreateActionResultInstance(await _mediator.Send(new SubmitDecisionCommand(id, body.SignatureId, key), ct)), ct);
    }

    [HttpGet("decisions/{decisionId}")]
    [HasPermission(QuarantinePermissions.HoldsRead)]
    public async Task<IActionResult> GetDecision(string decisionId, CancellationToken ct)
        => await CompanyChecked(decisionId, CreateActionResultInstance(await _mediator.Send(new GetDecisionQuery(decisionId), ct)), ct);

    [HttpPost("decisions/{decisionId}/refresh")]
    [HasPermission(QuarantinePermissions.HoldsRead)]
    public async Task<IActionResult> RefreshDecision(string decisionId, CancellationToken ct)
        => QuarantineSupport.TryParseId(decisionId, out var id)
            ? await CompanyChecked(decisionId, CreateActionResultInstance(await _mediator.Send(new RefreshDecisionCommand(id), ct)), ct)
            : NotFoundContract("QUARANTINE_DECISION_NOT_FOUND", "Decision not found.");

    [HttpPost("decisions/{decisionId}/withdraw")]
    [HasPermission(QuarantinePermissions.DecisionsWithdraw)]
    public async Task<IActionResult> WithdrawDecision(string decisionId, CancellationToken ct)
    {
        if (!QuarantineSupport.TryParseId(decisionId, out var id)) return NotFoundContract("QUARANTINE_DECISION_NOT_FOUND", "Decision not found.");
        var read = await QuarantineRequestReader.ReadAsync(Request.Body, QuarantineRequestReader.ReviewCompleteMembers,
            b => new { Reason = b.String("reason"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return await CompanyChecked(decisionId, CreateActionResultInstance(await _mediator.Send(new WithdrawDecisionCommand(id, body.Reason, key), ct)), ct);
    }

    [HttpPost("decisions/{decisionId}/apply")]
    [HasPermission(QuarantinePermissions.DecisionsApply)]
    public async Task<IActionResult> ReapplyDecision(string decisionId, CancellationToken ct)
        => QuarantineSupport.TryParseId(decisionId, out var id)
            ? await CompanyChecked(decisionId, CreateActionResultInstance(await _mediator.Send(new ReapplyDecisionCommand(id), ct)), ct)
            : NotFoundContract("QUARANTINE_DECISION_NOT_FOUND", "Decision not found.");

    // ── blocked stock ───────────────────────────────────────────────────────────────────────────────────

    [HttpGet("blocked")]
    [HasPermission(QuarantinePermissions.HoldsRead)]
    public async Task<IActionResult> ListBlocked(
        [FromQuery] string? q, [FromQuery] string? blockReasonCode, [FromQuery] string? pendingOnly, [FromQuery] string? origin, [FromQuery] string? status,
        [FromQuery] string? warehouseId, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(
            new ListBlockedQuery(q, blockReasonCode, pendingOnly, origin, status, warehouseId, page, pageSize, Language()), ct));

    [HttpGet("blocked/{blockedId}")]
    [HasPermission(QuarantinePermissions.HoldsRead)]
    public async Task<IActionResult> GetBlocked(string blockedId, CancellationToken ct)
        => await CompanyChecked(blockedId, WithETag(await _mediator.Send(new GetBlockedQuery(blockedId, Language()), ct), d => d.Version), ct);

    [HttpPost("blocked/{blockedId}/review-complete")]
    [HasPermission(QuarantinePermissions.HoldsReview)]
    public async Task<IActionResult> CompleteBlockedReview(string blockedId, CancellationToken ct)
    {
        if (!QuarantineSupport.TryParseId(blockedId, out var id)) return NotFoundContract("QUARANTINE_BLOCKED_NOT_FOUND", "Blocked-stock record not found.");
        var read = await QuarantineRequestReader.ReadAsync(Request.Body, QuarantineRequestReader.ReviewCompleteMembers,
            b => new { Reason = b.String("reason"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(blockedId, WithETag(await _mediator.Send(new CompleteBlockedReviewCommand(id, body.Reason, key, version), ct), b => b.Version), ct);
    }

    [HttpPost("blocked")]
    [HasPermission(QuarantinePermissions.BlockedBlock)]
    public async Task<IActionResult> BlockStock(CancellationToken ct)
    {
        var read = await QuarantineRequestReader.ReadAsync(Request.Body, QuarantineRequestReader.BlockMembers, b => new
        {
            Buckets = QuarantineRequestReader.Buckets(b),
            BlockReasonCode = b.String("blockReasonCode"),
            Note = b.String("note"),
            Reason = b.String("reason"),
            SignatureId = b.String("signatureId"),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (body.Buckets is null) return Invalid("buckets is required.");
        return CreateActionResultInstance(await _mediator.Send(
            new BlockStockCommand(body.Buckets, body.BlockReasonCode, body.Note, body.Reason, body.SignatureId, key), ct));
    }

    // ── retest queue (Q4, KQ-H) ─────────────────────────────────────────────────────────────────────────

    [HttpGet("retest")]
    [HasPermission(QuarantinePermissions.HoldsRead)]
    public async Task<IActionResult> ListRetest([FromQuery] string? horizonDays, [FromQuery] string? state, [FromQuery] string? warehouseId, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new ListRetestQuery(horizonDays, state, warehouseId), ct));

    // ── lot quality status (Q5, O-Q4-2 — the consumers' read and the MOD-0174 Lot page badge) ─────────────────────

    [HttpGet("lots/{lotId}/quality-status")]
    [HasPermission(QuarantinePermissions.HoldsRead)]
    public async Task<IActionResult> GetLotQualityStatus(string lotId, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetLotQualityStatusQuery(lotId, Language()), ct));

    // ── settings + block reasons ──────────────────────────────────────────────────────────────────────

    [HttpGet("settings")]
    [HasPermission(QuarantinePermissions.HoldsRead)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
        => WithETag(await _mediator.Send(new GetSettingsQuery(), ct), s => s.Version);

    [HttpPut("settings")]
    [HasPermission(QuarantinePermissions.SettingsManage)]
    public async Task<IActionResult> SaveSettings(CancellationToken ct)
    {
        var read = await QuarantineRequestReader.ReadAsync(Request.Body, QuarantineRequestReader.SettingsMembers, b => new SaveSettingsCommand(
            b.Int("waitTargetDays"), b.Bool("fourEyes"), QuarantineRequestReader.StringList(b, "approverCandidates"), b.StringMap("sampleLocations"),
            b.Bool("lotReleaseRequiresApproval"), b.Int("reconcileAfterSeconds"), 0), ct);
        if (read.Value is not { } command) return Invalid(read.Error);
        if (IfMatch() is not { } version) return Invalid("If-Match is required (W/\"0\" before the first save).");
        return WithETag(await _mediator.Send(command with { ExpectedVersion = version }, ct), s => s.Version);
    }

    [HttpPost("settings/workflow-template")]
    [HasPermission(QuarantinePermissions.SettingsManage)]
    public async Task<IActionResult> PrepareWorkflowTemplate(CancellationToken ct)
    {
        var read = await QuarantineRequestReader.ReadAsync(Request.Body, QuarantineRequestReader.KeyMembers, b => new { Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return CreateActionResultInstance(await _mediator.Send(new PrepareWorkflowTemplateCommand(key), ct));
    }

    [HttpGet("block-reasons")]
    [HasPermission(QuarantinePermissions.HoldsRead)]
    public async Task<IActionResult> ListBlockReasons([FromQuery] string? activeOnly, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new ListBlockReasonsQuery(activeOnly), ct));

    [HttpPost("block-reasons")]
    [HasPermission(QuarantinePermissions.SettingsManage)]
    public async Task<IActionResult> CreateBlockReason(CancellationToken ct)
    {
        var read = await QuarantineRequestReader.ReadAsync(Request.Body, QuarantineRequestReader.BlockReasonMembers,
            b => new CreateBlockReasonCommand(b.String("code"), b.StringMap("names"), b.Bool("isActive") ?? true), ct);
        return read.Value is { } command ? CreateActionResultInstance(await _mediator.Send(command, ct)) : Invalid(read.Error);
    }

    [HttpPut("block-reasons/{code}")]
    [HasPermission(QuarantinePermissions.SettingsManage)]
    public async Task<IActionResult> UpdateBlockReason(string code, CancellationToken ct)
    {
        var read = await QuarantineRequestReader.ReadAsync(Request.Body, QuarantineRequestReader.BlockReasonMembers, b =>
        {
            if (b.String("code") is { } bodyCode && bodyCode != code) throw new QuarantineRequestReader.FormatException("code cannot change.");
            return new UpdateBlockReasonCommand(code, b.StringMap("names"), b.Bool("isActive"));
        }, ct);
        return read.Value is { } command ? CreateActionResultInstance(await _mediator.Send(command, ct)) : Invalid(read.Error);
    }

    // ── plumbing ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// V23 / pack §13 #21: a record id of ANOTHER company of the caller's tenant answers 403 CROSS_LE_FORBIDDEN, not 404 (another
    /// tenant's stays 404). The handlers read scoped to the company; only their 404 is re-checked here.
    /// </summary>
    private async Task<IActionResult> CompanyChecked(string id, IActionResult result, CancellationToken ct)
    {
        if (result is ObjectResult { StatusCode: StatusCodes.Status404NotFound } && QuarantineSupport.TryParseId(id, out var guid)
            && await HttpContext.RequestServices.GetRequiredService<Diten.InventoryService.Domain.Quarantine.Repositories.IQuarantineRepository>()
                .ExistsInAnotherCompanyAsync(guid, ct))
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
        => QuarantineSupport.KeyError(Request.Headers[InventoryContract.IdempotencyKeyHeader].ToString(), body, out key);

    private int? IfMatch()
    {
        var header = Request.Headers[InventoryContract.IfMatchHeader].ToString();
        return string.IsNullOrWhiteSpace(header) ? null : QuarantineSupport.ExpectedVersion(header);
    }

    private static readonly string[] DecisionKeys =
        [QuarantinePermissions.DecisionsCreate, QuarantinePermissions.BlockedDispose, QuarantinePermissions.BlockedUnblock];

    private bool Holds(string key) => User.Claims.Any(c => c.Type == "permission" && c.Value == key);

    private string Language()
    {
        var header = Request.Headers.AcceptLanguage.ToString();
        var lang = header.Length >= 2 ? header[..2].ToLowerInvariant() : "en";
        return Application.Features.Quarantine.QuarantineContract.Languages.Contains(lang) ? lang : "en";
    }

    private IActionResult Invalid(string? message) => ContractFailure(StatusCodes.Status400BadRequest, InventoryErrorCodes.InvalidRequest, message ?? "The request is malformed.");

    private IActionResult Forbidden() => ContractFailure(StatusCodes.Status403Forbidden, null, "The caller lacks the permission required for this operation.");

    private IActionResult NotFoundContract(string code, string message) => ContractFailure(StatusCodes.Status404NotFound, code, message);
}
