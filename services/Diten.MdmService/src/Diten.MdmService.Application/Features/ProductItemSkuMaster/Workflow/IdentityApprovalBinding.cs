using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

/// <summary>
/// MOD-0290-FU04 S2 (§2.6, "paylaşılan bağlantı noktası") — the part of the approval binding every product-identity
/// flow reads the engine with, ONE place instead of a copy per flow: how the engine's terminal action maps to a
/// decision, which task / instance statuses a decision must come with, which start refusal proves nothing was
/// processed, and how a code read from the engine is bounded. The Global Product, first-GSKU and item flows call it;
/// each rule here is exactly what those flows had (no rule loosened — they read the same answers the same way).
/// </summary>
public static class IdentityApprovalBinding
{
    /// <summary>The engine's terminal action → the decision. Anything else is not a decision.</summary>
    public static bool TryMapDecision(string action, out ProductIdentityDecisionKind decision)
    {
        if (string.Equals(action, "Approve", StringComparison.Ordinal))
        {
            decision = ProductIdentityDecisionKind.Approved;
            return true;
        }
        if (string.Equals(action, "Reject", StringComparison.Ordinal))
        {
            decision = ProductIdentityDecisionKind.Rejected;
            return true;
        }
        decision = default;
        return false;
    }

    /// <summary>An approval comes with task Approved + instance Completed; a rejection with Rejected + Rejected.</summary>
    public static bool ExactTerminalStatuses(ProductIdentityDecisionKind decision, string taskStatus, string instanceStatus) =>
        decision switch
        {
            ProductIdentityDecisionKind.Approved =>
                string.Equals(taskStatus, "Approved", StringComparison.Ordinal)
                && string.Equals(instanceStatus, "Completed", StringComparison.Ordinal),
            ProductIdentityDecisionKind.Rejected =>
                string.Equals(taskStatus, "Rejected", StringComparison.Ordinal)
                && string.Equals(instanceStatus, "Rejected", StringComparison.Ordinal),
            _ => false
        };

    /// <summary>
    /// The start was refused BEFORE the engine processed anything (a readable invalid-request answer, or forbidden):
    /// nothing exists there under the key — the only start answer that lets a submit end without asking again.
    /// </summary>
    public static bool RefusedBeforeProcessing(ProductIdentityWorkflowTransportOutcome outcome, string? code) =>
        outcome == ProductIdentityWorkflowTransportOutcome.Invalid
            && code is not (ProductIdentityWorkflowFaultPolicy.ResponseUnreadable or ProductIdentityWorkflowFaultPolicy.ResponseInvalid)
        || outcome == ProductIdentityWorkflowTransportOutcome.Forbidden;

    /// <summary>A code read from elsewhere is kept only when it is a bounded single line; otherwise the fallback.</summary>
    public static string BoundedCode(string? code, string fallback) =>
        !string.IsNullOrWhiteSpace(code) && code.Length <= 128 && code.All(character => !char.IsControl(character))
            ? code
            : fallback;
}
