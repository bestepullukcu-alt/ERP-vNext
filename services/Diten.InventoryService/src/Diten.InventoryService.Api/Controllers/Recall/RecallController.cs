using Diten.InventoryService.Api.Recall;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall;
using Diten.InventoryService.Application.Features.Recall.Commands;
using Diten.InventoryService.Application.Features.Recall.Handlers;
using Diten.InventoryService.Application.Features.Recall.Queries;
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
/// RECALL-BUNDLE v1.0.0 — MOD-0177 (base <c>/api/recall</c>, contract <c>recall.openapi.yaml</c>). Slice R1 serves the settings (get / history /
/// put), the case (list, get, scope, timeline, open, add / mark scope items, submit, return, prepare, approve, cancel, effectiveness start /
/// complete, close), its records (notification, consumption, segregation) and the lot recall status. Every other operation of the contract
/// (dashboard, trace, quarantine requests, returns, drills, evidence, signable subjects) is R2–R7 and has no route yet. No DELETE; the only
/// PUT is the settings (master data, full replacement). No business rule here: the body is parsed, the key / If-Match headers read, and the
/// handler decides.
/// </summary>
[Route("api/recall")]
[Authorize]
public sealed class RecallController : CustomBaseController
{
    private readonly IMediator _mediator;

    public RecallController(IMediator mediator) => _mediator = mediator;

    // ── settings ─────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("settings")]
    [HasPermission(RecallPermissions.SettingsRead)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
        => WithETag(await _mediator.Send(new GetRecallSettingsQuery(), ct), s => s.Version);

    [HttpGet("settings/history")]
    [HasPermission(RecallPermissions.SettingsRead)]
    public async Task<IActionResult> GetSettingsHistory([FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetRecallSettingsHistoryQuery(page, pageSize), ct));

    [HttpPut("settings")]
    [HasPermission(RecallPermissions.SettingsManage)]
    public async Task<IActionResult> UpdateSettings(CancellationToken ct)
    {
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.SettingsMembers, b => new
        {
            Settings = new RecallSettingsInput(b.Int("drillTargetHours"), b.Int("drillFrequencyWarningDays"), b.Targets("effectivenessTargetPercent"),
                b.Int("traceMaxDepth"), b.Int("traceMaxNodes")),
            Reason = b.String("reason"),
            SignatureId = b.String("signatureId"),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required (\"0\" before the first save).");
        return WithETag(await _mediator.Send(new UpdateRecallSettingsCommand(body.Settings, body.Reason, body.SignatureId, key, version), ct), s => s.Version);
    }

    /// <summary>R4 prepareSettingsChange (RECALL 1.3.0, CT-R16) — the proposal to sign; the MOD-0022 subject recall.settings-change.</summary>
    [HttpPost("settings/pending-change")]
    [HasPermission(RecallPermissions.SettingsManage)]
    public async Task<IActionResult> PrepareSettingsChange(CancellationToken ct)
    {
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.PrepareSettingsMembers, b => new
        {
            Settings = new RecallSettingsInput(b.Int("drillTargetHours"), b.Int("drillFrequencyWarningDays"), b.Targets("effectivenessTargetPercent"),
                b.Int("traceMaxDepth"), b.Int("traceMaxNodes")),
            Reason = b.String("reason"),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required (\"0\" before the first save).");
        return CreateActionResultInstance(await _mediator.Send(new PrepareRecallSettingsChangeCommand(body.Settings, body.Reason, key, version), ct));
    }

    // ── R6: Hazırlık Paneli (pack §8.9) ─────────────────────────────────────────────────────────────────────

    [HttpGet("dashboard")]
    [HasPermission(RecallPermissions.DashboardRead)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetRecallDashboardQuery(), ct));

    // ── cases (reads) ────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("cases")]
    [HasPermission(RecallPermissions.CasesRead)]
    public async Task<IActionResult> ListCases(
        [FromQuery] string? type, [FromQuery] string[]? status, [FromQuery(Name = "class")] string? recallClass, [FromQuery] string? skuId,
        [FromQuery] string? openedFrom, [FromQuery] string? q, [FromQuery] string? sort, [FromQuery] string? page, [FromQuery] string? pageSize,
        [FromQuery] string[]? drillStatus, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(
            new ListRecallCasesQuery(type, status ?? [], recallClass, skuId, openedFrom, q, sort, page, pageSize, drillStatus ?? []), ct));

    [HttpGet("cases/{caseId}")]
    [HasPermission(RecallPermissions.CasesRead)]
    public async Task<IActionResult> GetCase(string caseId, CancellationToken ct)
        => await CompanyChecked(caseId, WithETag(await _mediator.Send(new GetRecallCaseQuery(caseId), ct), d => d.Version), ct);

    [HttpGet("cases/{caseId}/scope")]
    [HasPermission(RecallPermissions.CasesRead)]
    public async Task<IActionResult> GetCaseScope(string caseId, CancellationToken ct)
        => await CompanyChecked(caseId, WithETag(await _mediator.Send(new GetRecallCaseScopeQuery(caseId), ct), s => s.Version), ct);

    [HttpGet("cases/{caseId}/timeline")]
    [HasPermission(RecallPermissions.CasesRead)]
    public async Task<IActionResult> GetCaseTimeline(string caseId, [FromQuery] string? page, [FromQuery] string? pageSize, CancellationToken ct)
        => await CompanyChecked(caseId, CreateActionResultInstance(await _mediator.Send(new GetRecallCaseTimelineQuery(caseId, page, pageSize), ct)), ct);

    [HttpGet("lots/{lotId}/status")]
    [HasPermission(RecallPermissions.LotsStatusRead)]
    public async Task<IActionResult> GetLotRecallStatus(string lotId, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetLotRecallStatusQuery(lotId), ct));

    // ── trace (R2, pack §8.2) ────────────────────────────────────────────────────────────────────────────

    [HttpGet("trace")]
    [HasPermission(RecallPermissions.TraceRun)]
    public async Task<IActionResult> RunTrace([FromQuery] string? rootLotId, [FromQuery] string? direction, [FromQuery] string? asOfUtc, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new RunRecallTraceCommand(rootLotId, direction, asOfUtc), ct));

    [HttpGet("trace/export")]
    [HasPermission(RecallPermissions.TraceExport)]
    public async Task<IActionResult> ExportTrace([FromQuery] string? rootLotId, [FromQuery] string? direction, [FromQuery] string? format, CancellationToken ct)
    {
        var response = await _mediator.Send(new ExportRecallTraceCommand(rootLotId, direction, format), ct);
        if (!response.IsSuccessful || response.Data is not { } csv) return CreateActionResultInstance(response);
        return File(new System.Text.UTF8Encoding(false).GetBytes(csv.Content), "text/csv; charset=utf-8", csv.FileName);
    }

    [HttpGet("cases/{caseId}/scope/diff")]
    [HasPermission(RecallPermissions.CasesRead)]
    public async Task<IActionResult> GetCaseScopeDiff(string caseId, CancellationToken ct)
        => await CompanyChecked(caseId, CreateActionResultInstance(await _mediator.Send(new DiffRecallScopeCommand(caseId), ct)), ct);

    // ── R3: stock + quarantine requests (KR-B), returns (KR-H), scope revision (CT-R5) ─────────────────────

    [HttpGet("cases/{caseId}/stock")]
    [HasPermission(RecallPermissions.CasesRead)]
    public async Task<IActionResult> GetCaseStock(string caseId, CancellationToken ct)
        => await CompanyChecked(caseId, CreateActionResultInstance(await _mediator.Send(new GetRecallCaseStockQuery(caseId), ct)), ct);

    [HttpPost("cases/{caseId}/quarantine-requests")]
    [HasPermission(RecallPermissions.QuarantineRequestsCreate)]
    public async Task<IActionResult> RequestQuarantine(string caseId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(caseId, out var id)) return NotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.RequestQuarantineMembers,
            b => new { NodeKeys = b.Strings("nodeKeys"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return await CompanyChecked(caseId, CreateActionResultInstance(await _mediator.Send(new RequestRecallQuarantineCommand(id, body.NodeKeys, key), ct)), ct);
    }

    [HttpPost("cases/{caseId}/quarantine-requests/{requestId}/retry")]
    [HasPermission(RecallPermissions.QuarantineRequestsCreate)]
    public async Task<IActionResult> RetryQuarantineRequest(string caseId, string requestId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(caseId, out var id) || !RecallSupport.TryParseId(requestId, out var request)) return NotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.KeyMembers, b => new { Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return await CompanyChecked(caseId, CreateActionResultInstance(await _mediator.Send(new RetryRecallQuarantineCommand(id, request, key), ct)), ct);
    }

    // ── R7a: the case detail reads (RECALL 1.6.0) ─────────────────────────────────────────────────────────

    [HttpGet("cases/{caseId}/customers")]
    [HasPermission(RecallPermissions.CasesRead)]
    public async Task<IActionResult> GetCaseCustomers(string caseId, CancellationToken ct)
        => await CompanyChecked(caseId, CreateActionResultInstance(await _mediator.Send(new GetRecallCaseCustomersQuery(caseId), ct)), ct);

    [HttpGet("cases/{caseId}/effectiveness")]
    [HasPermission(RecallPermissions.CasesRead)]
    public async Task<IActionResult> GetCaseEffectiveness(string caseId, CancellationToken ct)
        => await CompanyChecked(caseId, CreateActionResultInstance(await _mediator.Send(new GetRecallCaseEffectivenessQuery(caseId), ct)), ct);

    [HttpGet("cases/{caseId}/returns")]
    [HasPermission(RecallPermissions.CasesRead)]
    public async Task<IActionResult> GetCaseReturns(string caseId, CancellationToken ct)
        => await CompanyChecked(caseId, CreateActionResultInstance(await _mediator.Send(new GetRecallCaseReturnsQuery(caseId), ct)), ct);

    [HttpPost("cases/{caseId}/return-links")]
    [HasPermission(RecallPermissions.ReturnsLink)]
    public async Task<IActionResult> LinkReturn(string caseId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(caseId, out var id)) return NotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.LinkReturnMembers, b => new
        {
            TransactionId = b.String("transactionId"), CustomerRef = b.String("customerRef"), Reference = b.String("returnReference"), Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return await CompanyChecked(caseId, CreateActionResultInstance(await _mediator.Send(
            new LinkRecallReturnCommand(id, body.TransactionId, body.CustomerRef, body.Reference, key), ct)), ct);
    }

    [HttpPost("cases/{caseId}/scope/revisions")]
    [HasPermission(RecallPermissions.CasesUpdate)]
    public async Task<IActionResult> ReviseScope(string caseId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(caseId, out var id)) return NotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.ReviseMembers, b => new
        {
            Add = b.Strings("addNodeKeys"), Marks = b.OptionalMarks("marks"), Reason = b.String("reason"), Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(caseId, WithETag(await _mediator.Send(new ReviseRecallScopeCommand(id, body.Add, body.Marks, body.Reason, key, version), ct), d => d.Version), ct);
    }

    // ── cases (writes) ───────────────────────────────────────────────────────────────────────────────────

    [HttpPost("cases")]
    [HasPermission(RecallPermissions.CasesCreate)]
    public async Task<IActionResult> OpenCase(CancellationToken ct)
    {
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.OpenMembers, b => new
        {
            RootLotId = b.Uuid("rootLotId"),
            Direction = b.Enum<RecallTraceDirection>("direction"),
            Class = b.Int("class"),
            Level = b.Enum<RecallLevel>("level"),
            Reason = b.String("reason"),
            ReasonReference = b.String("reasonReference"),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return WithETag(await _mediator.Send(
            new OpenRecallCaseCommand(body.RootLotId, body.Direction, body.Class, body.Level, body.Reason, body.ReasonReference, key), ct), d => d.Version);
    }

    [HttpPost("cases/{caseId}/scope/items")]
    [HasPermission(RecallPermissions.CasesUpdate)]
    public async Task<IActionResult> AddScopeItems(string caseId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(caseId, out var id)) return NotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.AddItemsMembers,
            b => new { Items = b.ScopeItems("items"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(caseId, WithETag(await _mediator.Send(new AddRecallScopeItemsCommand(id, body.Items, key, version), ct), s => s.Version), ct);
    }

    [HttpPost("cases/{caseId}/scope/marks")]
    [HasPermission(RecallPermissions.CasesUpdate)]
    public async Task<IActionResult> MarkScopeItems(string caseId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(caseId, out var id)) return NotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.MarksMembers,
            b => new { Marks = b.Marks("marks"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(caseId, WithETag(await _mediator.Send(new MarkRecallScopeItemsCommand(id, body.Marks, key, version), ct), s => s.Version), ct);
    }

    [HttpPost("cases/{caseId}/scope/submit")]
    [HasPermission(RecallPermissions.CasesUpdate)]
    public Task<IActionResult> SubmitScope(string caseId, CancellationToken ct)
        => CaseCommand(caseId, RecallRequestReader.NoteMembers, b => b.String("note"), (id, note, key, version) => new SubmitRecallScopeCommand(id, note, key, version), ct);

    [HttpPost("cases/{caseId}/scope/return")]
    [HasPermission(RecallPermissions.CasesApprove)]
    public Task<IActionResult> ReturnScope(string caseId, CancellationToken ct)
        => CaseCommand(caseId, RecallRequestReader.ReasonMembers, b => b.String("reason"), (id, reason, key, version) => new ReturnRecallScopeCommand(id, reason, key, version), ct);

    /// <summary>
    /// The key of the prepared decision is the decision's own (approve / complete / close) — checked here against the caller's permission
    /// claims, because one route serves three decisions (pack §8.8); the route attribute is the read key every preparer holds.
    /// </summary>
    [HttpPost("cases/{caseId}/pending-action")]
    [HasPermission(RecallPermissions.CasesRead)]
    public async Task<IActionResult> PrepareCaseAction(string caseId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(caseId, out var id)) return NotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.PrepareMembers, b => new
        {
            Action = b.Enum<RecallPendingActionKind>("action"),
            Note = b.String("note"),
            Summary = b.String("summary"),
            Acceptance = b.String("openQuantityAcceptance"),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        var required = body.Action switch
        {
            RecallPendingActionKind.ApproveScope => RecallPermissions.CasesApprove,
            RecallPendingActionKind.CompleteEffectiveness => RecallPermissions.EffectivenessComplete,
            _ => RecallPermissions.CasesClose
        };
        if (!User.Claims.Any(c => c.Type == "permission" && c.Value == required))
        {
            return ContractFailure(StatusCodes.Status403Forbidden, null, $"The caller lacks {required} for {body.Action}.");
        }

        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(caseId, WithETag(await _mediator.Send(
            new PrepareRecallCaseActionCommand(id, body.Action, body.Note, body.Summary, body.Acceptance, key, version), ct), p => p.Version), ct);
    }

    [HttpPost("cases/{caseId}/scope/approve")]
    [HasPermission(RecallPermissions.CasesApprove)]
    public async Task<IActionResult> ApproveScope(string caseId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(caseId, out var id)) return NotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.SignedNoteMembers,
            b => new { Note = b.String("note"), SignatureId = b.String("signatureId"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(caseId, WithETag(await _mediator.Send(new ApproveRecallScopeCommand(id, body.Note, body.SignatureId, key, version), ct), d => d.Version), ct);
    }

    [HttpPost("cases/{caseId}/cancel")]
    [HasPermission(RecallPermissions.CasesCancel)]
    public Task<IActionResult> CancelCase(string caseId, CancellationToken ct)
        => CaseCommand(caseId, RecallRequestReader.ReasonMembers, b => b.String("reason"), (id, reason, key, version) => new CancelRecallCaseCommand(id, reason, key, version), ct);

    [HttpPost("cases/{caseId}/effectiveness/start")]
    [HasPermission(RecallPermissions.EffectivenessComplete)]
    public Task<IActionResult> StartEffectiveness(string caseId, CancellationToken ct)
        => CaseCommand(caseId, RecallRequestReader.KeyMembers, _ => null, (id, _, key, version) => new StartRecallEffectivenessCommand(id, key, version), ct);

    [HttpPost("cases/{caseId}/effectiveness/complete")]
    [HasPermission(RecallPermissions.EffectivenessComplete)]
    public async Task<IActionResult> CompleteEffectiveness(string caseId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(caseId, out var id)) return NotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.SignedNoteMembers,
            b => new { Note = b.String("note"), SignatureId = b.String("signatureId"), Key = b.String("idempotencyKey") }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(caseId, WithETag(await _mediator.Send(
            new CompleteRecallEffectivenessCommand(id, body.Note, body.SignatureId, key, version), ct), d => d.Version), ct);
    }

    [HttpPost("cases/{caseId}/close")]
    [HasPermission(RecallPermissions.CasesClose)]
    public async Task<IActionResult> CloseCase(string caseId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(caseId, out var id)) return NotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.CloseMembers, b => new
        {
            Summary = b.String("summary"),
            Acceptance = b.String("openQuantityAcceptance"),
            SignatureId = b.String("signatureId"),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(caseId, WithETag(await _mediator.Send(
            new CloseRecallCaseCommand(id, body.Summary, body.Acceptance, body.SignatureId, key, version), ct), d => d.Version), ct);
    }

    // ── records (append-only, no If-Match) ───────────────────────────────────────────────────────────────

    [HttpPost("cases/{caseId}/notifications")]
    [HasPermission(RecallPermissions.NotificationsCreate)]
    public async Task<IActionResult> RecordNotification(string caseId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(caseId, out var id)) return NotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.NotificationMembers, b => new
        {
            CustomerRef = b.String("customerRef"),
            NotifiedOn = b.Date("notifiedOn"),
            Channel = b.Enum<RecallNotificationChannel>("channel"),
            Note = b.String("note"),
            SupersedesId = b.OptionalUuid("supersedesId"),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return await CompanyChecked(caseId, CreateActionResultInstance(await _mediator.Send(
            new RecordRecallNotificationCommand(id, body.CustomerRef, body.NotifiedOn, body.Channel, body.Note, body.SupersedesId, key), ct)), ct);
    }

    [HttpPost("cases/{caseId}/consumption-declarations")]
    [HasPermission(RecallPermissions.ConsumptionsCreate)]
    public async Task<IActionResult> DeclareConsumption(string caseId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(caseId, out var id)) return NotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.ConsumptionMembers, b => new
        {
            CustomerRef = b.String("customerRef"),
            SkuId = b.Uuid("skuId"),
            SkuLevel = b.SkuLevel("skuLevel"),
            Quantity = b.Decimal("quantity"),
            DeclaredOn = b.Date("declaredOn"),
            Evidence = b.String("evidence"),
            SupersedesId = b.OptionalUuid("supersedesId"),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return await CompanyChecked(caseId, CreateActionResultInstance(await _mediator.Send(new DeclareRecallConsumptionCommand(
            id, body.CustomerRef, body.SkuId, body.SkuLevel, body.Quantity, body.DeclaredOn, body.Evidence, body.SupersedesId, key), ct)), ct);
    }

    [HttpPost("cases/{caseId}/segregations")]
    [HasPermission(RecallPermissions.SegregationsConfirm)]
    public async Task<IActionResult> ConfirmSegregation(string caseId, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(caseId, out var id)) return NotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, RecallRequestReader.SegregationMembers, b => new
        {
            NodeKey = b.String("nodeKey"),
            LocationId = b.Uuid("locationId"),
            Quantity = b.Decimal("quantity"),
            Note = b.String("note"),
            Key = b.String("idempotencyKey")
        }, ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        return await CompanyChecked(caseId, CreateActionResultInstance(await _mediator.Send(
            new ConfirmRecallSegregationCommand(id, body.NodeKey, body.LocationId, body.Quantity, body.Note, key), ct)), ct);
    }

    // ── plumbing ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The If-Match commands whose body is one optional text member (note / reason) and the key.</summary>
    private async Task<IActionResult> CaseCommand(string caseId, IReadOnlySet<string> members, Func<RecallRequestReader.Body, string?> text,
        Func<Guid, string?, string, int, IRequest<Response<RecallCaseDetailDto>>> command, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(caseId, out var id)) return NotFound();
        var read = await RecallRequestReader.ReadAsync(Request.Body, members, b => new TextBody(text(b), b.String("idempotencyKey")), ct);
        if (read.Value is not { } body) return Invalid(read.Error);
        if (KeyError(body.Key, out var key) is { } keyError) return Invalid(keyError);
        if (IfMatch() is not { } version) return Invalid("If-Match is required.");
        return await CompanyChecked(caseId, WithETag(await _mediator.Send(command(id, body.Text, key, version), ct), d => d.Version), ct);
    }

    private sealed record TextBody(string? Text, string? Key);

    /// <summary>
    /// V23 pattern: a case id of ANOTHER company of the caller's tenant answers 403 CROSS_LE_FORBIDDEN; another tenant's stays 404. The handlers
    /// read scoped to the company; only their 404 is re-checked here.
    /// </summary>
    private async Task<IActionResult> CompanyChecked(string id, IActionResult result, CancellationToken ct)
    {
        if (result is ObjectResult { StatusCode: StatusCodes.Status404NotFound } && RecallSupport.TryParseId(id, out var guid)
            && await HttpContext.RequestServices.GetRequiredService<IRecallRepository>().ExistsInAnotherCompanyAsync(guid, ct))
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

    private new IActionResult NotFound() => ContractFailure(StatusCodes.Status404NotFound, RecallErrorCodes.CaseNotFound, "Recall case not found.");
}
