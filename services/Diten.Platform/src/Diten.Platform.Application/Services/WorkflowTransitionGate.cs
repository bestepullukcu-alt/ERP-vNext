using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Queries;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Services;

/// <summary>
/// A3 — in-process workflow gate. Delegates to the existing <see cref="EvaluateWorkflowTransitionGateQuery"/> and
/// reduces its rich response to a simple allow/block decision. NotApplicable (no workflow attached) ⇒ allowed.
/// A non-successful evaluation ⇒ blocked (fail-closed) — and so is a THROWN one. That second half was missing:
/// only <c>!IsSuccessful</c> was handled, so any exception from the evaluation (a repository fault, a validation
/// failure, a workflow module outage) escaped the gate and surfaced to the caller as an unhandled HTTP 500 while
/// the transition itself was correctly not committed. Fail-closed has to mean "answer blocked", not "crash and
/// happen to block" — the caller cannot turn a crash into a business message for the user.
/// </summary>
/// <summary>
/// Reason codes the GATE itself produces (as opposed to <see cref="WorkflowReasonCodes"/>, which MOD-0023 produces).
/// Named so the frontend code→message bridge can translate them instead of falling back to a generic error.
/// </summary>
public static class WorkflowGateReasonCodes
{
    /// <summary>The evaluation could not be completed, so the transition is refused. Kept at its original spelling
    /// (not SCREAMING_SNAKE like the others) because it is already on the wire and clients key on it.</summary>
    public const string EvaluationFailed = "WorkflowGateEvaluationFailed";
}

public sealed class WorkflowTransitionGate : IWorkflowTransitionGate
{
    private readonly IMediator _mediator;
    private readonly ILogger<WorkflowTransitionGate> _logger;

    public WorkflowTransitionGate(IMediator mediator, ILogger<WorkflowTransitionGate> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<WorkflowGateResult> EvaluateAsync(WorkflowGateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? Guid.NewGuid().ToString()
            : request.CorrelationId;

        var query = new EvaluateWorkflowTransitionGateQuery(
            new EvaluateWorkflowTransitionGateRequest(
                request.ObjectType,
                request.ObjectId,
                request.ObjectRef,
                request.RequestedTransition,
                request.RequestedTargetState,
                request.ActorId,
                request.ReasonCode),
            correlationId);

        Common.Response<EvaluateWorkflowTransitionGateResponse> response;
        try
        {
            response = await _mediator.Send(query, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fail-closed on a THROWN evaluation. Logged as a warning with the object it was about: the transition
            // is refused, which is a business outcome the caller reports as a conflict, but the underlying fault
            // still needs to be visible to whoever owns the workflow module.
            _logger.LogWarning(
                ex,
                "Workflow gate evaluation threw for {ObjectRef} ({RequestedTransition}); treating the transition as blocked.",
                request.ObjectRef,
                request.RequestedTransition);

            return new WorkflowGateResult(
                IsAllowed: false,
                Decision: WorkflowTransitionGateDecision.Blocked.ToString(),
                GateStatus: "EvaluationFailed",
                BlockingReasonCode: WorkflowGateReasonCodes.EvaluationFailed,
                BlockingMessage: "The workflow gate could not be evaluated.",
                CorrelationId: correlationId);
        }

        if (!response.IsSuccessful || response.Data is not { } data)
        {
            // Fail-closed: if the gate cannot be evaluated, block the transition.
            var error = response.Errors.Count > 0 ? string.Join("; ", response.Errors) : "Workflow gate evaluation failed.";
            return new WorkflowGateResult(
                IsAllowed: false,
                Decision: WorkflowTransitionGateDecision.Blocked.ToString(),
                GateStatus: "EvaluationFailed",
                BlockingReasonCode: WorkflowGateReasonCodes.EvaluationFailed,
                BlockingMessage: error,
                CorrelationId: correlationId);
        }

        // Allowed or NotApplicable (no workflow → nothing to gate) both permit the commit.
        var allowed = data.Decision is WorkflowTransitionGateDecision.Allowed or WorkflowTransitionGateDecision.NotApplicable;

        return new WorkflowGateResult(
            IsAllowed: allowed,
            Decision: data.Decision.ToString(),
            GateStatus: data.GateStatus.ToString(),
            BlockingReasonCode: allowed ? null : data.BlockingReasonCode,
            BlockingMessage: allowed ? null : data.BlockingMessage,
            CorrelationId: data.CorrelationId ?? correlationId);
    }

    public async Task EnsureAllowedOrThrowAsync(WorkflowGateRequest request, CancellationToken ct = default)
    {
        var result = await EvaluateAsync(request, ct);
        if (result.IsBlocked)
        {
            throw new WorkflowTransitionBlockedException(result);
        }
    }
}
