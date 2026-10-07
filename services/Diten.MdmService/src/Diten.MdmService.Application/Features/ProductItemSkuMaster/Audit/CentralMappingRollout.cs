using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit;

/// <summary>
/// WP-MDM-SETTLE-CAP-01 (BL-566, second half; FIX1) — which audit operations may wait for Platform's map. If MDM is
/// deployed before a Platform that maps an operation, Platform answers <c>AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED</c>
/// for a while: such an intent waits (up to <see cref="AuditIntentDeliveryProcessor.CentralMappingWaitAttempts"/>)
/// instead of becoming a dead letter, and its audit row is not lost.
///
/// <para><b>The base is data, by NAME.</b> <see cref="MappedByDeployedPlatform"/> holds the MDM operations the Platform
/// that is LIVE today already maps; every other operation MDM produces waits. Names, not enum numbers: a gap or a
/// renumbering in <see cref="ProductAuditOperation"/> cannot silently drop an operation out of the wait.</para>
///
/// <para><b>Today the base is EMPTY</b> (CT decision, SETTLE-CAP FIX1): no product module is live and Platform's
/// trusted-source map is not on <c>origin/main</c> at all — the live Platform maps none of these. So in this first
/// release EVERY produced product operation waits; an MDM deployed before Platform dead-letters nothing. When a Platform
/// that maps them is live everywhere, Control Tower fills the base with those names (BL-575 item 2) — and the
/// cross-service guard (<c>CentralMappingRolloutCoverageTests</c>, against Platform's production map) holds the two
/// together.</para>
/// </summary>
public static class CentralMappingRollout
{
    /// <summary>MDM operations the DEPLOYED Platform already maps (by name). Empty: nothing is mapped live yet.</summary>
    public static IReadOnlySet<string> MappedByDeployedPlatform { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// In MDM's enum, never produced (the runtime refuses to create them) and refused by Platform — nothing to wait for
    /// (named exception; <c>TrustedSourceAuditOperationCoverageTests.ReservedByMdm</c>).
    /// </summary>
    public static IReadOnlySet<ProductAuditOperation> NeverProduced { get; } = new HashSet<ProductAuditOperation>
    {
        ProductAuditOperation.GlobalProductCorrectionCancelled,
        ProductAuditOperation.GlobalProductRetirementCancelled
    };

    public static IReadOnlySet<ProductAuditOperation> AwaitingCentralMapping { get; } =
        Enum.GetValues<ProductAuditOperation>()
            .Where(operation => !NeverProduced.Contains(operation)
                                && !MappedByDeployedPlatform.Contains(operation.ToString()))
            .ToHashSet();
}
