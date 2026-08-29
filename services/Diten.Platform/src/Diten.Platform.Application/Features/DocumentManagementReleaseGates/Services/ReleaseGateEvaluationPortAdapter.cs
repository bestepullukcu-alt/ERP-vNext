using Diten.Platform.Application.Features.DocumentManagementLifecycle;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.DocumentManagementReleaseGates.Services;

/// <summary>
/// MOD-0029-FU10 — adapter implementing the FU08 <see cref="IReleaseGateEvaluationPort"/>. On the
/// ApprovedPendingEffective → Effective transition it hard-gates a document (SOP §19: non-waivable) when the entry is
/// subject to gating — i.e. the lifecycle policy requires it, OR the entry is flagged, OR it is Critical. When gating
/// applies it evaluates all six gates live and blocks unless the evaluation is Complete. When gating does not apply
/// (e.g. a non-critical legacy entry with no flag/policy) it returns null so the transition proceeds.
/// </summary>
public sealed class ReleaseGateEvaluationPortAdapter : IReleaseGateEvaluationPort
{
    private readonly DocumentReleaseGateEvaluator _evaluator;
    private readonly DocumentLifecycleOptions _lifecycleOptions;

    public ReleaseGateEvaluationPortAdapter(DocumentReleaseGateEvaluator evaluator, IOptions<DocumentLifecycleOptions> lifecycleOptions)
    {
        _evaluator = evaluator;
        _lifecycleOptions = lifecycleOptions.Value;
    }

    public async Task<string?> EvaluateForEffectiveAsync(DocumentMasterRegisterEntry entry, CancellationToken ct)
    {
        var shouldGate = _lifecycleOptions.RequireReleaseGateForEffective
            || entry.RequiresReleaseGateEvaluation
            || entry.Criticality == DocumentCriticality.Critical;
        if (!shouldGate)
        {
            return null;
        }

        // Evaluate + persist; the entry's LastReleaseGate* fields are mutated in memory for the caller to save.
        var (evaluation, results) = await _evaluator.EvaluateCoreAsync(entry, correlationId: string.Empty, ct);
        if (evaluation.EvaluationStatus == ReleaseGateEvaluationStatus.Complete)
        {
            return null;
        }

        var blocking = results.Where(r => r.GateResult != ReleaseGateResultValue.Yes)
            .Select(r => $"Gate {r.GateNumber} ({r.GateName})")
            .ToList();
        return $"Non-waivable release gates are incomplete (SOP §19): {string.Join("; ", blocking)}.";
    }
}
