using Diten.Platform.Application.Features.DocumentManagementReleaseGates;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementControlledCopy.Services;

/// <summary>
/// MOD-0029-FU17 — adapter implementing the FU10 <see cref="ICopyReconciliationPort"/>. Computes Gate 6 from the
/// controlled-copy withdrawal readiness of the register entry:
/// <list type="bullet">
/// <item>Controlled-copy data exists and withdrawal is under control (no unaccounted required copies, plan Completed,
/// no open critical obsolete finding) → PASS.</item>
/// <item>Controlled-copy data exists but withdrawal is incomplete / an obsolete copy is in use → BLOCK.</item>
/// <item>No controlled-copy data: a Critical / release-gate-required document → BLOCK; otherwise → fall back to the
/// FU10 manual Gate 6 evidence (backward compatible).</item>
/// </list>
/// </summary>
public sealed class CopyReconciliationPortAdapter : ICopyReconciliationPort
{
    private readonly IDocumentControlledCopyRepository _copies;
    private readonly IDocumentCopyWithdrawalPlanRepository _plans;
    private readonly IDocumentObsoleteCopyFindingRepository _findings;
    private readonly DocumentControlledCopyReadinessEvaluator _readiness;

    public CopyReconciliationPortAdapter(
        IDocumentControlledCopyRepository copies,
        IDocumentCopyWithdrawalPlanRepository plans,
        IDocumentObsoleteCopyFindingRepository findings,
        DocumentControlledCopyReadinessEvaluator readiness)
    {
        _copies = copies;
        _plans = plans;
        _findings = findings;
        _readiness = readiness;
    }

    public async Task<CopyGateDecision> EvaluateGate6Async(DocumentMasterRegisterEntry entry, CancellationToken ct)
    {
        var copies = await _copies.GetByRegisterEntryAsync(entry.Id, ct);
        var open = await _plans.GetOpenAsync(entry.Id, ct);
        var findings = await _findings.GetByRegisterEntryAsync(entry.Id, ct);

        var readiness = _readiness.Evaluate(entry.Id, copies, open, findings);

        if (!readiness.HasControlledCopyData)
        {
            var mustHaveData = entry.Criticality == DocumentCriticality.Critical || entry.RequiresReleaseGateEvaluation;
            return mustHaveData
                ? new CopyGateDecision(CopyGateOutcome.Block, null,
                    "CONTROLLED_COPY_DATA_MISSING: a controlled-copy withdrawal method must be established before a Critical document can be released (SOP §19 gate 6).")
                : new CopyGateDecision(CopyGateOutcome.FallBackToManual, null, null);
        }

        return readiness.Ready
            ? new CopyGateDecision(CopyGateOutcome.Pass, "Controlled-copy withdrawal readiness satisfied", null)
            : new CopyGateDecision(CopyGateOutcome.Block, null, readiness.BlockingReasons.FirstOrDefault() ?? "Superseded copies are not withdrawn from point of use.");
    }
}
