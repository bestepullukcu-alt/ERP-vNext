using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>WP-MDM-GSKU-ACCEPT-01 FIX3 — the rules themselves, without a store: the shared submit state (item 6), the
/// retry streak (item 1) and how a cycle is counted (item 1).</summary>
public sealed class GskuFix3RuleTests
{
    [Theory]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.Prepared, 3, false, ProductIdentitySubmitStateKind.InFlight)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted, 3, true, ProductIdentitySubmitStateKind.InFlight)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown, 3, false, ProductIdentitySubmitStateKind.InFlight)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay, 3, false, ProductIdentitySubmitStateKind.ResubmitRequired)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay, 3, true, ProductIdentitySubmitStateKind.InFlight)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, 3, false, ProductIdentitySubmitStateKind.ReconciliationRequired)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart, 3, false, ProductIdentitySubmitStateKind.Ended)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart, 2, false, ProductIdentitySubmitStateKind.None)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.Completed, 3, true, ProductIdentitySubmitStateKind.None)]
    public void A_global_product_submit_is_read_by_the_shared_rule(GlobalProductIdentityWorkflowCheckpoint checkpoint,
        int submittedAt, bool engineHolds, ProductIdentitySubmitStateKind expected)
    {
        var operation = new GlobalProductIdentityWorkflowOperation
        {
            Checkpoint = checkpoint, ExpectedProductVersion = submittedAt,
            WorkflowInstanceId = engineHolds ? Guid.NewGuid() : null
        };

        Assert.Equal(expected, ProductIdentitySubmitState.Of(operation, currentProductVersion: 3).Kind);
        Assert.Equal(ProductIdentitySubmitStateKind.None, ProductIdentitySubmitState.Of(null, 3).Kind);
    }

    // GSKU's rule is the same rule (its five kinds line up one to one with the shared ones).
    [Theory]
    [InlineData(FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted, FirstGskuIdentitySubmitStateKind.InFlight)]
    [InlineData(FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay, FirstGskuIdentitySubmitStateKind.ResubmitRequired)]
    [InlineData(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, FirstGskuIdentitySubmitStateKind.ReconciliationRequired)]
    [InlineData(FirstGskuIdentityWorkflowCheckpoint.Superseded, FirstGskuIdentitySubmitStateKind.Ended)]
    [InlineData(FirstGskuIdentityWorkflowCheckpoint.DecisionNotApplicable, FirstGskuIdentitySubmitStateKind.None)]
    public void A_gsku_submit_is_read_by_the_same_rule(FirstGskuIdentityWorkflowCheckpoint checkpoint,
        FirstGskuIdentitySubmitStateKind expected)
    {
        var operation = new FirstGskuIdentityWorkflowOperation { Checkpoint = checkpoint, ExpectedGskuVersion = 4 };

        Assert.Equal(expected, FirstGskuIdentitySubmitState.Of(operation, 4).Kind);
    }

    [Fact]
    public void The_streak_counts_doubles_its_wait_and_stops_at_the_limit()
    {
        var now = DateTimeOffset.UtcNow;
        var second = TimeSpan.FromSeconds(1);

        var first = ProductIdentityRecoveryStreak.Next(0, now, second, 3, TimeSpan.FromSeconds(10));
        var next = ProductIdentityRecoveryStreak.Next(1, now, second, 3, TimeSpan.FromSeconds(10));
        var last = ProductIdentityRecoveryStreak.Next(2, now, second, 3, TimeSpan.FromSeconds(10));
        var ceiling = ProductIdentityRecoveryStreak.Next(20, now, second, 0, TimeSpan.FromSeconds(10));

        Assert.Equal((false, 1, now.AddSeconds(1).UtcTicks), first);
        Assert.Equal((false, 2, now.AddSeconds(2).UtcTicks), next);
        Assert.True(last.Exhausted);
        Assert.Equal(3, last.Attempts);
        Assert.False(ceiling.Exhausted); // 0 = never quarantined
        Assert.Equal(now.AddSeconds(10).UtcTicks, ceiling.NextAttemptAtUtcTicks);
    }

    [Fact]
    public void A_cycle_counts_claimed_advanced_quarantined_and_waiting()
    {
        var cycle = new ProductIdentityRecoveryCycle();

        cycle.Count(claimed: true, quarantinedNow: false, advanced: true);
        cycle.Count(claimed: true, quarantinedNow: true, advanced: true);
        cycle.Count(claimed: true, quarantinedNow: false, advanced: false);
        cycle.Count(claimed: false, quarantinedNow: false, advanced: false);
        cycle.NotClaimed();

        Assert.Equal((5, 3, 1, 1, 3), (cycle.Seen, cycle.Claimed, cycle.Advanced, cycle.Quarantined, cycle.Waiting));
        Assert.True(ProductIdentityRecoveryCycle.WasNotClaimed("GSKU_CORRECTION_BUSY"));
        Assert.True(ProductIdentityRecoveryCycle.WasNotClaimed(ProductIdentityWorkflowClaims.StoreUnavailable));
        Assert.False(ProductIdentityRecoveryCycle.WasNotClaimed("GSKU_CORRECTION_RETRY_SCHEDULED"));
    }

    // The identity flows keep their own streak (FIX1, ServiceFaultCount): a service fault there waits and turns into
    // an error after the configured attempts — it is never quarantined. FIX3 pins that, it does not change it.
    [Fact]
    public void The_identity_streak_escalates_to_an_error_and_does_not_quarantine()
    {
        var settings = new ProductIdentityWorkflowServiceFaultSettings(TimeSpan.FromMinutes(15), 5);
        var now = DateTimeOffset.UtcNow;
        var operation = new FirstGskuIdentityWorkflowOperation
        {
            ServiceFaultCount = 4, ServiceFaultSinceUtcTicksV1 = now.AddMinutes(-1).UtcTicks,
            UpdatedAtUtcTicksV1 = now.AddSeconds(-30).UtcTicks
        };

        var streak = ProductIdentityWorkflowFaultRules.Streak(operation, now, countsAsWorkerAttempt: true, settings);

        Assert.Equal(5, streak.Count);
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Error,
            ProductIdentityWorkflowFaultRules.Level(false, streak.Count, streak.Since, now, settings));
        Assert.Equal(ProductIdentityWorkflowFaultKind.ServiceWait, ProductIdentityWorkflowFaultPolicy.Classify(
            ProductIdentityWorkflowCall.TerminalEvidence,
            Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowTransportOutcome.Timeout, null).Kind);
    }
}
