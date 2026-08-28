using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementLifecycle;

/// <summary>
/// MOD-0029-FU08 → FU10 seam. A PORT the FU08 lifecycle engine consults on the ApprovedPendingEffective → Effective
/// transition (SOP §19/§21 non-waivable release gates). FU08 owns the interface; FU10 (release gate engine) provides
/// the adapter. Kept optional so the FU08 engine has no hard dependency on the gate feature — a null port means "no
/// release gating" (FU08 falls back to its stored-status warning behaviour, backward compatible).
///
/// The adapter EVALUATES the six gates live against the passed entry (writing evaluation/result rows and updating the
/// entry's extension fields IN MEMORY so the caller's subsequent save persists them), then returns a human-readable
/// block reason, or null when every non-waivable gate passes.
/// </summary>
public interface IReleaseGateEvaluationPort
{
    /// <summary>
    /// Returns null when the entry is exempt from hard gating (per policy/flags) or when all gates pass; otherwise a
    /// block reason. Mutates the entry's LastReleaseGate* fields in memory; does not save the entry itself.
    /// </summary>
    Task<string?> EvaluateForEffectiveAsync(DocumentMasterRegisterEntry entry, CancellationToken ct);
}
