using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Queries;
using MediatR;

namespace Diten.Platform.Application.Services;

/// <summary>
/// A3 — in-process workflow gate. Delegates to the existing <see cref="EvaluateWorkflowTransitionGateQuery"/> and
/// reduces its rich response to a simple allow/block decision. NotApplicable (no workflow attached) ⇒ allowed.
/// A non-successful evaluation ⇒ blocked (fail-closed).
/// </summary>
public sealed class WorkflowTransitionGate : IWorkflowTransitionGate
{
    private readonly IMediator _mediator;

    public WorkflowTransitionGate(IMediator mediator)
    {
        _mediator = mediator;
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

        var response = await _mediator.Send(query, ct);
        if (!response.IsSuccessful || response.Data is not { } data)
        {
            // Fail-closed: if the gate cannot be evaluated, block the transition.
            var error = response.Errors.Count > 0 ? string.Join("; ", response.Errors) : "Workflow gate evaluation failed.";
            return new WorkflowGateResult(
                IsAllowed: false,
                Decision: WorkflowTransitionGateDecision.Blocked.ToString(),
                GateStatus: "EvaluationFailed",
                BlockingReasonCode: "WorkflowGateEvaluationFailed",
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
