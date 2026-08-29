using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementTraining.Services;

/// <summary>
/// MOD-0029-FU11 — pure training matrix resolver (GMG-QMS-SOP-0001 §7.3, §17). Given a register entry (its
/// criticality/class/impact flags) it computes the deterministic set of role-to-document training requirements.
/// Duplicates merge by <c>Audience:Role:TrainingType</c>, keeping the stricter flags. No I/O; the service reconciles
/// idempotently. This is a matrix FOUNDATION, not an LMS — user-level assignment and HCM/LMS wiring are later work.
/// </summary>
public sealed class DocumentTrainingMatrixResolver
{
    public sealed record Spec(
        TrainingAudienceType Audience, ApprovalRequiredRole? Role, DocumentTrainingType TrainingType,
        bool CriticalProcessUser, bool EffectivenessCheck, bool Acknowledgement, bool MandatoryBeforeEffective, TrainingSourceRule Source)
    {
        public string Key => $"{Audience}:{Role?.ToString() ?? "-"}:{TrainingType}";
    }

    // Roles that independently execute / decide / approve under a Critical document (SOP §17 competency audience).
    private static readonly ApprovalRequiredRole[] CriticalExecutorRoles =
    [
        ApprovalRequiredRole.GQD, ApprovalRequiredRole.QADocumentation, ApprovalRequiredRole.DocumentOwner, ApprovalRequiredRole.LocalQA
    ];

    public IReadOnlyList<Spec> Resolve(DocumentMasterRegisterEntry e)
    {
        var specs = new List<Spec>();

        switch (e.Criticality)
        {
            case DocumentCriticality.Critical:
                // Full SOP competency + effectiveness for executor/decider/approver roles (critical-process users).
                foreach (var role in CriticalExecutorRoles)
                {
                    specs.Add(new(TrainingAudienceType.Role, role, DocumentTrainingType.FullSopCompetencyAssessment,
                        CriticalProcessUser: true, EffectivenessCheck: true, Acknowledgement: false, MandatoryBeforeEffective: true, TrainingSourceRule.Criticality));
                }
                break;
            case DocumentCriticality.Major:
                // Read-and-understand for the owning/local roles; mandatory before effective (SOP §7.1 training).
                specs.Add(new(TrainingAudienceType.Role, ApprovalRequiredRole.DocumentOwner, DocumentTrainingType.ReadAndUnderstand,
                    false, false, Acknowledgement: true, MandatoryBeforeEffective: true, TrainingSourceRule.Criticality));
                specs.Add(new(TrainingAudienceType.Role, ApprovalRequiredRole.LocalQA, DocumentTrainingType.ReadAndUnderstand,
                    false, false, Acknowledgement: true, MandatoryBeforeEffective: true, TrainingSourceRule.Criticality));
                break;
            case DocumentCriticality.Minor:
                // Notification / read-and-understand where behaviour changes — not mandatory before effective.
                specs.Add(new(TrainingAudienceType.Role, ApprovalRequiredRole.DocumentOwner, DocumentTrainingType.ReadAndUnderstand,
                    false, false, Acknowledgement: true, MandatoryBeforeEffective: false, TrainingSourceRule.Criticality));
                break;
            case DocumentCriticality.UrgentTemporary:
                specs.Add(new(TrainingAudienceType.Role, ApprovalRequiredRole.LocalQA, DocumentTrainingType.ReadAndUnderstand,
                    false, false, Acknowledgement: true, MandatoryBeforeEffective: true, TrainingSourceRule.Criticality));
                break;
        }

        // Impact overlays (SOP §7.1 reviewers): scenario assessment for the specialist role.
        if (e.HasRaImpact)
            specs.Add(Scenario(ApprovalRequiredRole.GRA));
        if (e.HasPvImpact)
            specs.Add(Scenario(ApprovalRequiredRole.QPPV));
        if (e.HasBatchReleaseImpact)
            specs.Add(Scenario(ApprovalRequiredRole.QP));
        if (e.HasDmsCsvImpact)
            specs.Add(Scenario(ApprovalRequiredRole.ITCSVOwner));

        return specs
            .GroupBy(s => s.Key)
            .Select(g => g.Aggregate((a, b) => a with
            {
                CriticalProcessUser = a.CriticalProcessUser || b.CriticalProcessUser,
                EffectivenessCheck = a.EffectivenessCheck || b.EffectivenessCheck,
                Acknowledgement = a.Acknowledgement || b.Acknowledgement,
                MandatoryBeforeEffective = a.MandatoryBeforeEffective || b.MandatoryBeforeEffective
            }))
            .ToList();
    }

    private static Spec Scenario(ApprovalRequiredRole role) => new(
        TrainingAudienceType.Role, role, DocumentTrainingType.ScenarioAssessment,
        CriticalProcessUser: false, EffectivenessCheck: true, Acknowledgement: false, MandatoryBeforeEffective: true, TrainingSourceRule.ImpactAssessment);
}
