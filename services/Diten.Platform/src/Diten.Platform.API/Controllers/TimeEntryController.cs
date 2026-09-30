using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

/// <summary>
/// MOD-0280-FU01 T1a (pack §3.4) — thin Time Entry controller under its own prefix <c>api/v1/time-entry</c> (one
/// prefix on purpose: on extraction the gateway pair is re-pointed and nothing else moves — ADR-004). The gateway
/// routes are a Control Tower prerequisite of T2 (§15); T1a is reachable on the service port only.
///
/// <para>The person is ALWAYS the caller — no endpoint takes a user id (D11). Approve and reject are MOD-0023's own
/// actions; there is deliberately no approve endpoint here (D6). T1b added the timer, suggestion and plan fill-in
/// routes.</para>
/// </summary>
[ApiController]
[Route("api/v1/time-entry")]
[Authorize]
public sealed class TimeEntryController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContext _correlationContext;

    public TimeEntryController(IMediator mediator, ICorrelationContext correlationContext)
    {
        _mediator = mediator;
        _correlationContext = correlationContext;
    }

    private string CorrelationId =>
        string.IsNullOrWhiteSpace(_correlationContext.CorrelationId)
            ? HttpContext.TraceIdentifier
            : _correlationContext.CorrelationId!;

    // ── My weeks ────────────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("weeks/{weekKey}")]
    [HasPermission(TimeEntryPermissions.TimesheetsRead)]
    public async Task<IActionResult> GetWeek(string weekKey, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetMyTimesheetWeekQuery(weekKey, CorrelationId), ct));

    [HttpPut("weeks/{weekKey}/entries")]
    [HasPermission(TimeEntryPermissions.TimesheetsUpdate)]
    public async Task<IActionResult> SaveEntries(string weekKey, [FromBody] SaveTimeEntriesRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new SaveTimeEntriesCommand(weekKey, request, CorrelationId), ct));

    [HttpPost("weeks/{weekKey}/submit")]
    [HasPermission(TimeEntryPermissions.TimesheetsUpdate)]
    public async Task<IActionResult> Submit(string weekKey, [FromBody] SubmitTimesheetWeekRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new SubmitTimesheetWeekCommand(weekKey, request, CorrelationId), ct));

    [HttpPost("weeks/{weekKey}/withdraw")]
    [HasPermission(TimeEntryPermissions.TimesheetsUpdate)]
    public async Task<IActionResult> Withdraw(string weekKey, [FromBody] WithdrawTimesheetWeekRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new WithdrawTimesheetWeekCommand(weekKey, request, CorrelationId), ct));

    [HttpPost("weeks/{weekKey}/corrections")]
    [HasPermission(TimeEntryPermissions.TimesheetsUpdate)]
    public async Task<IActionResult> RequestCorrection(
        string weekKey, [FromBody] RequestTimesheetCorrectionRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new RequestTimesheetCorrectionCommand(weekKey, request, CorrelationId), ct));

    [HttpDelete("weeks/{weekKey}/corrections/draft")]
    [HasPermission(TimeEntryPermissions.TimesheetsUpdate)]
    public async Task<IActionResult> DiscardCorrectionDraft(string weekKey, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new DiscardCorrectionDraftCommand(weekKey, CorrelationId), ct));

    [HttpGet("weeks/{weekKey}/plan-fill-in")]
    [HasPermission(TimeEntryPermissions.TimesheetsRead)]
    public async Task<IActionResult> GetPlanFillIn(string weekKey, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetPlanFillInQuery(weekKey, CorrelationId), ct));

    [HttpPost("weeks/{weekKey}/suggestions/{id:guid}/accept")]
    [HasPermission(TimeEntryPermissions.TimesheetsUpdate)]
    public async Task<IActionResult> AcceptSuggestion(
        string weekKey, Guid id, [FromBody] AcceptTimeSuggestionRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new AcceptTimeSuggestionCommand(weekKey, id, request, CorrelationId), ct));

    [HttpPost("weeks/{weekKey}/suggestions/{id:guid}/dismiss")]
    [HasPermission(TimeEntryPermissions.TimesheetsUpdate)]
    public async Task<IActionResult> DismissSuggestion(string weekKey, Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new DismissTimeSuggestionCommand(weekKey, id, CorrelationId), ct));

    /// <summary>T2a — the "+ Task row" picker: open tasks the caller holds and tasks they recorded time on in the edit
    /// window, only those they can read; at most 50, filtered by title.</summary>
    [HttpGet("task-options")]
    [HasPermission(TimeEntryPermissions.TimesheetsUpdate)]
    public async Task<IActionResult> GetTaskOptions([FromQuery] string? search, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetTimeEntryTaskOptionsQuery(search, CorrelationId), ct));

    // ── My timer (T1b) — always the caller's own; no endpoint shows anyone else's (D11) ────────────────────────

    [HttpGet("timer")]
    [HasPermission(TimeEntryPermissions.TimesheetsRead)]
    public async Task<IActionResult> GetTimer(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetMyTimerQuery(CorrelationId), ct));

    [HttpPost("timer/start")]
    [HasPermission(TimeEntryPermissions.TimesheetsUpdate)]
    public async Task<IActionResult> StartTimer([FromBody] StartTimerRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new StartTimerCommand(request, CorrelationId), ct));

    [HttpPost("timer/stop")]
    [HasPermission(TimeEntryPermissions.TimesheetsUpdate)]
    public async Task<IActionResult> StopTimer(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new StopTimerCommand(CorrelationId), ct));

    [HttpPost("timer/undo-switch")]
    [HasPermission(TimeEntryPermissions.TimesheetsUpdate)]
    public async Task<IActionResult> UndoTimerSwitch([FromBody] UndoTimerSwitchRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new UndoTimerSwitchCommand(request, CorrelationId), ct));

    // ── Approvals (read only — the decision is MOD-0023's) ──────────────────────────────────────────────────────

    [HttpGet("approvals")]
    [HasPermission(TimeEntryPermissions.ApprovalsRead)]
    public async Task<IActionResult> GetApprovals(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] int? start = null, [FromQuery] int? length = null, [FromQuery] string? search = null,
        [FromQuery] string? orderBy = null, [FromQuery] string? orderDir = null, CancellationToken ct = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetApprovalListQuery(page, pageSize, CorrelationId, start, length, search, orderBy, orderDir), ct));

    [HttpGet("approvals/{weekId:guid}")]
    [HasPermission(TimeEntryPermissions.ApprovalsRead)]
    public async Task<IActionResult> GetApprovalWeek(Guid weekId, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetApprovalWeekQuery(weekId, CorrelationId), ct));

    // ── Time admin ──────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>F4 — addressed by (person, week): a never-written week has no row to name, and no GET creates one.</summary>
    [HttpPost("admin/weeks/reopen")]
    [HasPermission(TimeEntryPermissions.WeeksReopen)]
    public async Task<IActionResult> Reopen([FromBody] ReopenTimesheetWeekRequest request, CancellationToken ct)
    {
        // F15 — the server names the revision BEFORE the audited command runs, so its audit entry carries the week id:
        // the existing revision's, or the id the new one will be created with.
        var target = await _mediator.Send(new ResolveReopenTargetQuery(request.UserId, request.WeekKey, CorrelationId), ct);
        var weekId = target.Data ?? Guid.NewGuid();
        return CreateActionResultInstance(await _mediator.Send(new ReopenTimesheetWeekCommand(request, weekId, CorrelationId), ct));
    }

    // ── Categories ──────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>F6 — what a person can pick: ACTIVE categories only.</summary>
    [HttpGet("categories")]
    [HasPermission(TimeEntryPermissions.TimesheetsRead)]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWorkCategoryListQuery(ActiveOnly: true, CorrelationId), ct));

    /// <summary>F6 — the catalogue as its manager sees it: active and retired.</summary>
    [HttpGet("categories/manage")]
    [HasPermission(TimeEntryPermissions.CategoriesManage)]
    public async Task<IActionResult> GetCategoriesForManagement(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWorkCategoryListQuery(ActiveOnly: false, CorrelationId), ct));

    [HttpGet("categories/{id:guid}")]
    [HasPermission(TimeEntryPermissions.TimesheetsRead)]
    public async Task<IActionResult> GetCategory(Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWorkCategoryByIdQuery(id, CorrelationId), ct));

    [HttpPost("categories")]
    [HasPermission(TimeEntryPermissions.CategoriesManage)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateWorkCategoryRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new CreateWorkCategoryCommand(request, CorrelationId), ct));

    [HttpPut("categories/{id:guid}")]
    [HasPermission(TimeEntryPermissions.CategoriesManage)]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateWorkCategoryRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new UpdateWorkCategoryCommand(id, request, CorrelationId), ct));

    [HttpPost("categories/{id:guid}/activate")]
    [HasPermission(TimeEntryPermissions.CategoriesManage)]
    public async Task<IActionResult> ActivateCategory(Guid id, [FromBody] WorkCategoryStateRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new ActivateWorkCategoryCommand(id, request, CorrelationId), ct));

    [HttpPost("categories/{id:guid}/deactivate")]
    [HasPermission(TimeEntryPermissions.CategoriesManage)]
    public async Task<IActionResult> DeactivateCategory(Guid id, [FromBody] WorkCategoryStateRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new DeactivateWorkCategoryCommand(id, request, CorrelationId), ct));

    [HttpPost("categories/install-recommended")]
    [HasPermission(TimeEntryPermissions.CategoriesManage)]
    public async Task<IActionResult> InstallRecommendedCategories(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new InstallRecommendedWorkCategoriesCommand(CorrelationId), ct));

    // ── Settings ────────────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("settings")]
    [HasPermission(TimeEntryPermissions.SettingsManage)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetTimeEntrySettingsQuery(CorrelationId), ct));

    [HttpPut("settings")]
    [HasPermission(TimeEntryPermissions.SettingsManage)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateTimeEntrySettingsRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new UpdateTimeEntrySettingsCommand(request, CorrelationId), ct));

    [HttpGet("settings/legal-entities")]
    [HasPermission(TimeEntryPermissions.SettingsManage)]
    public async Task<IActionResult> GetLegalEntitySettings(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetLegalEntityTimeSettingListQuery(CorrelationId), ct));

    [HttpPut("settings/legal-entities/{legalEntityId:guid}")]
    [HasPermission(TimeEntryPermissions.SettingsManage)]
    public async Task<IActionResult> SetLegalEntityTimerSwitch(
        Guid legalEntityId, [FromBody] SetLegalEntityTimerSwitchRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new SetLegalEntityTimerSwitchCommand(legalEntityId, request, CorrelationId), ct));
}
