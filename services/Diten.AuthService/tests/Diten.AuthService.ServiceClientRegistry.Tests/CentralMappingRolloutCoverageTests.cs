extern alias MdmApplication;

using Diten.MdmService.Domain.Enums;
using Diten.Platform.Application.Features.Audit;
using Xunit;
using CentralMappingRollout = MdmApplication::Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit.CentralMappingRollout;

namespace Diten.AuthService.ServiceClientRegistry.Tests;

/// <summary>
/// WP-MDM-SETTLE-CAP-01 (BL-566, second half) — MDM's audit delivery waits for Platform's map only for the operations
/// new to Platform in this release (<c>CentralMappingRollout</c>); for every other operation a 409
/// <c>AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED</c> is final and the intent is a dead letter. So an operation Platform maps
/// NEW in this release but MDM does not let wait is an audit row lost whenever MDM is deployed first. Measured against
/// Platform's production map (this project references both services), and against MDM's production set — no copy.
/// </summary>
public sealed class CentralMappingRolloutCoverageTests
{
    // What an earlier Platform already mapped: MDM's operations before this release's first value.
    private static IReadOnlySet<string> MappedBeforeThisRelease { get; } = Enum.GetValues<ProductAuditOperation>()
        .Where(operation => operation < CentralMappingRollout.FirstOperationOfThisRelease)
        .Select(operation => operation.ToString())
        .ToHashSet(StringComparer.Ordinal);

    private static IReadOnlySet<string> Waiting { get; } = CentralMappingRollout.AwaitingCentralMapping
        .Select(operation => operation.ToString())
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Every_operation_Platform_maps_new_in_this_release_waits_for_the_map()
    {
        var mdmOperations = Enum.GetNames<ProductAuditOperation>().ToHashSet(StringComparer.Ordinal);
        var newlyMapped = TrustedSourceAuditIntentOperationMap.MappedOperationNames
            .Where(mdmOperations.Contains)
            .Where(name => !MappedBeforeThisRelease.Contains(name))
            .ToList();
        var notWaiting = newlyMapped.Where(name => !Waiting.Contains(name)).OrderBy(name => name, StringComparer.Ordinal).ToList();

        Assert.True(notWaiting.Count == 0,
            "Platform maps these operations new in this release, but MDM would dead-letter them if it is deployed first: "
            + string.Join(", ", notWaiting) + ". They belong to CentralMappingRollout.AwaitingCentralMapping.");
    }

    [Fact]
    public void Every_operation_that_waits_is_one_Platform_maps()
    {
        var unmapped = Waiting.Where(name => !TrustedSourceAuditIntentOperationMap.MappedOperationNames.Contains(name)).ToList();

        Assert.True(unmapped.Count == 0,
            "MDM waits for Platform to map these, but Platform's map never will: " + string.Join(", ", unmapped));
    }

    // This release's rollout, by name: GP flow 3 (79–81) and LSKU FIX1 (82–86). When a Platform that maps them is live
    // everywhere, the cut moves forward and this list changes with it — a visible decision, not a silent one.
    [Fact]
    public void This_release_waits_for_the_GP_and_the_LSKU_GSKU_additions()
    {
        string[] expected =
        [
            nameof(ProductAuditOperation.GlobalProductIdentityDecisionNotApplicable),
            nameof(ProductAuditOperation.GlobalProductCorrectionEnded),
            nameof(ProductAuditOperation.GlobalProductRetirementEnded),
            nameof(ProductAuditOperation.GskuChildCreationAdmissionReleased),
            nameof(ProductAuditOperation.LskuDraftCancelled),
            nameof(ProductAuditOperation.LskuIdentityDecisionNotApplicable),
            nameof(ProductAuditOperation.LskuRetirementManualReconciliationRequired),
            nameof(ProductAuditOperation.LskuIdentitySubmitAbandoned)
        ];

        Assert.Equal(expected.Order(StringComparer.Ordinal), Waiting.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_guard_is_not_vacuous()
    {
        Assert.True(MappedBeforeThisRelease.Count > 50);
        Assert.True(TrustedSourceAuditIntentOperationMap.MappedOperationNames.Count(MappedBeforeThisRelease.Contains) > 50);
    }
}
