using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;

/// <summary>ABB rules that the workflow, the Task Center dispatcher and the register repository answer alike.</summary>
public static class ProductAbbreviationDecisionRules
{
    /// <summary>
    /// FIX4 item 4 — an approver's decision (approved or rejected) already stands on this request and it is someone
    /// else's. The stored state says so whatever key the late caller sent: two approvers in the Task Center send
    /// different keys as soon as their reasons differ, and the late one must still hear "another approver decided".
    /// A cancellation is the requester withdrawing, not an approver deciding, so it is not this answer.
    /// </summary>
    public static bool DecidedByAnotherApprover(ProductAbbreviationRegisterEntry entry, string actor)
        => entry.LifecycleStatus is ProductAbbreviationLifecycleStatus.ACTIVE or ProductAbbreviationLifecycleStatus.REJECTED
           && !string.IsNullOrEmpty(entry.LastDecisionByCanonicalSubjectId)
           && !string.Equals(entry.LastDecisionByCanonicalSubjectId, actor, StringComparison.Ordinal);
}
