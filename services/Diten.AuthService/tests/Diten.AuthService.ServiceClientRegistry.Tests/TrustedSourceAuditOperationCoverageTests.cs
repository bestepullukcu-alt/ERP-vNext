using Diten.MdmService.Domain.Enums;
using Diten.Platform.Application.Features.Audit;
using Xunit;

namespace Diten.AuthService.ServiceClientRegistry.Tests;

/// <summary>
/// WP-GP-AUDIT-TRAIL-01 D1 — every audit operation MDM can PRODUCE reaches the central log, and the two it has
/// reserved are refused on both sides.
///
/// <para>MDM writes an audit intent for each product operation and sends it to Platform; Platform accepts only the
/// operations its map knows and answers the rest MAPPING_UNSUPPORTED, so an unmapped operation is an audit row that
/// never arrives — silently, from MDM's side. This project references BOTH services, which is why the guard lives
/// here: adding an operation to MDM without its Platform mapping turns this red.</para>
///
/// <para><b>Two operations are reserved, not produced.</b> <c>GlobalProductCorrectionCancelled</c> (43) and
/// <c>GlobalProductRetirementCancelled</c> (47) are in MDM's enum but its runtime refuses to create them (the correction
/// factory throws, the repository answers AUDIT_INTENT_CONTRACT_INVALID, the delivery preflight rejects them —
/// <c>GlobalProductCorrectionUnitTests</c>, <c>GlobalProductLifecycleMongoTests</c>,
/// <c>SelectedAuditIntentDeliveryMongoTests</c>), and Platform pins them as not accepted
/// (<c>TrustedSourceAuditIntentContractTests</c>). The guard holds that agreement too: mapping one on Platform while
/// MDM still cannot produce it is a decision, and this list is where it is made.</para>
/// </summary>
public sealed class TrustedSourceAuditOperationCoverageTests
{
    /// <summary>In MDM's enum, deliberately never produced, and refused by Platform.</summary>
    private static readonly string[] ReservedByMdm =
    [
        nameof(ProductAuditOperation.GlobalProductCorrectionCancelled),
        nameof(ProductAuditOperation.GlobalProductRetirementCancelled)
    ];

    [Fact]
    public void Every_product_audit_operation_MDM_can_produce_is_mapped_by_Platform()
    {
        var unmapped = Enum.GetNames<ProductAuditOperation>()
            .Except(ReservedByMdm, StringComparer.Ordinal)
            .Where(name => !TrustedSourceAuditIntentOperationMap.MappedOperationNames.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(unmapped.Count == 0,
            "MDM records these audit operations but Platform has no mapping for them, so their intents are refused "
            + "and never reach the central log: " + string.Join(", ", unmapped)
            + ". Add each to TrustedSourceAuditIntentOperationMap.");
    }

    [Fact]
    public void The_operations_MDM_reserves_are_refused_by_Platform_too()
    {
        var mappedButReserved = ReservedByMdm
            .Where(TrustedSourceAuditIntentOperationMap.MappedOperationNames.Contains)
            .ToList();

        Assert.True(mappedButReserved.Count == 0,
            "Platform maps operations MDM has reserved and never produces: " + string.Join(", ", mappedButReserved)
            + ". Either MDM starts producing them (then remove them from ReservedByMdm) or Platform stops mapping them.");
        Assert.All(ReservedByMdm, name => Assert.True(Enum.IsDefined(typeof(ProductAuditOperation), Enum.Parse<ProductAuditOperation>(name))));
    }

    // LSKU FIX2 (CT sabotage L15): a name in the map is not enough — an operation moved to another aggregate is still
    // "mapped" by name and still refused at delivery. Every LSKU operation MDM can produce maps on the LSKU aggregate
    // and every GSKU child-admission operation on the GSKU aggregate (the pair MDM's intent carries).
    [Fact]
    public void Every_lsku_and_gsku_admission_operation_maps_on_its_own_aggregate()
    {
        var produced = Enum.GetNames<ProductAuditOperation>().Except(ReservedByMdm, StringComparer.Ordinal).ToList();
        var pairs = produced.Where(name => name.StartsWith("Lsku", StringComparison.Ordinal)).Select(name => ("Lsku", name))
            .Concat(produced.Where(name => name.StartsWith("GskuChildCreation", StringComparison.Ordinal)).Select(name => ("Gsku", name)))
            .ToList();
        var unmapped = pairs.Where(pair => !TrustedSourceAuditIntentOperationMap.TryMap(pair.Item1, pair.Item2, out var entity, out _)
                || entity != pair.Item1)
            .Select(pair => $"{pair.Item1}/{pair.Item2}").ToList();

        Assert.True(pairs.Count >= 13, $"only {pairs.Count} LSKU / GSKU-admission operations found");
        Assert.True(unmapped.Count == 0, "Not mapped on their own aggregate: " + string.Join(", ", unmapped));
    }

    [Fact]
    public void The_guard_is_not_vacuous()
    {
        Assert.True(Enum.GetNames<ProductAuditOperation>().Length > 50);
        Assert.True(TrustedSourceAuditIntentOperationMap.MappedOperationNames.Count > 50);
    }
}
