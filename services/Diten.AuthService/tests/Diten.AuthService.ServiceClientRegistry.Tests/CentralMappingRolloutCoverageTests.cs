extern alias MdmApplication;

using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.Platform.Application.Features.Audit;
using Xunit;
using CentralMappingRollout = MdmApplication::Diten.MdmService.Application.Features.ProductItemSkuMaster.Audit.CentralMappingRollout;
using GlobalProductCorrectionAuditIntentFactory = MdmApplication::Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.GlobalProductCorrectionAuditIntentFactory;
using GlobalProductRetirementRequestAuditIntentFactory = MdmApplication::Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.GlobalProductRetirementRequestAuditIntentFactory;
using ProductIdentityLifecycleAuditIntentFactory = MdmApplication::Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.ProductIdentityLifecycleAuditIntentFactory;

namespace Diten.AuthService.ServiceClientRegistry.Tests;

/// <summary>
/// WP-MDM-SETTLE-CAP-01 (BL-566, second half; FIX1) — MDM's audit delivery waits for Platform's map only for the
/// operations the DEPLOYED Platform does not map yet (<c>CentralMappingRollout</c>); for the rest a 409
/// <c>AUDIT_SOURCE_INTENT_MAPPING_UNSUPPORTED</c> is final and the intent is a dead letter — an audit row lost whenever
/// MDM is deployed first. Measured against Platform's PRODUCTION map (this project references both services) and MDM's
/// production set; the deployed base is DATA here, by name — not derived from the same cut it checks.
/// </summary>
public sealed class CentralMappingRolloutCoverageTests
{
    /// <summary>
    /// The MDM operation NAMES the Platform live today maps. EMPTY (CT decision, SETTLE-CAP FIX1): no product module is
    /// live and Platform's trusted-source map is not on origin/main at all. Control Tower refreshes it with the cut
    /// (BL-575 item 2) — and MDM's own base (CentralMappingRollout.MappedByDeployedPlatform) must say the same.
    /// </summary>
    private static readonly string[] DeployedBase = [];

    private static IReadOnlySet<string> Waiting { get; } = CentralMappingRollout.AwaitingCentralMapping
        .Select(operation => operation.ToString())
        .ToHashSet(StringComparer.Ordinal);

    private static IReadOnlySet<string> MdmOperations { get; } = Enum.GetNames<ProductAuditOperation>().ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void MDMs_deployed_base_is_the_measured_one()
    {
        Assert.Equal(DeployedBase.Order(StringComparer.Ordinal),
            CentralMappingRollout.MappedByDeployedPlatform.Order(StringComparer.Ordinal));
    }

    // (a) Every operation Platform maps that the deployed Platform does not, MDM lets wait — and exactly those: a wait
    // set wider than Platform's map (an operation Platform dropped) or narrower (a cut moved) is red.
    [Fact]
    public void The_operations_that_wait_are_exactly_those_Platform_maps_beyond_the_deployed_base()
    {
        var newlyMapped = TrustedSourceAuditIntentOperationMap.MappedOperationNames
            .Where(MdmOperations.Contains)
            .Where(name => !DeployedBase.Contains(name, StringComparer.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        var notWaiting = newlyMapped.Except(Waiting).Order(StringComparer.Ordinal).ToList();
        var waitingForNothing = Waiting.Except(newlyMapped).Order(StringComparer.Ordinal).ToList();
        Assert.True(notWaiting.Count == 0,
            "Platform maps these, the deployed Platform does not, yet MDM would dead-letter them if deployed first: "
            + string.Join(", ", notWaiting));
        Assert.True(waitingForNothing.Count == 0,
            "MDM waits for Platform to map these, but Platform's map does not hold them: " + string.Join(", ", waitingForNothing));
    }

    // (b) A name in the map is not enough (FIX1 M4): Platform maps by (aggregate, operation), and a pair it does not know
    // is MAPPING_UNSUPPORTED too. GP 79–81, built by MDM's OWN intent factories, map on the aggregate those factories set.
    [Fact]
    public void The_GP_operations_that_wait_map_on_the_aggregate_MDMs_factories_write()
    {
        var product = new GlobalProduct
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), CanonicalCode = "GP-000000000001",
            GlobalProductName = "Product", GlobalProductNameNormalized = "PRODUCT"
        };
        var actor = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        LocalAuditIntent[] intents =
        [
            ProductIdentityLifecycleAuditIntentFactory.CreateDecisionNotApplicable(product, 1, Guid.NewGuid(), Guid.NewGuid(),
                Guid.NewGuid(), actor, "CLOSED", now),
            GlobalProductCorrectionAuditIntentFactory.Create(product, 0, Guid.NewGuid(), actor,
                ProductAuditOperation.GlobalProductCorrectionEnded, "Proposed", now, "WHY"),
            GlobalProductRetirementRequestAuditIntentFactory.Create(product, 0, Guid.NewGuid(), actor,
                ProductAuditOperation.GlobalProductRetirementEnded, "Reason", now, "WHY")
        ];

        Assert.Equal(
            [ProductAuditOperation.GlobalProductIdentityDecisionNotApplicable, ProductAuditOperation.GlobalProductCorrectionEnded,
                ProductAuditOperation.GlobalProductRetirementEnded],
            intents.Select(intent => intent.Operation));
        foreach (var intent in intents)
        {
            Assert.Contains(intent.Operation.ToString(), Waiting);
            Assert.True(TrustedSourceAuditIntentOperationMap.TryMap(intent.AggregateType.ToString(), intent.Operation.ToString(),
                    out var entity, out _),
                $"Platform does not map {intent.AggregateType}/{intent.Operation} — the pair MDM's factory writes.");
            Assert.Equal(intent.AggregateType.ToString(), entity);
        }
    }

    // This release's own additions, by name, are among what waits (GP flow 3 and LSKU FIX1); the never-produced pair is not.
    [Fact]
    public void This_releases_additions_wait_and_the_never_produced_pair_does_not()
    {
        string[] additions =
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

        Assert.All(additions, name => Assert.Contains(name, Waiting));
        Assert.DoesNotContain(nameof(ProductAuditOperation.GlobalProductCorrectionCancelled), Waiting);
        Assert.DoesNotContain(nameof(ProductAuditOperation.GlobalProductRetirementCancelled), Waiting);
    }

    [Fact]
    public void The_guard_is_not_vacuous()
    {
        Assert.True(Waiting.Count > 50);
        Assert.True(TrustedSourceAuditIntentOperationMap.MappedOperationNames.Count(MdmOperations.Contains) > 50);
    }
}
