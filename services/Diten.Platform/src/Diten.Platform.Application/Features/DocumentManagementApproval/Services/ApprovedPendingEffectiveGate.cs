using Diten.Platform.Application.Features.DocumentManagementLifecycle;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.DocumentManagementApproval.Services;

/// <summary>
/// MOD-0029-FU09 — adapter implementing the FU08 <see cref="IApprovedPendingEffectiveGate"/> port. When the approval
/// It blocks InReview → ApprovedPendingEffective until every mandatory approval requirement is Completed and
/// segregation passes. The gate is non-waivable; the legacy option is accepted only for configuration compatibility.
/// </summary>
public sealed class ApprovedPendingEffectiveGate : IApprovedPendingEffectiveGate
{
    private readonly IDocumentApprovalRequirementRepository _requirements;
    private readonly DocumentSegregationRuleEvaluator _segregation;
    public ApprovedPendingEffectiveGate(
        IDocumentApprovalRequirementRepository requirements,
        DocumentSegregationRuleEvaluator segregation,
        IOptions<DocumentApprovalOptions> options)
    {
        _requirements = requirements;
        _segregation = segregation;
        _ = options;
    }

    public async Task<string?> EvaluateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct)
    {
        var requirements = await _requirements.GetByRegisterEntryAsync(entry.Id, ct);
        if (requirements.Count == 0)
        {
            return "Approval route has not been resolved; the document cannot enter Approved-pending-effective.";
        }

        if (requirements.Any(r => r.Status == ApprovalRequirementStatus.Rejected))
        {
            return "An approval was rejected; the document cannot enter Approved-pending-effective.";
        }

        if (requirements.Where(r => r.IsMandatory).Any(r => r.Status != ApprovalRequirementStatus.Completed))
        {
            return "Mandatory approvals are incomplete; the document cannot enter Approved-pending-effective.";
        }

        var failures = _segregation.Evaluate(entry, requirements);
        return failures.Count > 0 ? failures[0] : null;
    }
}
