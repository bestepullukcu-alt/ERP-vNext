namespace Diten.Platform.Application.Contracts;

/// <summary>
/// A3 — defence-in-depth workflow gate. A state-transitioning business module injects this and calls it
/// BEFORE committing a transition. If the result is blocked, the module MUST NOT commit. This is the in-process
/// counterpart of the <c>POST /api/v1/workflow/transitions/evaluate</c> endpoint (which cross-service callers use).
/// NOT best-effort: a failed evaluation is treated as blocked (fail-closed), so a gate outage cannot let an
/// unapproved transition through.
/// </summary>
public interface IWorkflowTransitionGate
{
    /// <summary>Evaluates whether the proposed transition is permitted by the workflow (if any) for the object.</summary>
    Task<WorkflowGateResult> EvaluateAsync(WorkflowGateRequest request, CancellationToken ct = default);

    /// <summary>Evaluates and throws <see cref="WorkflowTransitionBlockedException"/> when blocked — convenience
    /// for callers that want to fail the operation rather than branch on the result.</summary>
    Task EnsureAllowedOrThrowAsync(WorkflowGateRequest request, CancellationToken ct = default);
}

public sealed record WorkflowGateRequest(
    string ObjectType,
    string ObjectId,
    string ObjectRef,
    string RequestedTransition,
    string RequestedTargetState,
    string ActorId,
    string? ReasonCode = null,
    string? CorrelationId = null);

public sealed record WorkflowGateResult(
    bool IsAllowed,
    string Decision,
    string GateStatus,
    string? BlockingReasonCode,
    string? BlockingMessage,
    string? CorrelationId)
{
    public bool IsBlocked => !IsAllowed;
}

/// <summary>Thrown by <see cref="IWorkflowTransitionGate.EnsureAllowedOrThrowAsync"/> when a transition is blocked.</summary>
public sealed class WorkflowTransitionBlockedException : Exception
{
    public WorkflowTransitionBlockedException(WorkflowGateResult result)
        : base(result.BlockingMessage ?? $"Workflow gate blocked the transition ({result.BlockingReasonCode ?? result.GateStatus}).")
    {
        Result = result;
    }

    public WorkflowGateResult Result { get; }
}
