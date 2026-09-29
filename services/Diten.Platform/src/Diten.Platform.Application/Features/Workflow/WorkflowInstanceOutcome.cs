using Diten.Platform.Domain.Enums.Workflow;

namespace Diten.Platform.Application.Features.Workflow;

/// <summary>
/// WP-WORKFLOW-APPROVAL-STATUS-01 (B1) — what a MOD-0023 instance status MEANS to a module waiting on it, decided in ONE
/// place.
///
/// <para><b>Why this exists.</b> MOD-0023 closes an approved instance as <see cref="WorkflowInstanceStatus.Completed"/>
/// (the last approve in <c>WorkflowTaskTransitionSupport</c>); nothing writes <see cref="WorkflowInstanceStatus.Approved"/>.
/// MOD-0024 counted only <c>Approved</c> as approval, so every really-approved task kept reading "start closed —
/// approval pending". The tests that should have caught it SEEDED <c>Approved</c> instead of going through the real
/// approve route.</para>
///
/// <para>Escalated and TimedOut are NOT approvals: nobody decided, and treating them as such would let unapproved work
/// start.</para>
/// </summary>
public static class WorkflowInstanceOutcome
{
    public static bool IsApproved(WorkflowInstanceStatus status)
        => status is WorkflowInstanceStatus.Approved or WorkflowInstanceStatus.Completed;

    public static bool IsRejected(WorkflowInstanceStatus status) => status is WorkflowInstanceStatus.Rejected;
}
