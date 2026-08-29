using Diten.Platform.API.Controllers.Common;
using Diten.Platform.API.Models.Workflow;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers.Internal;

[ApiController]
[Authorize(AuthenticationSchemes = TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme)]
[Route("api/internal/v1/workflow/trusted-consumer")]
public sealed class InternalTrustedWorkflowConsumerController : CustomBaseController
{
    private readonly ITrustedWorkflowConsumerRequestExecutor _executor;
    private readonly IMediator _mediator;

    public InternalTrustedWorkflowConsumerController(
        ITrustedWorkflowConsumerRequestExecutor executor,
        IMediator mediator)
    {
        _executor = executor;
        _mediator = mediator;
    }

    [HttpPost("start")]
    public Task<IActionResult> Start(CancellationToken cancellationToken) =>
        _executor.ExecuteStartAsync(
            HttpContext,
            cancellationToken,
            DispatchStartAsync,
            StartFailure);

    [HttpPost("terminal-decision-evidence")]
    public Task<IActionResult> GetTerminalDecisionEvidence(CancellationToken cancellationToken) =>
        _executor.ExecuteEvidenceAsync(
            HttpContext,
            cancellationToken,
            DispatchEvidenceAsync,
            EvidenceFailure);

    [HttpPost("start-result")]
    public Task<IActionResult> GetStartResult(CancellationToken cancellationToken) =>
        _executor.ExecuteStartResultAsync(
            HttpContext,
            cancellationToken,
            DispatchStartResultAsync,
            StartFailure);

    private async Task<IActionResult> DispatchStartAsync(
        TrustedWorkflowStartTransportRequest request,
        string idempotencyKey,
        TrustedWorkflowConsumerServiceIdentity serviceIdentity,
        TrustedWorkflowDelegatedUserIdentity delegatedUser,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new StartTrustedWorkflowInstanceCommand(
            new TrustedWorkflowStartRequest(
                request.TemplateId,
                request.TemplateCode,
                request.ObjectType,
                request.ObjectId,
                request.ObjectRef,
                request.CandidatePrincipalIds,
                request.ReasonCode,
                idempotencyKey,
                request.CommentRequired,
                request.EvidenceRequired,
                request.DueAt),
            serviceIdentity.ClientId,
            delegatedUser.UserId,
            HttpContext.TraceIdentifier), cancellationToken);
        return CreateActionResultInstance(response);
    }

    private async Task<IActionResult> DispatchEvidenceAsync(
        TrustedWorkflowTerminalDecisionEvidenceTransportRequest request,
        TrustedWorkflowConsumerServiceIdentity serviceIdentity,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetTrustedWorkflowTerminalDecisionEvidenceQuery(
            request.WorkflowInstanceId,
            serviceIdentity.ClientId,
            request.ExpectedObjectType,
            request.ExpectedObjectId,
            HttpContext.TraceIdentifier), cancellationToken);
        return CreateActionResultInstance(response);
    }

    private async Task<IActionResult> DispatchStartResultAsync(
        TrustedWorkflowStartResultTransportRequest request,
        string idempotencyKey,
        TrustedWorkflowConsumerServiceIdentity serviceIdentity,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetTrustedWorkflowStartResultQuery(
            idempotencyKey,
            serviceIdentity.ClientId,
            request.ExpectedMakerSubjectId,
            request.ExpectedObjectType,
            request.ExpectedObjectId,
            HttpContext.TraceIdentifier), cancellationToken);
        return CreateActionResultInstance(response);
    }

    private IActionResult StartFailure(int statusCode, string code) => CreateActionResultInstance(
        Response<TrustedWorkflowStartResult>.Fail(code, statusCode, code, HttpContext.TraceIdentifier));

    private IActionResult EvidenceFailure(int statusCode, string code) => CreateActionResultInstance(
        Response<TrustedWorkflowTerminalDecisionEvidence>.Fail(
            code,
            statusCode,
            code,
            HttpContext.TraceIdentifier));
}
