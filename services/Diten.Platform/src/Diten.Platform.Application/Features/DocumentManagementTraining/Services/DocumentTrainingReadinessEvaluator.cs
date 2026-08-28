using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister.Services;

namespace Diten.Platform.Application.Features.DocumentManagementTraining.Services;

/// <summary>
/// MOD-0029-FU11 — pure training readiness evaluator (GMG-QMS-SOP-0001 §7.3, §9.11, §19 gate 5). Given the entry's
/// training requirements and their assignments it computes whether training is ready for effective release. Fail-
/// closed and NON-WAIVABLE: a mandatory requirement that is unassigned, incomplete, or (for a critical-process user)
/// lacks completion + a passed effectiveness check OR a formal restriction, blocks readiness. No waiver.
/// </summary>
public sealed class DocumentTrainingReadinessEvaluator
{
    public TrainingReadinessModel Evaluate(
        DocumentMasterRegisterEntry entry,
        IReadOnlyList<DocumentTrainingMatrixRequirement> requirements,
        IReadOnlyList<DocumentTrainingAssignment> assignments)
    {
        var blocking = new List<string>();
        var warnings = new List<string>();
        if (!DocumentLinkGovernanceGuard.IsGovernedRelationCompatible(entry))
        {
            blocking.Add(DocumentLinkGovernanceGuard.BlockingReason);
        }

        var mandatory = requirements.Where(r => r.MandatoryBeforeEffective).ToList();
        var missingAssignment = 0;

        foreach (var req in mandatory)
        {
            var reqAssignments = assignments.Where(a => a.RequirementId == req.Id).ToList();
            var label = $"{req.RequiredRole?.ToString() ?? req.AudienceType.ToString()} / {req.TrainingType}";

            if (reqAssignments.Count == 0)
            {
                missingAssignment++;
                blocking.Add($"Training not assigned: {label}.");
                continue;
            }

            if (req.IsCriticalProcessUserRequirement)
            {
                var restricted = reqAssignments.Any(a => a.Status == TrainingAssignmentStatus.Restricted);
                var trainedAndEffective = reqAssignments.Any(a =>
                    a.Status == TrainingAssignmentStatus.Completed && a.EffectivenessCheckStatus == TrainingEffectivenessCheckStatus.Passed);

                if (!restricted && !trainedAndEffective)
                {
                    var effectivenessPending = reqAssignments.Any(a =>
                        a.Status == TrainingAssignmentStatus.Completed && a.EffectivenessCheckStatus == TrainingEffectivenessCheckStatus.Pending);
                    blocking.Add(effectivenessPending
                        ? $"Effectiveness check pending for critical-process training: {label}."
                        : $"Critical-process training not completed/effective and not formally restricted: {label}.");
                }
            }
            else
            {
                var completed = reqAssignments.Any(a => a.Status == TrainingAssignmentStatus.Completed);
                if (!completed)
                {
                    blocking.Add($"Training not completed: {label}.");
                }
                else if (req.EffectivenessCheckRequired &&
                         !reqAssignments.Any(a => a.Status == TrainingAssignmentStatus.Completed && a.EffectivenessCheckStatus == TrainingEffectivenessCheckStatus.Passed))
                {
                    blocking.Add($"Effectiveness check not passed: {label}.");
                }
            }
        }

        var ready = mandatory.Count > 0 && blocking.Count == 0;
        if (mandatory.Count == 0)
        {
            warnings.Add("No mandatory training requirements are defined for this document.");
        }

        return new TrainingReadinessModel(
            entry.Id,
            RequiredCount: mandatory.Count,
            AssignedCount: assignments.Count(a => a.Status == TrainingAssignmentStatus.Assigned),
            CompletedCount: assignments.Count(a => a.Status == TrainingAssignmentStatus.Completed),
            RestrictedCount: assignments.Count(a => a.Status == TrainingAssignmentStatus.Restricted),
            PendingCount: assignments.Count(a => a.Status == TrainingAssignmentStatus.Assigned),
            FailedCount: assignments.Count(a => a.Status == TrainingAssignmentStatus.Failed),
            MissingAssignmentCount: missingAssignment,
            EffectivenessPendingCount: assignments.Count(a => a.EffectivenessCheckStatus == TrainingEffectivenessCheckStatus.Pending),
            Ready: ready,
            BlockingReasons: blocking,
            WarningReasons: warnings);
    }
}
