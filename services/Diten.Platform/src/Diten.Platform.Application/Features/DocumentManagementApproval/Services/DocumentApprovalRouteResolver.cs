using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementApproval.Services;

/// <summary>
/// MOD-0029-FU09 — pure approval route resolver (GMG-QMS-SOP-0001 §6.1, §7.1, §7.2). Given a register entry (its
/// class, criticality and impact flags) it computes the deterministic set of mandatory approval/review requirements.
/// Duplicates are merged by <c>Role:Type</c> key, keeping the STRICTER flags (mandatory + non-delegable win). No I/O,
/// no persistence — the service reconciles the result idempotently.
/// </summary>
public sealed class DocumentApprovalRouteResolver
{
    /// <summary>A single desired requirement before persistence. Key dedupes across criticality/class/overlay rules.</summary>
    public sealed record Spec(ApprovalRequiredRole Role, ApprovalRequirementType Type, bool Mandatory, bool NonDelegable, ApprovalSourceRule Source)
    {
        public string Key => $"{Role}:{Type}";
    }

    public IReadOnlyList<Spec> Resolve(DocumentMasterRegisterEntry e)
    {
        var specs = new List<Spec>();

        // 1) Criticality (SOP §7.1).
        switch (e.Criticality)
        {
            case DocumentCriticality.Critical:
                specs.Add(new(ApprovalRequiredRole.GQD, ApprovalRequirementType.Approval, true, true, ApprovalSourceRule.Criticality));
                specs.Add(new(ApprovalRequiredRole.QADocumentation, ApprovalRequirementType.Review, true, false, ApprovalSourceRule.Criticality));
                break;
            case DocumentCriticality.Major:
                specs.Add(new(ApprovalRequiredRole.DocumentOwner, ApprovalRequirementType.Approval, true, false, ApprovalSourceRule.Criticality));
                specs.Add(new(ApprovalRequiredRole.QADocumentation, ApprovalRequirementType.Review, true, false, ApprovalSourceRule.Criticality));
                break;
            case DocumentCriticality.Minor:
                // Owner + a second approver; there is no self-approval at any criticality (SOP §5.1).
                specs.Add(new(ApprovalRequiredRole.DocumentOwner, ApprovalRequirementType.Approval, true, false, ApprovalSourceRule.Criticality));
                specs.Add(new(ApprovalRequiredRole.QADocumentation, ApprovalRequirementType.Approval, true, false, ApprovalSourceRule.Criticality));
                break;
            case DocumentCriticality.UrgentTemporary:
                specs.Add(new(ApprovalRequiredRole.GQD, ApprovalRequirementType.Approval, true, true, ApprovalSourceRule.Criticality));
                break;
        }

        // 2) Document class (SOP §6.1).
        switch (e.DocumentClass)
        {
            case ControlledDocumentClass.PolicyGovernance:
                specs.Add(new(ApprovalRequiredRole.CEO, ApprovalRequirementType.Endorsement, true, true, ApprovalSourceRule.DocumentClass));
                specs.Add(new(ApprovalRequiredRole.GQD, ApprovalRequirementType.Approval, true, true, ApprovalSourceRule.DocumentClass));
                break;
            case ControlledDocumentClass.ManualSystemDescription:
                specs.Add(new(ApprovalRequiredRole.GQD, ApprovalRequirementType.Approval, true, true, ApprovalSourceRule.DocumentClass));
                break;
            case ControlledDocumentClass.Sop:
                specs.Add(new(ApprovalRequiredRole.GQD, ApprovalRequirementType.Approval, true, true, ApprovalSourceRule.DocumentClass));
                break;
            case ControlledDocumentClass.WorkInstruction:
                specs.Add(new(ApprovalRequiredRole.DocumentOwner, ApprovalRequirementType.Approval, true, false, ApprovalSourceRule.DocumentClass));
                specs.Add(new(ApprovalRequiredRole.QADocumentation, ApprovalRequirementType.Review, true, false, ApprovalSourceRule.DocumentClass));
                break;
            case ControlledDocumentClass.FormTemplateRegisterMatrixPlanChecklist:
                specs.Add(new(ApprovalRequiredRole.QADocumentation, ApprovalRequirementType.Approval, true, false, ApprovalSourceRule.DocumentClass));
                specs.Add(new(ApprovalRequiredRole.DocumentOwner, ApprovalRequirementType.Approval, true, false, ApprovalSourceRule.DocumentClass));
                break;
            case ControlledDocumentClass.QualityTechnicalAgreementSdea:
                specs.Add(new(ApprovalRequiredRole.GQD, ApprovalRequirementType.Approval, true, true, ApprovalSourceRule.AgreementOverlay));
                specs.Add(new(ApprovalRequiredRole.Legal, ApprovalRequirementType.LegalReview, true, false, ApprovalSourceRule.AgreementOverlay));
                break;
            case ControlledDocumentClass.UrgentTemporaryInstruction:
                specs.Add(new(ApprovalRequiredRole.GQD, ApprovalRequirementType.Approval, true, true, ApprovalSourceRule.DocumentClass));
                break;
        }

        // 3) Overlays (SOP §7.2) — a stricter, non-displaceable role right.
        if (e.HasRaImpact)
        {
            specs.Add(new(ApprovalRequiredRole.GRA, ApprovalRequirementType.Approval, true, true, ApprovalSourceRule.RegulatoryOverlay));
            specs.Add(new(ApprovalRequiredRole.GQD, ApprovalRequirementType.QualityConcurrence, true, true, ApprovalSourceRule.RegulatoryOverlay));
        }
        if (e.HasPvImpact)
        {
            specs.Add(new(ApprovalRequiredRole.QPPV, ApprovalRequirementType.Approval, true, true, ApprovalSourceRule.PVOverlay));
            specs.Add(new(ApprovalRequiredRole.GQD, ApprovalRequirementType.QualityConcurrence, true, true, ApprovalSourceRule.PVOverlay));
        }
        if (e.HasBatchReleaseImpact)
        {
            specs.Add(new(ApprovalRequiredRole.QP, ApprovalRequirementType.Approval, true, true, ApprovalSourceRule.BatchReleaseOverlay));
        }
        if (e.HasDmsCsvImpact)
        {
            specs.Add(new(ApprovalRequiredRole.GQD, ApprovalRequirementType.Approval, true, true, ApprovalSourceRule.DmsCsvOverlay));
            specs.Add(new(ApprovalRequiredRole.ITCSVOwner, ApprovalRequirementType.TechnicalReview, true, false, ApprovalSourceRule.DmsCsvOverlay));
        }
        if (e.IsGroupGovernance || e.RequiresCeoEndorsement)
        {
            specs.Add(new(ApprovalRequiredRole.CEO, ApprovalRequirementType.Endorsement, true, true, ApprovalSourceRule.GroupGovernanceOverlay));
            specs.Add(new(ApprovalRequiredRole.GQD, ApprovalRequirementType.Approval, true, true, ApprovalSourceRule.GroupGovernanceOverlay));
        }
        if (e.HasQualityAgreementImpact || e.RequiresLegalReview)
        {
            specs.Add(new(ApprovalRequiredRole.Legal, ApprovalRequirementType.LegalReview, true, false, ApprovalSourceRule.AgreementOverlay));
        }

        // 4) Segregation-driven overlay (SOP §5.1): a Critical document authored by its process owner needs an
        //    independent qualified senior QA technical review.
        if (e.Criticality == DocumentCriticality.Critical
            && e.AuthorUserId is { } author && e.ProcessOwnerUserId == author && author != Guid.Empty)
        {
            specs.Add(new(ApprovalRequiredRole.IndependentQASenior, ApprovalRequirementType.TechnicalReview, true, true, ApprovalSourceRule.SegregationOverlay));
        }

        if (e.RequiresIndependentTechnicalReview)
        {
            specs.Add(new(ApprovalRequiredRole.IndependentQASenior, ApprovalRequirementType.TechnicalReview, true, true, ApprovalSourceRule.SegregationOverlay));
        }

        // Merge by key; stricter (mandatory / non-delegable) wins; first-seen source wins for provenance.
        return specs
            .GroupBy(s => s.Key)
            .Select(g => g.Aggregate((a, b) => a with
            {
                Mandatory = a.Mandatory || b.Mandatory,
                NonDelegable = a.NonDelegable || b.NonDelegable
            }))
            .ToList();
    }

    /// <summary>Approval-family requirements count toward the "author cannot be sole approver" rule (SOP §5.1).</summary>
    public static bool IsApprovalFamily(ApprovalRequirementType type) =>
        type is ApprovalRequirementType.Approval or ApprovalRequirementType.Endorsement;
}
