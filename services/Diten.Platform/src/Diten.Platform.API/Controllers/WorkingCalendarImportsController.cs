using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.WorkingCalendar;
using Diten.Platform.Application.Features.WorkingCalendarImport;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

[ApiController]
[Authorize(Policy = "PlatformActor")]
[Route("api/platform/working-calendars/imports")]
public sealed class WorkingCalendarImportsController : CustomBaseController
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserContext _currentUser;
    public WorkingCalendarImportsController(IMediator mediator, ICurrentUserContext currentUser)
        => (_mediator, _currentUser) = (mediator, currentUser);

    [HttpGet("contract")]
    [HasPermission(WorkingCalendarImportPermissionKeys.Read)]
    [RequiresExplicitPermission(WorkingCalendarImportPermissionKeys.Read)]
    public async Task<IActionResult> Contract(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWorkingCalendarImportContractQuery(), ct));

    [HttpGet]
    [HasPermission(WorkingCalendarImportPermissionKeys.Read)]
    [RequiresExplicitPermission(WorkingCalendarImportPermissionKeys.Read)]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? countryCode,
        [FromQuery] int? calendarYear, [FromQuery] Guid? targetCalendarId, [FromQuery] string? triggerSource, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new ListWorkingCalendarImportsQuery(status, countryCode,
            calendarYear, targetCalendarId, triggerSource), ct));

    [HttpGet("{id:guid}")]
    [HasPermission(WorkingCalendarImportPermissionKeys.Read)]
    [RequiresExplicitPermission(WorkingCalendarImportPermissionKeys.Read)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWorkingCalendarImportByIdQuery(id), ct));

    [HttpGet("provider-status")]
    [HasPermission(WorkingCalendarImportPermissionKeys.Read)]
    [RequiresExplicitPermission(WorkingCalendarImportPermissionKeys.Read)]
    public async Task<IActionResult> ProviderStatus(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWorkingCalendarImportProviderStatusQuery(), ct));

    [HttpGet("schedule")]
    [HasPermission(WorkingCalendarImportPermissionKeys.Read)]
    [RequiresExplicitPermission(WorkingCalendarImportPermissionKeys.Read)]
    public async Task<IActionResult> Schedule(CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new GetWorkingCalendarImportScheduleQuery(), ct));

    [HttpPost]
    [HasPermission(WorkingCalendarImportPermissionKeys.Run)]
    [RequiresExplicitPermission(WorkingCalendarImportPermissionKeys.Run)]
    public async Task<IActionResult> Start([FromBody] StartImportRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new StartWorkingCalendarImportCommand(request.TargetCalendarId,
            request.IncludeNonPublicTypes, request.Notes, WorkingCalendarImportTriggerSource.Manual, _currentUser.ActorName), ct));

    [HttpPost("{id:guid}/candidates/{candidateId:guid}/decision")]
    [HasPermission(WorkingCalendarImportPermissionKeys.Review)]
    [RequiresExplicitPermission(WorkingCalendarImportPermissionKeys.Review)]
    public async Task<IActionResult> DecideCandidate(Guid id, Guid candidateId, [FromBody] DecisionRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new DecideWorkingCalendarImportCandidateCommand(id, candidateId,
            request.Decision, request.Reason, _currentUser.ActorName), ct));

    [HttpPost("{id:guid}/decisions")]
    [HasPermission(WorkingCalendarImportPermissionKeys.Review)]
    [RequiresExplicitPermission(WorkingCalendarImportPermissionKeys.Review)]
    public async Task<IActionResult> DecideBatch(Guid id, [FromBody] BatchDecisionsRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new DecideWorkingCalendarImportBatchCommand(id,
            request.Decisions.Select(x => new WorkingCalendarImportDecisionInput(x.CandidateId, x.Decision, x.Reason)).ToList(),
            _currentUser.ActorName), ct));

    [HttpPost("{id:guid}/apply")]
    [HasPermission(WorkingCalendarImportPermissionKeys.Apply)]
    [RequiresExplicitPermission(WorkingCalendarImportPermissionKeys.Apply)]
    public async Task<IActionResult> Apply(Guid id, [FromBody] ApplyImportRequest request, CancellationToken ct)
    {
        var claims = User.Claims;
        var hasApply = PermissionClaimEvaluator.Evaluate(claims, WorkingCalendarImportPermissionKeys.Apply).IsSatisfied;
        var hasActivate = PermissionClaimEvaluator.Evaluate(claims, WorkingCalendarPermissions.Activate).IsSatisfied;
        return CreateActionResultInstance(await _mediator.Send(new ApplyWorkingCalendarImportCommand(id,
            request.ExpectedBatchVersion, request.ExpectedCalendarVersion, _currentUser.ActorName, hasApply, hasActivate), ct));
    }

    [HttpPost("{id:guid}/discard")]
    [HasPermission(WorkingCalendarImportPermissionKeys.Review)]
    [RequiresExplicitPermission(WorkingCalendarImportPermissionKeys.Review)]
    public async Task<IActionResult> Discard(Guid id, [FromBody] DiscardImportRequest request, CancellationToken ct)
        => CreateActionResultInstance(await _mediator.Send(new DiscardWorkingCalendarImportCommand(id,
            request.ExpectedVersion, request.Reason, _currentUser.ActorName), ct));
}

public sealed record StartImportRequest(Guid TargetCalendarId, bool IncludeNonPublicTypes, string? Notes);
public sealed record DecisionRequest(string Decision, string? Reason);
public sealed record CandidateDecisionRequest(Guid CandidateId, string Decision, string? Reason);
public sealed record BatchDecisionsRequest(IReadOnlyList<CandidateDecisionRequest> Decisions);
public sealed record ApplyImportRequest(int ExpectedBatchVersion, int ExpectedCalendarVersion);
public sealed record DiscardImportRequest(int ExpectedVersion, string? Reason);
