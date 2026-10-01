using Diten.Platform.Domain.Entities.Workflow;

namespace Diten.Platform.Application.Features.WorkAggregation.Services;

// WC-1 (DCP-004) — the pure mapping engine that turns a MOD-0023 ApprovalTask (+ its joined
// WorkflowInstance) into the canonical, contract-conformant WorkItemProjectionDto. Deterministic and
// side-effect-free: same input → same output, zero writes.
//
// Returns null when the item must be HIDDEN from the current actor (e.g. a Delegated task — it is a
// disposition, not this actor's active work) or when the source object cannot be resolved (a work item
// without its source is not projectable).
public interface IWorkItemProjectionService
{
    WorkItemProjectionDto? Project(
        ApprovalTask task,
        WorkflowInstance? instance,
        WorkItemActor actor,
        string providerCode,
        string providerContractVersion,
        // BL-437 — the source owner's answer (title, requester, address). Optional: without it the item keeps the
        // generic fallback title and carries no requester, link or reason — exactly today's shape.
        ApprovalSourceContext? sourceContext = null,
        // REQ-WCN-01 — what the pinned template version says about the task's step. Optional: without it the item
        // carries no step name and no candidate positions — exactly today's shape.
        ApprovalStepContext? stepContext = null);
}

/// <summary>
/// REQ-WCN-01 — the step an approval task sits on, as its pinned template version describes it. Read-only display
/// facts; never a decision input.
/// </summary>
/// <param name="StepName">The step's own name (W-1); null when the definition gives none.</param>
/// <param name="CandidatePositionNames">Names of the step's candidate positions (W-3); null or empty when the step
/// names a person directly or no position could be read.</param>
public sealed record ApprovalStepContext(string? StepName, IReadOnlyList<string>? CandidatePositionNames);
