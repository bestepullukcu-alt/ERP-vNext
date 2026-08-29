using Diten.Platform.Application.Features.DocumentManagementReleaseGates;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementTraining.Services;

/// <summary>
/// MOD-0029-FU11 — adapter implementing the FU10 <see cref="ITrainingReadinessPort"/>. Computes Gate 5 from the
/// training matrix:
/// <list type="bullet">
/// <item>If a training matrix exists → Gate 5 = training readiness (Pass/Block).</item>
/// <item>If no matrix exists and the document is Critical / release-gate-required → BLOCK with TRAINING_MATRIX_MISSING
/// (a critical document must have a resolved training matrix, SOP §7.3/§19 gate 5).</item>
/// <item>Otherwise → fall back to the FU10 manual/auto Gate 5 behaviour (backward compatible for non-critical legacy).</item>
/// </list>
/// </summary>
public sealed class TrainingReadinessPortAdapter : ITrainingReadinessPort
{
    private readonly IDocumentTrainingMatrixRequirementRepository _requirements;
    private readonly IDocumentTrainingAssignmentRepository _assignments;
    private readonly DocumentTrainingReadinessEvaluator _readiness;

    public TrainingReadinessPortAdapter(
        IDocumentTrainingMatrixRequirementRepository requirements,
        IDocumentTrainingAssignmentRepository assignments,
        DocumentTrainingReadinessEvaluator readiness)
    {
        _requirements = requirements;
        _assignments = assignments;
        _readiness = readiness;
    }

    public async Task<TrainingGateDecision> EvaluateGate5Async(DocumentMasterRegisterEntry entry, CancellationToken ct)
    {
        var requirements = await _requirements.GetByRegisterEntryAsync(entry.Id, ct);

        if (requirements.Count == 0)
        {
            var mustHaveMatrix = entry.Criticality == DocumentCriticality.Critical || entry.RequiresReleaseGateEvaluation;
            return mustHaveMatrix
                ? new TrainingGateDecision(TrainingGateOutcome.Block, null,
                    "TRAINING_MATRIX_MISSING: a training matrix must be resolved before a Critical document can be released (SOP §7.3 / §19 gate 5).")
                : new TrainingGateDecision(TrainingGateOutcome.FallBackToManual, null, null);
        }

        var assignments = await _assignments.GetByRegisterEntryAsync(entry.Id, ct);
        var readiness = _readiness.Evaluate(entry, requirements, assignments);

        return readiness.Ready
            ? new TrainingGateDecision(TrainingGateOutcome.Pass,
                $"Training readiness: {readiness.CompletedCount + readiness.RestrictedCount}/{readiness.RequiredCount} satisfied", null)
            : new TrainingGateDecision(TrainingGateOutcome.Block, null,
                readiness.BlockingReasons.FirstOrDefault() ?? "Training readiness is not met.");
    }
}
