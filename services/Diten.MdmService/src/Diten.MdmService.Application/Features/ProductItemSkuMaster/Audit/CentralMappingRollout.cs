using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;

/// <summary>
/// WP-MDM-SETTLE-CAP-01 (BL-566, second half) — which audit operations may wait for Platform's map. One place, one
/// decision: the first operation whose Platform mapping ships with THIS release. Every operation from it on (79 onward:
/// GP 79–81, LSKU / GSKU 82–86, and any value added later) is new to Platform in the same deploy, so if MDM is deployed
/// first, Platform answers <c>AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED</c> for a while: such an intent waits (up to
/// <see cref="AuditIntentDeliveryProcessor.CentralMappingWaitAttempts"/>) instead of becoming a dead letter, and its
/// audit row is not lost. Every operation before it was mapped by an earlier Platform: a 409 for it is final.
///
/// <para>Derived, not listed by hand: a new operation waits without anyone remembering to add it. Once a Platform that
/// maps these operations is live everywhere, the cut moves forward to the next release's first value — and the guard
/// (<c>TrustedSourceAuditOperationCoverageTests</c>, against Platform's production map) names what moved.</para>
/// </summary>
public static class CentralMappingRollout
{
    public const ProductAuditOperation FirstOperationOfThisRelease = ProductAuditOperation.GlobalProductIdentityDecisionNotApplicable;

    public static IReadOnlySet<ProductAuditOperation> AwaitingCentralMapping { get; } =
        Enum.GetValues<ProductAuditOperation>().Where(operation => operation >= FirstOperationOfThisRelease).ToHashSet();
}
