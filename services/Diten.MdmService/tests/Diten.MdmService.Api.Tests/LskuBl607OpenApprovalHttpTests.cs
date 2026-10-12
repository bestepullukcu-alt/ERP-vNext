using System.Net;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
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
        try
        {
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
        finally
        {
            // BL-607 FIX1: the 400-day approval must not stay the oldest for every later test of this collection (the gauge
            // reads across tenants on the shared test mongod): its operation is retired here.
            await OperationsCollection.UpdateManyAsync(x => x.TenantId == _tenant && x.LskuId == lsku,
                Builders<LskuIdentityWorkflowOperation>.Update.Set(x => x.IsDeleted, true));
        }
    }

    // ------------------------------------------------------------------------------------------------- BL-607 FIX1

    private IMongoCollection<LskuIdentityWorkflowOperation> OperationsCollection =>
        host.Database.GetCollection<LskuIdentityWorkflowOperation>("mdm_lsku_identity_workflow_operations");

    /// <summary>A copy of this LSKU's real operation under a tenant of its own (no unique key is shared), never due for the
    /// worker, with its start and one changed field — a row the open-approval read must or must not see.</summary>
    private async Task<Guid> OpenApprovalCopyAsync(Guid lsku, int version, long? startedAtUtcTicks,
        Action<LskuIdentityWorkflowOperation>? change = null)
    {
        var copy = await IdentityOperationAsync(lsku, version);
        copy.Id = Guid.NewGuid();
        copy.TenantId = Guid.NewGuid();
        copy.OperationId = Guid.NewGuid();
        copy.WorkflowStartedAtUtcTicksV1 = startedAtUtcTicks;
        copy.NextAttemptAtUtcTicksV1 = DateTimeOffset.UtcNow.AddYears(50).UtcTicks;   // the worker never claims it
        change?.Invoke(copy);
        await OperationsCollection.InsertOneAsync(copy);
        return copy.TenantId;
    }

    /// <summary>FIX1 items 1, 4 and the CT survivors B5 / B6 / B7 / R2 — the read sees only the open approvals the worker
    /// keeps asking about: the oldest of them, not the newest; not a row with no start, a deleted one, a completed one, a
    /// wait at its limit (a real outage, counted as stuck elsewhere), a row of an older storage version the worker never
    /// scans, nor a corrupt (negative) start. Each decoy is OLDER than the real one, so a missing rule makes it win.</summary>
    [Fact]
    public async Task Bl607Fix1_The_open_approval_read_sees_only_what_the_worker_keeps_asking_about_and_the_oldest_of_it()
    {
        var (lsku, version) = await SubmittedLskuWaitingForAPersonAsync();
        var now = DateTimeOffset.UtcNow;
        long DaysAgo(int days) => now.AddDays(-days).UtcTicks;
        var target = DaysAgo(3000);
        var copies = new List<Guid>();
        try
        {
            copies.Add(await OpenApprovalCopyAsync(lsku, version, target));
            copies.Add(await OpenApprovalCopyAsync(lsku, version, DaysAgo(10)));                         // B5: newer
            copies.Add(await OpenApprovalCopyAsync(lsku, version, null));                                // B6: no start
            copies.Add(await OpenApprovalCopyAsync(lsku, version, DaysAgo(4000), x => x.IsDeleted = true)); // R2
            copies.Add(await OpenApprovalCopyAsync(lsku, version, DaysAgo(4001),
                x => x.Checkpoint = LskuIdentityWorkflowCheckpoint.Completed));                          // B7
            copies.Add(await OpenApprovalCopyAsync(lsku, version, DaysAgo(4002),
                x => x.LastFailureCode = LskuIdentityWorkflowProcessor.RetryLimitReached));              // item 4
            copies.Add(await OpenApprovalCopyAsync(lsku, version, DaysAgo(4003),
                x => x.TemporalStorageVersion = LskuIdentityWorkflowOperation.CurrentTemporalStorageVersion - 1)); // item 4
            copies.Add(await OpenApprovalCopyAsync(lsku, version, -100));                                // item 1: corrupt

            var oldest = await new LskuIdentityWorkflowTenantPartitionDiscoveryRepository(host.Database)
                .OldestOpenApprovalStartedAtUtcTicksAsync();

            Assert.Equal(target, oldest);

            await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
            var age = host.Factory.Services.GetRequiredService<ProductIdentityRecoveryMetrics>()
                .OldestOpenApprovalSeconds(LskuIdentityWorkflowRecoveryRunner.Flow);
            Assert.InRange(age!.Value, TimeSpan.FromDays(3000).TotalSeconds - 120, TimeSpan.FromDays(3000).TotalSeconds + 120);
        }
        finally
        {
            await OperationsCollection.UpdateManyAsync(x => copies.Contains(x.TenantId),
                Builders<LskuIdentityWorkflowOperation>.Update.Set(x => x.IsDeleted, true));
        }
    }

    /// <summary>FIX1 item 2 — the read is served by its partial index (only AwaitingDecision rows, by start), not a scan of
    /// every tenant's history: the index exists with its partial filter and the planner's winning plan uses it.</summary>
    [Fact]
    public async Task Bl607Fix1_The_open_approval_read_uses_its_partial_index()
    {
        using (var scope = TenantScope())
            _ = scope.ServiceProvider.GetRequiredService<ILskuIdentityWorkflowOperationRepository>();   // its indexes
        var collection = OperationsCollection;
        var indexes = await (await collection.Indexes.ListAsync()).ToListAsync();

        var index = Assert.Single(indexes, x => x["name"] == "ix_mdm_lsku_identity_workflow_open_approval");
        Assert.Contains("Checkpoint", index["partialFilterExpression"].AsBsonDocument.Names);

        var serializer = collection.DocumentSerializer;
        var registry = collection.Settings.SerializerRegistry;
        var explain = await host.Database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            ["explain"] = new BsonDocument
            {
                ["find"] = collection.CollectionNamespace.CollectionName,
                ["filter"] = LskuIdentityWorkflowTenantPartitionDiscoveryRepository.OpenApproval.Render(serializer, registry),
                ["sort"] = Builders<LskuIdentityWorkflowOperation>.Sort.Ascending(x => x.WorkflowStartedAtUtcTicksV1).Render(serializer, registry),
                ["limit"] = 1
            },
            ["verbosity"] = "queryPlanner"
        });
        Assert.Contains("ix_mdm_lsku_identity_workflow_open_approval", explain["queryPlanner"]["winningPlan"].ToJson());
    }

    /// <summary>FIX1 item 5 (CT survivor B1) — THE RULE ITSELF: a run of real faults raises the worker's streak; the next "not
    /// decided yet" answer ends it (0), as a success would. The stored code is the engine's own.</summary>
    [Fact]
    public async Task Bl607Fix1_A_run_of_real_faults_is_ended_by_the_next_not_decided_yet_answer()
    {
        var (lsku, version) = await SubmittedLskuWaitingForAPersonAsync();
        host.Engine.TerminalEvidenceFailure = (ProductIdentityWorkflowTransportOutcome.Retryable, "WORKFLOW_UNAVAILABLE");
        try
        {
            for (var cycle = 0; cycle < 3; cycle++)
            {
                await DueNowAsync(lsku);
                await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
            }
        }
        finally
        {
            host.Engine.TerminalEvidenceFailure = null;
        }
        var faulted = await IdentityOperationAsync(lsku, version);
        Assert.True(faulted.FailedAttemptCount >= 2, $"the faults were not counted: {faulted.FailedAttemptCount}");

        await DueNowAsync(lsku);
        await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());

        var waiting = await IdentityOperationAsync(lsku, version);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AwaitingDecision, waiting.Checkpoint);
        Assert.Equal(0, waiting.FailedAttemptCount);
        Assert.Equal("WORKFLOW_DECISION_NOT_TERMINAL", waiting.LastFailureCode);
    }

    /// <summary>FIX1 small item — an open answer without a code stores this flow's own DECISION_PENDING.</summary>
    [Fact]
    public async Task Bl607Fix1_An_open_answer_without_a_code_stores_the_flows_own_pending_code()
    {
        var (lsku, version) = await SubmittedLskuWaitingForAPersonAsync();
        host.Engine.TerminalEvidenceFailure = (ProductIdentityWorkflowTransportOutcome.NonTerminal, "");
        try
        {
            await DueNowAsync(lsku);
            await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
        }
        finally
        {
            host.Engine.TerminalEvidenceFailure = null;
        }

        Assert.Equal(LskuIdentityWorkflowProcessor.DecisionPending, (await IdentityOperationAsync(lsku, version)).LastFailureCode);
    }

    /// <summary>FIX1 item 5 — every OTHER fault of the evidence call still counts (test 4 covers Retryable): not found,
    /// incomplete, a timeout, and a refusal the engine gives this service (ServiceWait).</summary>
    [Theory]
    [InlineData(ProductIdentityWorkflowTransportOutcome.NotFound, "NOT_FOUND_NON_LEAKAGE")]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Incomplete, "WORKFLOW_RESPONSE_INCOMPLETE")]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Timeout, "WORKFLOW_TIMEOUT")]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_SOMETHING_FORBIDDEN")]
    public async Task Bl607Fix1_Every_other_fault_of_the_evidence_call_still_counts(ProductIdentityWorkflowTransportOutcome outcome, string code)
    {
        var (lsku, version) = await SubmittedLskuWaitingForAPersonAsync();
        host.Engine.TerminalEvidenceFailure = (outcome, code);
        try
        {
            await DueNowAsync(lsku);
            await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
        }
        finally
        {
            host.Engine.TerminalEvidenceFailure = null;
        }

        var operation = await IdentityOperationAsync(lsku, version);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AwaitingDecision, operation.Checkpoint);
        Assert.Equal(1, operation.FailedAttemptCount);
        Assert.Equal(code, operation.LastFailureCode);
    }

    /// <summary>FIX1 small item (CT survivor R3) — the host's own validation of the worker options refuses a threshold inside
    /// the backoff ceiling plus one poll BY NAME (the general worker validator only says "configuration invalid").</summary>
    [Fact]
    public void Bl607Fix1_The_host_refuses_a_threshold_inside_the_ceiling_by_its_own_name()
    {
        var validators = host.Factory.Services.GetServices<IValidateOptions<LskuIdentityWorkflowWorkerOptions>>().ToList();
        var bad = new LskuIdentityWorkflowWorkerOptions { Enabled = true, LeaseOwner = "bl607-fix1", OpenApprovalAlertAfterMinutes = 15 };

        var failures = validators.Select(v => v.Validate(Microsoft.Extensions.Options.Options.DefaultName, bad))
            .Where(result => result.Failed).SelectMany(result => result.Failures ?? []).ToList();

        Assert.Contains("LSKU_IDENTITY_WORKFLOW_OPEN_APPROVAL_ALERT_THRESHOLD_INVALID", failures);
        Assert.All(validators, v => Assert.False(v.Validate(Microsoft.Extensions.Options.Options.DefaultName,
            new LskuIdentityWorkflowWorkerOptions { Enabled = true, LeaseOwner = "bl607-fix1" }).Failed));
    }
}
