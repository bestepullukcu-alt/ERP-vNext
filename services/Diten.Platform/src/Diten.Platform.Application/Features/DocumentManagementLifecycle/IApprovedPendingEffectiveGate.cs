using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementLifecycle;

/// <summary>
/// MOD-0029-FU08 → FU09 seam. A PORT the FU08 lifecycle engine consults on the InReview → ApprovedPendingEffective
/// transition. FU08 owns the interface; FU09 (approval route/segregation) provides the adapter. Kept optional so the
/// FU08 engine has no hard dependency on the approval feature — a null gate means "no approval gating" (default,
/// backward compatible). This is NOT the FU10 non-waivable release gate; it only guards moving into
/// approved-pending-effective when the approval-required policy is switched on.
/// </summary>
public interface IApprovedPendingEffectiveGate
{
    /// <summary>Returns a human-readable block reason, or null to allow the transition.</summary>
    Task<string?> EvaluateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct);
}
