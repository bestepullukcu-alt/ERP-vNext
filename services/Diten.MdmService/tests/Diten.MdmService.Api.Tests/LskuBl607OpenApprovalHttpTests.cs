using System.Net;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// BL-607 (WP-MDM-BL607-01) — an LSKU identity approval that a person has not decided yet is OPEN, not failing. Through the
/// real Api, the real recovery runner and the real store (MdmHttpHost, its own mongod), with the fake engine holding the
/// approval open:
/// <list type="bullet">
/// <item>35 "not decided yet" answers in a row (more than the 30-attempt limit): the operation is not stuck, the worker's
/// streak never grows, and the next look is never further than the backoff ceiling;</item>
/// <item>the next look grows with how long the approval has been open (the configured delay at first — 30 s by default, 1 s on this host — the 15-min ceiling for an old one) —
/// never a flat 30-second poll;</item>
/// <item>the decision, when it comes, is applied as before;</item>
/// <item>a REAL outage of the engine still counts and reaches RETRY_LIMIT_REACHED as before;</item>
/// <item>the oldest-open-approval gauge is recorded by the runner (age from the moment the engine opened it) with its alert
/// threshold.</item>
/// </list>
/// </summary>
public sealed partial class LskuFix3HttpTests
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1); // MdmHttpHost: LskuIdentityWorkflowWorker__RetryDelaySeconds = 1
    private static readonly TimeSpan Ceiling = ProductIdentityOpenApprovalBackoff.Ceiling(RetryDelay);

    private Task BackdateApprovalOpenedAsync(Guid lsku, TimeSpan openFor) =>
        host.Database.GetCollection<LskuIdentityWorkflowOperation>("mdm_lsku_identity_workflow_operations").UpdateOneAsync(
            x => x.TenantId == _tenant && x.LskuId == lsku,
            Builders<LskuIdentityWorkflowOperation>.Update.Set(x => x.WorkflowStartedAtUtcTicksV1,
                DateTimeOffset.UtcNow.Subtract(openFor).UtcTicks));

    private async Task<(Guid Lsku, int Version)> SubmittedLskuWaitingForAPersonAsync()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "AM");
        var version = (await LskuAsync(lsku)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(lsku, version)).StatusCode);
        for (var cycle = 0; cycle < 10 && (await IdentityOperationAsync(lsku, version)).Checkpoint != LskuIdentityWorkflowCheckpoint.AwaitingDecision; cycle++)
        {
            await DueNowAsync(lsku);
            await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
        }

        var operation = await IdentityOperationAsync(lsku, version);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AwaitingDecision, operation.Checkpoint);
        Assert.NotNull(operation.WorkflowStartedAtUtcTicksV1);
        return (lsku, version);
    }

    [Fact]
    public async Task Bl607_Thirty_five_not_decided_yet_answers_leave_the_approval_waiting_not_stuck_and_never_past_the_ceiling()
    {
        var (lsku, version) = await SubmittedLskuWaitingForAPersonAsync();

        for (var cycle = 0; cycle < 35; cycle++)
        {
            await DueNowAsync(lsku);
            var before = DateTimeOffset.UtcNow;
            await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
            var operation = await IdentityOperationAsync(lsku, version);

            Assert.Equal(LskuIdentityWorkflowCheckpoint.AwaitingDecision, operation.Checkpoint);
            Assert.Equal(0, operation.FailedAttemptCount);                                   // the limit is never approached
            Assert.NotEqual(LskuIdentityWorkflowProcessor.RetryLimitReached, operation.LastFailureCode);
            Assert.Null(operation.RetryLimitCauseCode);
            var nextLook = TimeSpan.FromTicks(operation.NextAttemptAtUtcTicksV1!.Value - before.UtcTicks);
            Assert.True(nextLook <= Ceiling + TimeSpan.FromSeconds(5), $"cycle {cycle}: next look in {nextLook}, past the ceiling {Ceiling}");
        }

        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await LskuAsync(lsku)).LifecycleStatus);
    }

    [Fact]
    public async Task Bl607_The_next_look_grows_with_how_long_the_approval_has_been_open_up_to_the_ceiling()
    {
        var (lsku, version) = await SubmittedLskuWaitingForAPersonAsync();

        // Just opened: the configured delay (never a shorter look).
        await BackdateApprovalOpenedAsync(lsku, TimeSpan.Zero);
        await DueNowAsync(lsku);
        var before = DateTimeOffset.UtcNow;
        await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
        var fresh = TimeSpan.FromTicks((await IdentityOperationAsync(lsku, version)).NextAttemptAtUtcTicksV1!.Value - before.UtcTicks);
        Assert.InRange(fresh, RetryDelay - TimeSpan.FromSeconds(1), RetryDelay + TimeSpan.FromSeconds(5));

        // Open for 4 minutes: the next look is ~4 minutes away (twice the previous gap, roughly) — not 30 s.
        await BackdateApprovalOpenedAsync(lsku, TimeSpan.FromMinutes(4));
        await DueNowAsync(lsku);
        before = DateTimeOffset.UtcNow;
        await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
        var middle = TimeSpan.FromTicks((await IdentityOperationAsync(lsku, version)).NextAttemptAtUtcTicksV1!.Value - before.UtcTicks);
        Assert.InRange(middle, TimeSpan.FromMinutes(4) - TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(10));

        // Open for 3 hours: the ceiling, not 3 hours.
        await BackdateApprovalOpenedAsync(lsku, TimeSpan.FromHours(3));
        await DueNowAsync(lsku);
        before = DateTimeOffset.UtcNow;
        await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
        var old = TimeSpan.FromTicks((await IdentityOperationAsync(lsku, version)).NextAttemptAtUtcTicksV1!.Value - before.UtcTicks);
        Assert.InRange(old, Ceiling - TimeSpan.FromSeconds(1), Ceiling + TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Bl607_A_decision_after_a_long_wait_is_applied_as_before()
    {
        var (lsku, version) = await SubmittedLskuWaitingForAPersonAsync();
        for (var cycle = 0; cycle < 32; cycle++)
        {
            await DueNowAsync(lsku);
            await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
        }

        host.Engine.Approve(host.Engine.Open("lsku", lsku.ToString("D")), host.Approver);
        LskuIdentityWorkflowOperation operation = await IdentityOperationAsync(lsku, version);
        for (var cycle = 0; cycle < 10 && operation.Checkpoint != LskuIdentityWorkflowCheckpoint.Completed; cycle++)
        {
            await DueNowAsync(lsku);
            await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
            operation = await IdentityOperationAsync(lsku, version);
        }

        Assert.Equal(LskuIdentityWorkflowCheckpoint.Completed, operation.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await LskuAsync(lsku)).LifecycleStatus);
    }

    [Fact]
    public async Task Bl607_A_real_outage_of_the_engine_still_counts_and_reaches_the_limit()
    {
        var (lsku, version) = await SubmittedLskuWaitingForAPersonAsync();
        host.Engine.TerminalEvidenceFailure = (ProductIdentityWorkflowTransportOutcome.Retryable, "WORKFLOW_UNAVAILABLE");
        try
        {
            LskuIdentityWorkflowOperation operation = await IdentityOperationAsync(lsku, version);
            for (var cycle = 0; cycle < 45 && operation.LastFailureCode != LskuIdentityWorkflowProcessor.RetryLimitReached; cycle++)
            {
                await DueNowAsync(lsku);
                await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
                operation = await IdentityOperationAsync(lsku, version);
            }

            Assert.Equal(LskuIdentityWorkflowProcessor.RetryLimitReached, operation.LastFailureCode);
            Assert.True(operation.FailedAttemptCount >= ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts);
            Assert.Equal(LskuIdentityWorkflowCheckpoint.AwaitingDecision, operation.Checkpoint);
        }
        finally
        {
            host.Engine.TerminalEvidenceFailure = null;
        }
    }

    [Fact]
    public async Task Bl607_The_runner_records_the_oldest_open_approval_age_from_when_the_engine_opened_it_and_the_threshold()
    {
        var (lsku, _) = await SubmittedLskuWaitingForAPersonAsync();
        // Older than any other open approval a test of this collection may leave behind.
        await BackdateApprovalOpenedAsync(lsku, TimeSpan.FromDays(400));
        await DueNowAsync(lsku);

        await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());

        var metrics = host.Factory.Services.GetRequiredService<ProductIdentityRecoveryMetrics>();
        var age = metrics.OldestOpenApprovalSeconds(LskuIdentityWorkflowRecoveryRunner.Flow);
        Assert.NotNull(age);
        Assert.True(age >= TimeSpan.FromDays(400).TotalSeconds - 60, $"oldest open approval age {age} s");
        Assert.Equal(TimeSpan.FromMinutes(1440).TotalSeconds, metrics.OpenApprovalAlertAfterSeconds(LskuIdentityWorkflowRecoveryRunner.Flow));
    }
}
