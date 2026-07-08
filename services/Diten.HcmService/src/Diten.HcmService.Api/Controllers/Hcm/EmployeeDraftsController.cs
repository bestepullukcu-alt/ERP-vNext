using Diten.HcmService.Application.Features.CoreHrEmployeeMaster;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Commands;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster.Queries;
using Diten.HcmService.Application.Common.Models;
using Diten.HcmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.HcmService.Api.Controllers.Hcm;

[Authorize]
[ApiController]
[Route("api/v1/hcm/employees/drafts")]
public sealed class EmployeeDraftsController : CustomBaseController
{
    private const string DraftPermission = "mod0251.employee.create_draft";
    private const string SubmitPermission = "mod0251.employee.submit";
    private const string LifecycleScopeBlockedMessage =
        "MOD-0251 lifecycle activation is not enabled under the current approved draft/reference-validation scope.";
    private readonly IMediator _mediator;

    public EmployeeDraftsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [HasPermission(DraftPermission)]
    public async Task<IActionResult> Create([FromBody] EmployeeDraftCreateRequest request, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new CreateEmployeeDraftCommand(request), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPatch("{draftSessionId:guid}")]
    [HasPermission(DraftPermission)]
    public async Task<IActionResult> Patch(
        Guid draftSessionId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] EmployeeDraftPatchRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new PatchEmployeeDraftCommand(draftSessionId, ifMatch, request), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpGet("{draftSessionId:guid}")]
    [HasPermission(DraftPermission)]
    public async Task<IActionResult> Get(Guid draftSessionId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetEmployeeDraftQuery(draftSessionId), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{draftSessionId:guid}/validate-references")]
    [HasPermission(DraftPermission)]
    public async Task<IActionResult> ValidateReferences(
        Guid draftSessionId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] ReferenceValidationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ValidateDraftReferencesCommand(draftSessionId, ifMatch, request), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{draftSessionId:guid}/review")]
    [HasPermission(DraftPermission)]
    public async Task<IActionResult> Review(
        Guid draftSessionId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] DraftReviewRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new ReviewEmployeeDraftCommand(draftSessionId, ifMatch, request), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{draftSessionId:guid}/submit")]
    [HasPermission(SubmitPermission)]
    public Task<IActionResult> Submit(
        Guid draftSessionId,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromBody] DraftSubmitRequest request,
        CancellationToken cancellationToken)
    {
        var response = Response<DraftSubmitResponse>.Fail(LifecycleScopeBlockedMessage, 409);
        return Task.FromResult(CreateActionResultInstance(response));
    }

    [HttpPost("workflow-decisions")]
    public Task<IActionResult> ConsumeWorkflowDecision(
        [FromBody] WorkflowApprovalDecisionRecordedMessage message,
        CancellationToken cancellationToken)
    {
        var response = Response<WorkflowDecisionConsumptionResponse>.Fail(LifecycleScopeBlockedMessage, 409);
        return Task.FromResult(CreateActionResultInstance(response));
    }
}
