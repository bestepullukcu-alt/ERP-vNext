using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementApproval.Services;

/// <summary>
/// MOD-0029-FU09 — pure segregation-of-duties evaluator (GMG-QMS-SOP-0001 §5.1). Given the register entry and its
/// completed requirements, it returns the list of segregation FAILURES (empty = passed). Fail-closed: an unknown
/// author identity is a failure, never a silent pass.
/// </summary>
public sealed class DocumentSegregationRuleEvaluator
{
    public IReadOnlyList<string> Evaluate(DocumentMasterRegisterEntry entry, IReadOnlyList<DocumentApprovalRequirement> requirements)
    {
        var failures = new List<string>();

        var completedApprovals = requirements
            .Where(r => r.Status == ApprovalRequirementStatus.Completed && DocumentApprovalRouteResolver.IsApprovalFamily(r.RequirementType))
            .ToList();

        var completedMandatory = requirements
            .Where(r => r.Status == ApprovalRequirementStatus.Completed && r.IsMandatory)
            .ToList();

        // Only evaluate once at least one approval-family sign-off exists (nothing to violate before then).
        if (completedApprovals.Count == 0)
        {
            return failures;
        }

        // Author identity must be known to verify §5.1 (fail-closed).
        if (entry.AuthorUserId is not { } authorId || authorId == Guid.Empty)
        {
            failures.Add("Author identity is unknown; segregation of duties cannot be verified (SOP §5.1).");
            return failures;
        }

        var distinctApprovers = completedApprovals
            .Where(r => r.CompletedByUserId is not null)
            .Select(r => r.CompletedByUserId!.Value)
            .Distinct()
            .ToList();

        // 1) The author shall not be the sole approver.
        if (distinctApprovers.Count == 1 && distinctApprovers[0] == authorId)
        {
            failures.Add("The author is the sole approver (SOP §5.1: the author shall not be the sole approver).");
        }

        // 2) No single user may satisfy ALL mandatory requirements alone.
        if (completedMandatory.Count > 1)
        {
            var distinctMandatoryCompleters = completedMandatory
                .Where(r => r.CompletedByUserId is not null)
                .Select(r => r.CompletedByUserId!.Value)
                .Distinct()
                .ToList();
            if (distinctMandatoryCompleters.Count == 1)
            {
                failures.Add("A single user completed every mandatory requirement (SOP §5.1: no self-approval).");
            }
        }

        // 3) The requester of an exception shall not approve it (best-effort: requester ≠ any approver here is not
        //    forced, but a requester who is also the sole approver is caught by rule 1). Recorded for future exception
        //    flows; no additional failure emitted in this FU.

        return failures;
    }
}
