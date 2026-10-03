using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using ProductIdentityWorkflowCancellationEvidence = Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowCancellationEvidence;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using Checkpoint = Diten.MdmService.Domain.Enums.GlobalProductIdentityWorkflowCheckpoint;
using Outcome = Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowTransportOutcome;

namespace Diten.MdmService.Application.Tests;

// WP-GP-1B-A-RESILIENCE-01 on the REAL operation repository, processor and recovery runner: the lease, the
// transition table, the persisted fault fields and the cycle breaker. Only the transport is a fake.
// ITS OWN DATABASE, with a fixed suffix (DB-010): the recovery runner walks EVERY tenant of the database it is given,
// so in the shared test database another class's leftover operation was both written to and counted. Here nothing
// but this class's operations exist: the collection is emptied before each test, and each test still owns two
// fresh tenants.
// ONE RUN AT A TIME PER MONGO: the database name is fixed and InitializeAsync deletes EVERY operation in it. Two runs
// of this class against the same mongod (two worktrees, a sabotage run beside a full run) delete each other's rows.
[Collection(ProductLegalEntityScopeMongoCollection.Name)]
public sealed class GlobalProductIdentityWorkflowResilienceMongoTests : IAsyncLifetime
{
    private const string DatabaseName = ProductLegalEntityScopeMongoCollection.DatabaseName + "_gp_identity_resilience";
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Retry = TimeSpan.FromSeconds(30);
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _makerId = Guid.NewGuid();
    private readonly Guid _approverId = Guid.NewGuid();
    private readonly Guid _templateId = Guid.NewGuid();
    private readonly Guid _templateVersionId = Guid.NewGuid();
    private readonly Clock _clock = new();
    private readonly Transport _transport = new();
    private readonly Readiness _readiness = new();
    private readonly Log _log = new();
    private IMongoDatabase _database = null!;

    public async Task InitializeAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(
            Environment.GetEnvironmentVariable("MDM_TEST_MONGO")
            ?? Environment.GetEnvironmentVariable("MONGO_TEST_URI")
            ?? "mongodb://localhost:27017");
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        _database = new MongoClient(settings).GetDatabase(DatabaseName);
        await _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        await _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .DeleteManyAsync(FilterDefinition<GlobalProductIdentityWorkflowOperation>.Empty);
    }

    public async Task DisposeAsync()
    {
        foreach (var tenant in new[] { _tenantA, _tenantB })
        {
            await _database.GetCollection<GlobalProduct>("mdm_global_products")
                .DeleteManyAsync(item => item.TenantId == tenant);
            await _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                    GlobalProductIdentityWorkflowOperationRepository.CollectionName)
                .DeleteManyAsync(item => item.TenantId == tenant);
        }
    }

    // ── the lease, not the retry schedule, is what keeps two actors apart ─────────────────────────────────────────

    [Fact]
    public async Task Claim_WithAFreeLease_TheMakerPassesAFutureRetryTime_TheWorkerDoesNot()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision);
        var repository = Repository(_tenantA);
        await ScheduleAsync(repository, operation, _clock.GetUtcNow().AddMinutes(15));
        var now = _clock.GetUtcNow().UtcTicks;

        var worker = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [Checkpoint.AwaitingDecision], "worker", now, now + Lease.Ticks));
        var maker = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [Checkpoint.AwaitingDecision], "maker", now, now + Lease.Ticks, IgnoreRetrySchedule: true));
        var secondMaker = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [Checkpoint.AwaitingDecision], "maker-2", now + 1, now + Lease.Ticks, IgnoreRetrySchedule: true));

        Assert.Null(worker);
        Assert.NotNull(maker);
        Assert.Null(secondMaker); // the lease is held: ignoring the schedule never means ignoring the lease
    }

    [Fact]
    public async Task Withdraw_WhileTheWorkerHoldsTheLease_IsBusy_AndAfterwardsPassesAFutureRetryTime()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision, withPendingProduct: true);
        var repository = Repository(_tenantA);
        await ScheduleAsync(repository, operation, _clock.GetUtcNow().AddMinutes(15));
        _transport.Preflight = (_, _) => (Outcome.Retryable, "PRODUCT_WORKFLOW_UNAVAILABLE");
        using var services = Services();

        var now = _clock.GetUtcNow().UtcTicks;
        var held = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [Checkpoint.AwaitingDecision], "worker", now - TimeSpan.FromMinutes(16).Ticks, now + Lease.Ticks));
        Assert.Null(held); // the worker itself is still paced by the schedule…
        await ScheduleAsync(repository, operation, null); // …so let it in, and keep its lease
        held = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [Checkpoint.AwaitingDecision], "worker", now, now + Lease.Ticks));
        Assert.NotNull(held);

        var busy = await WithdrawAsync(services, operation, Guid.NewGuid());

        Assert.Equal(409, busy.StatusCode);
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_BUSY", busy.ErrorCode);
        Assert.Equal(Checkpoint.AwaitingDecision, (await repository.GetByOperationIdAsync(operation.OperationId))!.Checkpoint);
        Assert.Equal(0, _transport.Calls(_tenantA));

        // The worker finishes its poll: the lease is free and the next attempt is 15 minutes away.
        Assert.True(await repository.AdvanceAsync(held!, new(Checkpoint.AwaitingDecision,
            ProductIdentityWorkflowRecoveryDisposition.Retryable, now + 1,
            _clock.GetUtcNow().AddMinutes(15).UtcTicks, "PRODUCT_WORKFLOW_IDENTITY_REJECTED", ReleaseLease: true,
            ServiceFaultCount: 4, ServiceFaultSinceUtcTicksV1: now)));

        var passed = await WithdrawAsync(services, operation, Guid.NewGuid());

        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(503, passed.StatusCode);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, passed.ErrorCode);
        Assert.Equal(Checkpoint.WithdrawalRequested, stored.Checkpoint); // the claim went through
        // The requester's attempt is not the worker's: what the worker counted and when it started stay as they were.
        Assert.Equal(4, stored.ServiceFaultCount);
        Assert.Equal(now, stored.ServiceFaultSinceUtcTicksV1);
        // …and so does the worker's next attempt: the withdrawal did not bring the fifteen minutes forward.
        Assert.Equal(_clock.GetUtcNow().AddMinutes(15).UtcTicks, stored.NextAttemptAtUtcTicksV1);
        Assert.Null(stored.LeaseOwner);
    }

    // ── the transition table: a maker's resubmit has somewhere to go ──────────────────────────────────────────────

    [Fact]
    public async Task Resubmit_FromAwaitingMakerReplay_WhoseOutcomeIsUnknown_IsStored_NotRefused_AndTheFaultFieldsRoundTrip()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingMakerReplay, withDraftProduct: true);
        var repository = Repository(_tenantA);
        _transport.Start = (_, _) => (Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        _transport.StartResult = (_, _) => (Outcome.Retryable, "PRODUCT_WORKFLOW_UNAVAILABLE");
        using var services = Services();

        var result = await SubmitAsync(services, operation);

        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, result.ErrorCode);
        Assert.Equal(Checkpoint.StartOutcomeUnknown, stored.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, stored.RecoveryDisposition);
        Assert.Equal(0, stored.ServiceFaultCount); // the maker's attempt does not count
        Assert.Equal(_clock.GetUtcNow().UtcTicks, stored.ServiceFaultSinceUtcTicksV1);
        Assert.Null(stored.NextAttemptAtUtcTicksV1); // …and does not move the worker's next attempt
        Assert.Null(stored.LeaseOwner);
        Assert.Null(stored.LeaseUntilUtcTicksV1);
        var since = stored.ServiceFaultSinceUtcTicksV1;

        // The worker's lookup fails too: THAT counts, backs off, and keeps the first moment.
        _clock.Advance(TimeSpan.FromSeconds(15));
        await RecoverAsync(services, stored);
        stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(Checkpoint.StartOutcomeUnknown, stored.Checkpoint);
        Assert.Equal(1, stored.ServiceFaultCount);
        Assert.Equal(since, stored.ServiceFaultSinceUtcTicksV1);
        Assert.Equal(_clock.GetUtcNow().Add(Retry).UtcTicks, stored.NextAttemptAtUtcTicksV1);

        // The maker clicks six times while the worker is backing off: nothing the worker scheduled moves.
        var scheduled = stored.NextAttemptAtUtcTicksV1;
        for (var click = 0; click < 6; click++)
        {
            Assert.Equal(503, (await SubmitAsync(services, operation)).StatusCode);
        }
        stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(1, stored.ServiceFaultCount);
        Assert.Equal(since, stored.ServiceFaultSinceUtcTicksV1);
        Assert.Equal(scheduled, stored.NextAttemptAtUtcTicksV1);

        // The worker's lookup then learns that nothing arrived. The chain answered for this operation: the streak of
        // faults is over, and the operation is the maker's again.
        _clock.Advance(TimeSpan.FromMinutes(1));
        _transport.StartResult = (_, _) => (Outcome.NotFound, "NOT_FOUND_NON_LEAKAGE");
        await RecoverAsync(services, stored);

        stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(Checkpoint.AwaitingMakerReplay, stored.Checkpoint);
        Assert.Equal(0, stored.ServiceFaultCount);
        Assert.Null(stored.ServiceFaultSinceUtcTicksV1);

        // A fault met AFTER that answer starts a new streak at its own moment — not at the old one.
        _clock.Advance(TimeSpan.FromMinutes(20));
        _transport.StartResult = (_, _) => (Outcome.Retryable, "PRODUCT_WORKFLOW_UNAVAILABLE");
        Assert.Equal(503, (await SubmitAsync(services, operation)).StatusCode);
        stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(_clock.GetUtcNow().UtcTicks, stored.ServiceFaultSinceUtcTicksV1);
        Assert.DoesNotContain(_log.Entries.Skip(_log.Entries.Count - 1), entry => entry.Level == LogLevel.Error);
    }

    [Theory]
    [InlineData(Checkpoint.StartOutcomeUnknown, true)]
    [InlineData(Checkpoint.WorkflowStarted, true)]
    [InlineData(Checkpoint.WithdrawalObserved, true)]
    [InlineData(Checkpoint.AwaitingDecision, true)]
    [InlineData(Checkpoint.ManualReconciliationRequired, true)]
    [InlineData(Checkpoint.Completed, false)]
    [InlineData(Checkpoint.LocalPendingApplied, false)]
    public async Task AwaitingMakerReplay_MayMoveOnlyWhereAResubmitCanEnd(Checkpoint next, bool allowed)
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingMakerReplay);
        var repository = Repository(_tenantA);
        var now = _clock.GetUtcNow().UtcTicks;
        var claim = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [Checkpoint.AwaitingMakerReplay], "maker", now, now + Lease.Ticks));
        Assert.NotNull(claim);

        var advanced = await repository.AdvanceAsync(claim!, new(next,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 1, ReleaseLease: true));

        Assert.Equal(allowed, advanced);
        Assert.Equal(allowed ? next : Checkpoint.AwaitingMakerReplay,
            (await repository.GetByOperationIdAsync(operation.OperationId))!.Checkpoint);
    }

    [Fact]
    public async Task ASubmitRepeatedWhileAWithdrawalIsInFlight_IsRefused_WithoutALeaseOrACall()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.WithdrawalRequested, withPendingProduct: true);
        var repository = Repository(_tenantA);
        var before = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        using var services = Services();

        var result = await SubmitAsync(services, operation);

        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_STATE_CONFLICT", result.ErrorCode);
        Assert.Equal(0, _transport.Calls(_tenantA));
        Assert.Equal((before.Checkpoint, before.Version, before.LeaseGeneration),
            (stored.Checkpoint, stored.Version, stored.LeaseGeneration));
    }

    [Fact]
    public async Task ASubmitInterruptedInTheMiddleOfAStep_GivesTheLeaseBack_KeepsTheSchedule_AndTheNextSubmitIsNotBusy()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.StartOutcomeUnknown, withDraftProduct: true);
        var repository = Repository(_tenantA);
        var scheduled = _clock.GetUtcNow().AddMinutes(15);
        await ScheduleAsync(repository, operation, scheduled);
        _transport.Start = (_, _) => throw new OperationCanceledException("the caller went away");
        using var services = Services();

        await Assert.ThrowsAsync<OperationCanceledException>(() => SubmitAsync(services, operation));

        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(Checkpoint.StartOutcomeUnknown, stored.Checkpoint);
        Assert.Null(stored.LeaseOwner);
        Assert.Null(stored.LeaseUntilUtcTicksV1);
        Assert.Equal(scheduled.UtcTicks, stored.NextAttemptAtUtcTicksV1);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, stored.RecoveryDisposition);

        // Straight away, with the same clock: a lease left behind would answer 409 BUSY here.
        _transport.Start = (_, _) => (Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED");
        var next = await SubmitAsync(services, operation);

        Assert.Equal(503, next.StatusCode);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, next.ErrorCode);
    }

    [Fact]
    public async Task AWithdrawalInterruptedInTheMiddleOfAStep_GivesTheLeaseBack_AndTheNextOneIsNotBusy()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision, withPendingProduct: true);
        var repository = Repository(_tenantA);
        _transport.Preflight = (_, _) => throw new InvalidOperationException("the wire broke");
        using var services = Services();
        var commandId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() => WithdrawAsync(services, operation, commandId));

        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(Checkpoint.WithdrawalRequested, stored.Checkpoint);
        Assert.Null(stored.LeaseOwner);

        _transport.Preflight = (_, _) => (Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        var next = await WithdrawAsync(services, operation, commandId);

        Assert.Equal(503, next.StatusCode);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, next.ErrorCode);
    }

    // ── a withdrawal met by an outage never hides an approval from the worker (item 2) ────────────────────────────

    [Fact]
    public async Task WithdrawalRefusedDuringAnOutage_NeverRepeated_TheApproversDecisionIsObserved_AndTheProductLeavesPending()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision, withPendingProduct: true);
        var repository = Repository(_tenantA);
        _transport.Preflight = (_, request) => PreflightOpen(request);
        _transport.Cancel = (_, _) => (Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED");
        using var services = Services();

        var refused = await WithdrawAsync(services, operation, Guid.NewGuid());

        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(503, refused.StatusCode);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, refused.ErrorCode);
        Assert.Equal(Checkpoint.AwaitingDecision, stored.Checkpoint); // a state the worker reads
        Assert.Null(stored.WithdrawalCommandId);
        Assert.Null(stored.WithdrawalFingerprint);
        Assert.Null(stored.WithdrawalExpectedWorkflowInstanceVersion);
        // Refused before it was processed: nothing can land, so the worker's schedule is what it was (none) — a
        // refused withdrawal does not keep the decision poll waiting.
        Assert.Null(stored.NextAttemptAtUtcTicksV1);

        // The requester gives up. The approver approves at the engine. The worker's very next cycle — the real
        // runner, the real discovery — finds the operation and applies the decision.
        _transport.Terminal = (_, _) => Approved(stored);
        var cycle = await Runner(services).RunCycleAsync();

        stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(1, cycle.CompletedCount);
        Assert.Equal(Checkpoint.Completed, stored.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await ProductAsync(operation)).LifecycleStatus);
    }

    [Fact]
    public async Task WithdrawalWhoseOutcomeIsUnknown_NeverRepeated_TheWorkerReadsTheApproval_AndTheDecisionIsObserved()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision, withPendingProduct: true);
        var repository = Repository(_tenantA);
        _transport.Preflight = (_, request) => PreflightOpen(request);
        _transport.Cancel = (_, _) => (Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        using var services = Services();

        Assert.Equal(503, (await WithdrawAsync(services, operation, Guid.NewGuid())).StatusCode);
        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(Checkpoint.WithdrawalOutcomeUnknown, stored.Checkpoint);

        Assert.Equal(_clock.GetUtcNow().Add(Retry).UtcTicks, stored.NextAttemptAtUtcTicksV1);

        // A cycle INSIDE the settle window does not read the approval: "still open" would be a guess while the
        // cancellation may yet land.
        var calls = _transport.Calls(_tenantA);
        _clock.Advance(Retry - TimeSpan.FromSeconds(1));
        await Runner(services).RunCycleAsync();
        Assert.Equal(calls, _transport.Calls(_tenantA));
        Assert.Equal(Checkpoint.WithdrawalOutcomeUnknown,
            (await repository.GetByOperationIdAsync(operation.OperationId))!.Checkpoint);

        // After it: the approval is still open, so the withdrawal did not land — it is forgotten, and the
        // decision is polled in the same cycle (not decided yet).
        _clock.Advance(TimeSpan.FromSeconds(1));
        _transport.Terminal = (_, _) => (Outcome.NonTerminal, "WORKFLOW_DECISION_NOT_TERMINAL");
        await Runner(services).RunCycleAsync();
        stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(Checkpoint.AwaitingDecision, stored.Checkpoint);
        Assert.Null(stored.WithdrawalCommandId);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await ProductAsync(operation)).LifecycleStatus);

        // Later the approver approves; the next cycle observes it.
        _clock.Advance(TimeSpan.FromMinutes(1));
        _transport.Terminal = (_, _) => Approved(stored);
        await Runner(services).RunCycleAsync();

        Assert.Equal(Checkpoint.Completed, (await repository.GetByOperationIdAsync(operation.OperationId))!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await ProductAsync(operation)).LifecycleStatus);
    }

    [Fact]
    public async Task ARefusedWithdrawal_CanBeAskedAgain_AndThenWithdraws()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision, withPendingProduct: true);
        var repository = Repository(_tenantA);
        _transport.Preflight = (_, request) => PreflightOpen(request);
        _transport.Cancel = (_, _) => (Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED");
        using var services = Services();
        Assert.Equal(503, (await WithdrawAsync(services, operation, Guid.NewGuid())).StatusCode);

        _transport.Cancel = (_, request) => Cancelled((ProductIdentityWorkflowCancellationRequest)request);
        var again = await WithdrawAsync(services, operation, Guid.NewGuid()); // a NEW command: the old one is forgotten

        Assert.True(again.Succeeded, again.ErrorCode);
        Assert.Equal(Checkpoint.Completed, (await repository.GetByOperationIdAsync(operation.OperationId))!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await ProductAsync(operation)).LifecycleStatus);
    }

    // ── fix round 2 ───────────────────────────────────────────────────────────────────────────────────────────────

    // Item 1, the failure it was written for: the cancellation lands AFTER the requester was told "unknown". A
    // worker that read the approval at once saw "open", forgot the withdrawal — and the decision poll then met an
    // approval that was cancelled, which is a quarantine.
    [Fact]
    public async Task ACancellationThatLandsLate_IsNotForgottenByACycleInBetween_AndTheRequestersRepeatCompletesIt()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision, withPendingProduct: true);
        var repository = Repository(_tenantA);
        var commandId = Guid.NewGuid();
        var engine = UseEngine();
        engine.HoldCancellations = true; // received, answered by nobody, processed later
        using var services = Services();
        Assert.Equal(503, (await WithdrawAsync(services, operation, commandId)).StatusCode);

        // The worker's cycle comes ten seconds later, while the engine is still working on the cancellation.
        _clock.Advance(TimeSpan.FromSeconds(10));
        await Runner(services).RunCycleAsync();
        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(Checkpoint.WithdrawalOutcomeUnknown, stored.Checkpoint);
        Assert.Equal(commandId, stored.WithdrawalCommandId);

        // The cancellation lands. After the settle window the worker reads: closed, no decision — it keeps the
        // withdrawal and quarantines nothing.
        engine.LandHeldCancellation();
        _clock.Advance(Retry);
        await Runner(services).RunCycleAsync();
        stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(Checkpoint.WithdrawalOutcomeUnknown, stored.Checkpoint);
        Assert.Equal(commandId, stored.WithdrawalCommandId);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, stored.RecoveryDisposition);

        // The requester repeats the same command. The engine replays a cancellation only for the same key AND the
        // same versions (anything else is a conflict): the repeat carries exactly what the first request carried.
        var repeated = await WithdrawAsync(services, operation, commandId);

        Assert.True(repeated.Succeeded, repeated.ErrorCode);
        Assert.Equal(2, engine.CancelCalls);
        Assert.Equal(Checkpoint.Completed, (await repository.GetByOperationIdAsync(operation.OperationId))!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await ProductAsync(operation)).LifecycleStatus);
    }

    // Fix round 3, item 1: the cancellation LANDED, its answer was lost — and the requester's repeat is refused
    // before it is processed (their session ran out, or the service identity is refused). That refusal says nothing
    // about the cancellation that landed: the withdrawal must not be forgotten.
    [Theory]
    [InlineData(Outcome.AuthenticationRejected, "WORKFLOW_DELEGATED_USER_UNAUTHENTICATED", 401)]
    [InlineData(Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED", 503)]
    public async Task AWithdrawalThatLanded_ThenRepeatedAndRefusedBeforeProcessing_IsNotForgotten_NotQuarantined_AndCompletes(
        Outcome refusal, string code, int status)
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision, withPendingProduct: true);
        var repository = Repository(_tenantA);
        var commandId = Guid.NewGuid();
        var engine = UseEngine();
        engine.LoseTheAnswer = true; // processed at once; the answer never arrives
        using var services = Services();
        Assert.Equal(503, (await WithdrawAsync(services, operation, commandId)).StatusCode);
        var scheduled = (await repository.GetByOperationIdAsync(operation.OperationId))!.NextAttemptAtUtcTicksV1;

        engine.Refuse = (refusal, code);
        _clock.Advance(TimeSpan.FromSeconds(5));
        var refused = await WithdrawAsync(services, operation, commandId);

        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(status, refused.StatusCode);
        Assert.Equal(Checkpoint.WithdrawalOutcomeUnknown, stored.Checkpoint);
        Assert.Equal(commandId, stored.WithdrawalCommandId);
        Assert.Equal(scheduled, stored.NextAttemptAtUtcTicksV1); // the refusal did not push the worker's read out

        // The worker reads the approval: cancelled at the engine, no decision. Not a quarantine.
        _clock.Advance(Retry);
        await Runner(services).RunCycleAsync();
        stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(Checkpoint.WithdrawalOutcomeUnknown, stored.Checkpoint);
        Assert.NotEqual(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, stored.RecoveryDisposition);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await ProductAsync(operation)).LifecycleStatus);

        // Signed in again, the requester repeats: the withdrawal completes.
        engine.Refuse = null;
        var repeated = await WithdrawAsync(services, operation, commandId);

        Assert.True(repeated.Succeeded, repeated.ErrorCode);
        Assert.Equal(Checkpoint.Completed, (await repository.GetByOperationIdAsync(operation.OperationId))!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await ProductAsync(operation)).LifecycleStatus);
    }

    // Fix round 3, item 6: the settle window is max(retry delay, floor) counted from NOW — measured with a retry
    // delay above the floor, and with a plan of the worker's that is already in the past (the ordinary case).
    [Fact]
    public async Task TheSettleWindow_IsTheRetryDelayWhenThatIsLonger_AndCountsFromNow_NotFromAPlanInThePast()
    {
        var longer = TimeSpan.FromSeconds(45);
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision, withPendingProduct: true);
        var past = await SeedAsync(_tenantB, Checkpoint.AwaitingDecision, withPendingProduct: true);
        await ScheduleAsync(Repository(_tenantB), past, _clock.GetUtcNow().AddMinutes(-10));
        UseEngine().LoseTheAnswer = true;
        using var services = Services();

        await WithdrawAsync(services, operation, Guid.NewGuid(), longer);
        UseEngine().LoseTheAnswer = true; // a second approval: its own engine state
        await WithdrawAsync(services, past, Guid.NewGuid());

        Assert.Equal(_clock.GetUtcNow().Add(longer).UtcTicks,
            (await Repository(_tenantA).GetByOperationIdAsync(operation.OperationId))!.NextAttemptAtUtcTicksV1);
        Assert.Equal(_clock.GetUtcNow().Add(Retry).UtcTicks,
            (await Repository(_tenantB).GetByOperationIdAsync(past.OperationId))!.NextAttemptAtUtcTicksV1);
    }

    // Item 6: the write that stores the versions settles too — it is what stands on the record WHILE the
    // cancellation is at the engine (a process that dies there writes nothing afterwards).
    [Fact]
    public async Task WhileTheCancellationIsAtTheEngine_TheRecordAlreadyKeepsTheWorkerAway()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision, withPendingProduct: true);
        var repository = Repository(_tenantA);
        var commandId = Guid.NewGuid();
        _transport.Preflight = (_, _) => throw new InvalidOperationException("the wire broke");
        using var services = Services();
        await Assert.ThrowsAsync<InvalidOperationException>(() => WithdrawAsync(services, operation, commandId));

        // Five minutes later the requester repeats; what stands on the record during the cancellation call?
        _clock.Advance(TimeSpan.FromMinutes(5));
        long? during = null;
        Checkpoint? at = null;
        _transport.Preflight = (_, request) => PreflightOpen(request);
        _transport.Cancel = (_, _) =>
        {
            var row = repository.GetByOperationIdAsync(operation.OperationId).GetAwaiter().GetResult()!;
            (during, at) = (row.NextAttemptAtUtcTicksV1, row.Checkpoint);
            return (Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        };
        await WithdrawAsync(services, operation, commandId);

        Assert.Equal(Checkpoint.WithdrawalPreflightObserved, at);
        Assert.Equal(_clock.GetUtcNow().Add(Retry).UtcTicks, during);
    }

    // Item 6: two faults are one streak up to two ceilings apart (2 x 15 min), and not beyond.
    [Theory]
    [InlineData(0, 2, LogLevel.Error)]   // exactly two ceilings: still the same outage — and by now an error
    [InlineData(1, 1, LogLevel.Warning)] // a second more: a new streak
    public async Task TwoFaults_AreOneStreak_UpToTwoCeilingsApart(int secondsBeyond, int count, LogLevel level)
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision);
        var repository = Repository(_tenantA);
        _transport.Terminal = (_, _) => (Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        using var services = Services();
        await Runner(services).RunCycleAsync();
        var first = _clock.GetUtcNow().UtcTicks;

        _clock.Advance(TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(secondsBeyond));
        _log.Entries.Clear();
        await Runner(services).RunCycleAsync();

        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(count, stored.ServiceFaultCount);
        Assert.Equal(count == 2 ? first : _clock.GetUtcNow().UtcTicks, stored.ServiceFaultSinceUtcTicksV1);
        Assert.Equal(level, Assert.Single(_log.Entries,
            entry => entry.Message.Contains("met a service fault", StringComparison.Ordinal)).Level);
    }

    // Item 6: a streak that spans EXACTLY the ceiling is an error (not only one that is longer).
    [Fact]
    public async Task AStreakThatSpansExactlyTheCeiling_IsAnError()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision);
        _transport.Terminal = (_, _) => (Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        using var services = Services();
        await Runner(services).RunCycleAsync();

        _clock.Advance(TimeSpan.FromMinutes(15));
        _log.Entries.Clear();
        await Runner(services).RunCycleAsync();

        Assert.Equal(2, (await Repository(_tenantA).GetByOperationIdAsync(operation.OperationId))!.ServiceFaultCount);
        Assert.Equal(LogLevel.Error, Assert.Single(_log.Entries,
            entry => entry.Message.Contains("met a service fault", StringComparison.Ordinal)).Level);
    }

    // Item 6: a write that only passes by must not make an old streak look as if it were going on.
    [Fact]
    public async Task AWithdrawalLongAfterAnOldFault_DoesNotReviveTheOldStreak()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision, withPendingProduct: true);
        var repository = Repository(_tenantA);
        _transport.Terminal = (_, _) => (Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        using var services = Services();
        await Runner(services).RunCycleAsync();
        Assert.Equal(1, (await repository.GetByOperationIdAsync(operation.OperationId))!.ServiceFaultCount);

        _clock.Advance(TimeSpan.FromHours(3)); // the worker was off
        _log.Entries.Clear();
        UseEngine().LoseTheAnswer = true;
        await WithdrawAsync(services, operation, Guid.NewGuid());

        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(Checkpoint.WithdrawalOutcomeUnknown, stored.Checkpoint);
        Assert.Equal(0, stored.ServiceFaultCount);
        Assert.Equal(_clock.GetUtcNow().UtcTicks, stored.ServiceFaultSinceUtcTicksV1);
        Assert.Equal(LogLevel.Warning, Assert.Single(_log.Entries,
            entry => entry.Message.Contains("met a service fault", StringComparison.Ordinal)).Level);
    }

    [Fact]
    public async Task ALeaseGivenBackLongAfterAnOldFault_DoesNotReviveTheOldStreak()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingMakerReplay, withDraftProduct: true);
        var repository = Repository(_tenantA);
        _transport.Start = (_, _) => (Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED");
        using var services = Services();
        Assert.Equal(503, (await SubmitAsync(services, operation)).StatusCode);
        var old = (await repository.GetByOperationIdAsync(operation.OperationId))!.ServiceFaultSinceUtcTicksV1;
        Assert.Equal(_clock.GetUtcNow().UtcTicks, old);

        _clock.Advance(TimeSpan.FromHours(3));
        _transport.Start = (_, _) => throw new InvalidOperationException("the wire broke"); // only gives the lease back
        await Assert.ThrowsAsync<InvalidOperationException>(() => SubmitAsync(services, operation));
        _clock.Advance(TimeSpan.FromMinutes(1));
        _transport.Start = (_, _) => (Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED");
        await SubmitAsync(services, operation);

        Assert.Equal(_clock.GetUtcNow().UtcTicks,
            (await repository.GetByOperationIdAsync(operation.OperationId))!.ServiceFaultSinceUtcTicksV1);
    }

    // The two discovery filters, each on its own: the operation list inside a tenant that HAS other due
    // operations, and the tenant list for tenants whose only operations are at rest for good.
    [Fact]
    public async Task InATenantWithDueOperations_AnOrdinaryAwaitingMakerReplayOperation_IsNotListedForTheWorker()
    {
        var due = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision);
        var ordinary = await SeedAsync(_tenantA, Checkpoint.AwaitingMakerReplay);
        var parked = await SeedAsync(_tenantA, Checkpoint.AwaitingMakerReplay);
        await _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(item => item.TenantId == _tenantA && item.OperationId == parked.OperationId,
                Builders<GlobalProductIdentityWorkflowOperation>.Update.Set(item => item.WithdrawalCommandId, Guid.NewGuid()));

        var listed = (await Repository(_tenantA).DiscoverRecoverableAsync(_clock.GetUtcNow().UtcTicks, 10))
            .Operations.Select(item => item.OperationId).ToList();

        Assert.Contains(due.OperationId, listed);
        Assert.Contains(parked.OperationId, listed);
        Assert.DoesNotContain(ordinary.OperationId, listed);
    }

    [Theory]
    [InlineData(Checkpoint.AbandonedBeforeWorkflowStart)]
    [InlineData(Checkpoint.Superseded)]
    [InlineData(Checkpoint.Completed)]
    public async Task ATenantWhoseOnlyOperationIsAtRestForGood_IsNotDiscovered(Checkpoint atRest)
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision);
        await SeedAsync(_tenantB, Checkpoint.AwaitingDecision);
        await _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
                GlobalProductIdentityWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(item => item.TenantId == _tenantA && item.OperationId == operation.OperationId,
                Builders<GlobalProductIdentityWorkflowOperation>.Update.Set(item => item.Checkpoint, atRest));

        var discovered = (await new GlobalProductIdentityWorkflowTenantPartitionDiscoveryRepository(_database)
            .DiscoverAsync(null, _clock.GetUtcNow().UtcTicks, 10)).TenantIds;

        Assert.DoesNotContain(_tenantA, discovered);
        Assert.Contains(_tenantB, discovered);
    }

    // Items 1 and 5 (NEW-6): a cycle that falls between the requester's two steps must not drop the withdrawal,
    // and a withdrawal must not pull the worker's next read forward.
    [Fact]
    public async Task ACycleBetweenTheRequestersTwoSteps_DoesNotDropTheWithdrawal_AndTheWorkersScheduleIsNotBroughtForward()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision, withPendingProduct: true);
        var repository = Repository(_tenantA);
        var scheduled = _clock.GetUtcNow().AddMinutes(12);
        await ScheduleAsync(repository, operation, scheduled);
        var commandId = Guid.NewGuid();
        _transport.Preflight = (_, _) => throw new InvalidOperationException("the wire broke"); // cut between the steps
        using var services = Services();

        await Assert.ThrowsAsync<InvalidOperationException>(() => WithdrawAsync(services, operation, commandId));
        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(Checkpoint.WithdrawalRequested, stored.Checkpoint);
        Assert.Equal(scheduled.UtcTicks, stored.NextAttemptAtUtcTicksV1); // 12 minutes stay 12 minutes

        _transport.Preflight = (_, request) => PreflightOpen(request);
        var calls = _transport.Calls(_tenantA);
        await Runner(services).RunCycleAsync();
        stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(calls, _transport.Calls(_tenantA));
        Assert.Equal(Checkpoint.WithdrawalRequested, stored.Checkpoint);
        Assert.Equal(commandId, stored.WithdrawalCommandId);

        // Without a schedule of the worker's own, the settle window alone keeps the cycle away.
        var fresh = await SeedAsync(_tenantB, Checkpoint.AwaitingDecision, withPendingProduct: true);
        _transport.Preflight = (tenant, request) => tenant == _tenantB
            ? throw new InvalidOperationException("the wire broke")
            : PreflightOpen(request);
        await Assert.ThrowsAsync<InvalidOperationException>(() => WithdrawAsync(services, fresh, Guid.NewGuid()));
        var freshStored = (await Repository(_tenantB).GetByOperationIdAsync(fresh.OperationId))!;
        Assert.Equal(Checkpoint.WithdrawalRequested, freshStored.Checkpoint);
        Assert.Equal(_clock.GetUtcNow().Add(Retry).UtcTicks, freshStored.NextAttemptAtUtcTicksV1);
        Assert.Null(freshStored.LeaseOwner);
        await Runner(services).RunCycleAsync();
        Assert.Equal(1, _transport.Calls(_tenantB)); // only the requester's own call

        // A repeat that is cut off LATER, in the middle of the cancellation itself: the settle window starts again
        // from that moment — the cancellation may be landing right now.
        var freshCommand = freshStored.WithdrawalCommandId!.Value;
        _clock.Advance(TimeSpan.FromMinutes(5));
        _transport.Preflight = (_, request) => PreflightOpen(request);
        _transport.Cancel = (_, _) => throw new InvalidOperationException("the wire broke");
        await Assert.ThrowsAsync<InvalidOperationException>(() => WithdrawAsync(services, fresh, freshCommand));
        _clock.Advance(TimeSpan.FromMinutes(5));
        await Assert.ThrowsAsync<InvalidOperationException>(() => WithdrawAsync(services, fresh, freshCommand));
        freshStored = (await Repository(_tenantB).GetByOperationIdAsync(fresh.OperationId))!;
        Assert.Equal(Checkpoint.WithdrawalPreflightObserved, freshStored.Checkpoint);
        Assert.Equal(_clock.GetUtcNow().Add(Retry).UtcTicks, freshStored.NextAttemptAtUtcTicksV1);
        Assert.Null(freshStored.LeaseOwner);
    }

    // Item 2: "conflict" while the approval is STILL OPEN (it moved on since the versions were read).
    [Fact]
    public async Task AConflictWhileTheApprovalIsStillOpen_DoesNotLockTheWithdrawal_ItIsAskedAgainAndWithdraws()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision, withPendingProduct: true);
        var repository = Repository(_tenantA);
        var engine = UseEngine();
        // The approval moves on between the read of its versions and the cancellation — once.
        var moved = false;
        _transport.Cancel = (_, request) =>
        {
            if (!moved)
            {
                moved = true;
                engine.InstanceVersion++;
                engine.TaskVersion++;
            }
            return engine.Cancel(request);
        };
        using var services = Services();

        var result = await WithdrawAsync(services, operation, Guid.NewGuid());

        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(2, engine.CancelCalls);
        Assert.Equal(Checkpoint.Completed, stored.Checkpoint);
        // What went through carried the versions read after the conflict (the engine accepts no others).
        Assert.Equal((4, 6), (stored.WithdrawalExpectedWorkflowInstanceVersion, stored.WithdrawalExpectedApprovalTaskVersion));
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await ProductAsync(operation)).LifecycleStatus);
    }

    [Fact]
    public async Task AnApprovalThatKeepsMoving_IsAnsweredAfterOneRetry_AndTheRecordCanBeWithdrawnLater()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision, withPendingProduct: true);
        var repository = Repository(_tenantA);
        var engine = UseEngine();
        var moving = true;
        _transport.Cancel = (_, request) =>
        {
            if (moving)
            {
                engine.InstanceVersion++;
                engine.TaskVersion++;
            }
            return engine.Cancel(request);
        };
        using var services = Services();
        var commandId = Guid.NewGuid();

        var result = await WithdrawAsync(services, operation, commandId);

        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal((409, ProductIdentityWorkflowMakerCodes.ApprovalChanging), (result.StatusCode, result.ErrorCode));
        Assert.Equal(2, engine.CancelCalls);
        Assert.Equal(Checkpoint.AwaitingDecision, stored.Checkpoint);
        Assert.Null(stored.WithdrawalCommandId);
        Assert.Null(stored.LeaseOwner);
        Assert.Null(stored.NextAttemptAtUtcTicksV1); // the decision poll is not kept waiting

        moving = false;
        var repeated = await WithdrawAsync(services, operation, commandId);

        Assert.True(repeated.Succeeded, repeated.ErrorCode);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await ProductAsync(operation)).LifecycleStatus);
    }

    [Fact]
    public async Task AConflictBecauseTheApprovalWasDecided_KeepsItsAnswer_AndTheWorkerObservesTheDecision()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision, withPendingProduct: true);
        var repository = Repository(_tenantA);
        var commandId = Guid.NewGuid();
        var preflights = 0;
        _transport.Preflight = (_, request) => ++preflights == 1
            ? PreflightOpen(request)
            : (Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND");
        _transport.Cancel = (_, _) => (Outcome.Conflict, "WORKFLOW_TRUSTED_CANCEL_CONFLICT");
        using var services = Services();

        var first = await WithdrawAsync(services, operation, commandId);
        // The worker polls the decision in between (not decided yet) and rewrites LastFailureCode; the repeat still
        // reads the withdrawal's own answer.
        _transport.Terminal = (_, _) => (Outcome.NonTerminal, "WORKFLOW_DECISION_NOT_TERMINAL");
        await Runner(services).RunCycleAsync();
        Assert.Equal("WORKFLOW_DECISION_NOT_TERMINAL",
            (await repository.GetByOperationIdAsync(operation.OperationId))!.LastFailureCode);
        var again = await WithdrawAsync(services, operation, commandId);

        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal("WORKFLOW_TRUSTED_CANCEL_CONFLICT", stored.WithdrawalRefusalCode);
        Assert.Equal((409, "WORKFLOW_TRUSTED_CANCEL_CONFLICT"), (first.StatusCode, first.ErrorCode));
        Assert.Equal((409, "WORKFLOW_TRUSTED_CANCEL_CONFLICT"), (again.StatusCode, again.ErrorCode));
        Assert.Equal(Checkpoint.AwaitingDecision, stored.Checkpoint);

        _transport.Terminal = (_, _) => Approved(stored);
        _clock.Advance(Retry);
        await Runner(services).RunCycleAsync();

        Assert.Equal(Checkpoint.Completed, (await repository.GetByOperationIdAsync(operation.OperationId))!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await ProductAsync(operation)).LifecycleStatus);
    }

    // Item 3: a withdrawal an earlier version parked in AwaitingMakerReplay is found by the REAL discovery — and
    // an ordinary AwaitingMakerReplay operation (nothing started) still is not.
    [Fact]
    public async Task AWithdrawalParkedInAwaitingMakerReplayByAnEarlierVersion_IsFoundByDiscovery_AndItsDecisionIsObserved()
    {
        var parked = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision, withPendingProduct: true);
        var ordinary = await SeedAsync(_tenantB, Checkpoint.AwaitingMakerReplay, withDraftProduct: true);
        var repository = Repository(_tenantA);
        var collection = _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
            GlobalProductIdentityWorkflowOperationRepository.CollectionName);
        // What the earlier version left behind, written the way it wrote it: no transition of today leads here.
        await collection.UpdateOneAsync(item => item.TenantId == _tenantA && item.OperationId == parked.OperationId,
            Builders<GlobalProductIdentityWorkflowOperation>.Update
                .Set(item => item.Checkpoint, Checkpoint.AwaitingMakerReplay)
                .Set(item => item.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay)
                .Set(item => item.WithdrawalCommandId, Guid.NewGuid())
                .Set(item => item.WithdrawalFingerprint, "an-earlier-fingerprint")
                .Set(item => item.WithdrawalRequesterSubjectId, _makerId)
                .Set(item => item.WithdrawalExpectedProductVersion, 1)
                .Set(item => item.WithdrawalReasonCode, "REQUESTER_WITHDRAWAL")
                .Set(item => item.WithdrawalExpectedWorkflowInstanceVersion, 3)
                .Set(item => item.WithdrawalExpectedApprovalTaskVersion, 5)
                .Set(item => item.WithdrawalObjectRef, "GP-REF"));
        var stored = (await repository.GetByOperationIdAsync(parked.OperationId))!;
        _transport.Preflight = (_, _) => (Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND"); // decided meanwhile
        _transport.Terminal = (_, _) => Approved(stored);
        using var services = Services();

        var discovered = (await services.GetRequiredService<IGlobalProductIdentityWorkflowTenantPartitionDiscovery>()
            .DiscoverAsync(null, _clock.GetUtcNow().UtcTicks, 10)).TenantIds;
        var cycle = await Runner(services).RunCycleAsync();

        Assert.Contains(_tenantA, discovered);
        Assert.DoesNotContain(_tenantB, discovered);
        Assert.Equal(0, _transport.Calls(_tenantB));
        Assert.Equal(1, cycle.CompletedCount);
        Assert.Equal(Checkpoint.Completed, (await repository.GetByOperationIdAsync(parked.OperationId))!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await ProductAsync(parked)).LifecycleStatus);
        Assert.Equal(Checkpoint.AwaitingMakerReplay,
            (await Repository(_tenantB).GetByOperationIdAsync(ordinary.OperationId))!.Checkpoint);
    }

    // Item 4: the error is about an outage that is going on — not about a fault that happened once, long ago.
    [Fact]
    public async Task AnAnswerEndsTheFaultStreak_SoALaterGlitchIsAWarning_AndOnlyConsecutiveFaultsBecomeAnErrorByDuration()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision);
        var repository = Repository(_tenantA);
        using var services = Services();

        // 09:00 — one fault. Then the engine answers (not decided yet): the streak is over.
        _transport.Terminal = (_, _) => (Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        await Runner(services).RunCycleAsync();
        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(1, stored.ServiceFaultCount);
        Assert.NotNull(stored.ServiceFaultSinceUtcTicksV1);
        _clock.Advance(TimeSpan.FromMinutes(1));
        _transport.Terminal = (_, _) => (Outcome.NonTerminal, "WORKFLOW_DECISION_NOT_TERMINAL");
        await Runner(services).RunCycleAsync();
        stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(0, stored.ServiceFaultCount);
        Assert.Null(stored.ServiceFaultSinceUtcTicksV1);

        // 11:00 — a single glitch: a warning, counted from one, since eleven.
        _clock.Advance(TimeSpan.FromHours(2));
        _log.Entries.Clear();
        _transport.Terminal = (_, _) => (Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        await Runner(services).RunCycleAsync();
        stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        var glitchAt = _clock.GetUtcNow().UtcTicks;
        Assert.Equal(1, stored.ServiceFaultCount);
        Assert.Equal(glitchAt, stored.ServiceFaultSinceUtcTicksV1);
        var glitch = Assert.Single(_log.Entries, entry => entry.Message.Contains("met a service fault", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Warning, glitch.Level);

        // The outage goes on: consecutive faults, and once they span the ceiling (15 min) it is an error — at the
        // worker's third attempt, well before the attempt threshold of five.
        _clock.Advance(TimeSpan.FromMinutes(1));
        await Runner(services).RunCycleAsync();
        _clock.Advance(TimeSpan.FromMinutes(15));
        _log.Entries.Clear();
        await Runner(services).RunCycleAsync();
        stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(3, stored.ServiceFaultCount);
        Assert.Equal(glitchAt, stored.ServiceFaultSinceUtcTicksV1);
        var outage = Assert.Single(_log.Entries, entry => entry.Message.Contains("met a service fault", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Error, outage.Level);
    }

    // Item 4: faults far apart are not one outage. With NO answer in between (the worker was off), a fault long
    // after the last one starts a new streak. On the real store: the in-memory fake hands the processor the very
    // object a claim has just stamped, so it cannot show how old the last write was.
    [Fact]
    public async Task Worker_AFaultLongAfterTheLastOne_StartsANewStreak_NotAnErrorSinceTheOldOne()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision);
        var repository = Repository(_tenantA);
        _transport.Terminal = (_, _) => (Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
        using var services = Services();
        await Runner(services).RunCycleAsync();
        var first = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(1, first.ServiceFaultCount);

        _clock.Advance(TimeSpan.FromHours(3)); // no cycle ran in between
        _log.Entries.Clear();
        await Runner(services).RunCycleAsync();

        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(1, stored.ServiceFaultCount);
        Assert.Equal(_clock.GetUtcNow().UtcTicks, stored.ServiceFaultSinceUtcTicksV1);
        var line = Assert.Single(_log.Entries, entry => entry.Message.Contains("met a service fault", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Warning, line.Level);
    }

    // Item 6: the withdrawal is forgotten WHOLE in the real store — every field a withdrawal writes.
    [Fact]
    public async Task ForgettingAWithdrawal_ClearsEveryFieldAWithdrawalWrites()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision, withPendingProduct: true);
        var repository = Repository(_tenantA);
        var collection = _database.GetCollection<GlobalProductIdentityWorkflowOperation>(
            GlobalProductIdentityWorkflowOperationRepository.CollectionName);
        await collection.UpdateOneAsync(item => item.TenantId == _tenantA && item.OperationId == operation.OperationId,
            Builders<GlobalProductIdentityWorkflowOperation>.Update
                .Set(item => item.Checkpoint, Checkpoint.WithdrawalOutcomeUnknown)
                .Set(item => item.WithdrawalCommandId, Guid.NewGuid())
                .Set(item => item.WithdrawalFingerprint, "fingerprint")
                .Set(item => item.WithdrawalRequesterSubjectId, _makerId)
                .Set(item => item.WithdrawalExpectedProductVersion, 1)
                .Set(item => item.WithdrawalReasonCode, "REQUESTER_WITHDRAWAL")
                .Set(item => item.WithdrawalComment, "a comment")
                .Set(item => item.WithdrawalExpectedWorkflowInstanceVersion, 3)
                .Set(item => item.WithdrawalExpectedApprovalTaskVersion, 5)
                .Set(item => item.WithdrawalTransitionLogId, Guid.NewGuid())
                .Set(item => item.WithdrawalObservedAtUtcTicksV1, 42)
                .Set(item => item.WithdrawalTransitionSequence, 3)
                .Set(item => item.WithdrawalResultWorkflowInstanceVersion, 4)
                .Set(item => item.WithdrawalResultApprovalTaskVersion, 6)
                .Set(item => item.WithdrawalTaskStatus, "Cancelled")
                .Set(item => item.WithdrawalInstanceStatus, "Cancelled")
                .Set(item => item.WithdrawalObjectRef, "GP-REF")
                .Set(item => item.WithdrawalRefusalCode, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND")
                .Set(item => item.WithdrawalAwaitingRequesterSinceUtcTicksV1, 42));
        var now = _clock.GetUtcNow().UtcTicks;
        var claim = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [Checkpoint.WithdrawalOutcomeUnknown], "worker", now, now + Lease.Ticks));
        Assert.NotNull(claim);

        Assert.True(await repository.AdvanceAsync(claim!, new(Checkpoint.AwaitingDecision,
            ProductIdentityWorkflowRecoveryDisposition.None, now + 1, ReleaseLease: true, ClearWithdrawal: true)));

        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        var withdrawalFields = typeof(GlobalProductIdentityWorkflowOperation).GetProperties()
            .Where(property => property.Name.StartsWith("Withdrawal", StringComparison.Ordinal)).ToList();
        Assert.Equal(18, withdrawalFields.Count); // a nineteenth field has to be cleared too — and listed above
        Assert.All(withdrawalFields, property => Assert.True(property.GetValue(stored) is null, property.Name));
        Assert.Equal(Checkpoint.AwaitingDecision, stored.Checkpoint);
    }

    // Item 6: a lease is given back even when the caller's token is already cancelled.
    [Fact]
    public async Task ASubmitWhoseCallerIsGone_StillGivesItsLeaseBack_ThroughTheRealStore()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingMakerReplay, withDraftProduct: true);
        var repository = Repository(_tenantA);
        using var cancelled = new CancellationTokenSource();
        _transport.Start = (_, _) =>
        {
            cancelled.Cancel();
            cancelled.Token.ThrowIfCancellationRequested();
            return (Outcome.Retryable, "unreachable");
        };
        using var services = Services();
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(operation.TenantId);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scope.ServiceProvider.GetRequiredService<GlobalProductIdentityWorkflowProcessor>()
                .StartInteractiveAsync(operation.TenantId, operation.GlobalProductId, 0, operation.OperationId,
                    _makerId, "maker-token", Lease, Retry, cancelled.Token));

        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Null(stored.LeaseOwner);
        Assert.Null(stored.LeaseUntilUtcTicksV1);
        Assert.DoesNotContain(_log.Entries, entry => entry.Message.Contains("could not be given back", StringComparison.Ordinal));
    }

    // ── the recovery cycle ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cycle_ARefusedCredential_StopsTheWholeCycleAfterOneCall_AndNothingIsQuarantined()
    {
        var operations = new[]
        {
            await SeedAsync(_tenantA, Checkpoint.AwaitingDecision), await SeedAsync(_tenantA, Checkpoint.AwaitingDecision),
            await SeedAsync(_tenantB, Checkpoint.AwaitingDecision), await SeedAsync(_tenantB, Checkpoint.AwaitingDecision)
        };
        _transport.Terminal = (_, _) => (Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_REJECTED");
        using var services = Services();

        var result = await Runner(services).RunCycleAsync();

        var stored = await StoredAsync(operations);
        Assert.Equal(1, _transport.Calls(_tenantA) + _transport.Calls(_tenantB));
        Assert.All(stored, item => Assert.Equal(Checkpoint.AwaitingDecision, item.Checkpoint));
        var waiting = Assert.Single(stored, item => item.ServiceFaultCount == 1);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        Assert.Equal("PRODUCT_WORKFLOW_IDENTITY_REJECTED", waiting.LastFailureCode);
        Assert.All(stored.Where(item => item.OperationId != waiting.OperationId), item =>
        {
            Assert.Equal(0, item.ServiceFaultCount);
            Assert.Null(item.LastFailureCode);
            Assert.Null(item.NextAttemptAtUtcTicksV1);
        });
        Assert.Equal(0, result.FailedCount);
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Error
            && entry.Message.Contains("cycle stopped", StringComparison.Ordinal)
            && entry.Message.Contains("PRODUCT_WORKFLOW_IDENTITY_REJECTED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cycle_AMissingTenantGrant_SkipsTheRestOfThatTenantOnly()
    {
        var a = new[] { await SeedAsync(_tenantA, Checkpoint.AwaitingDecision), await SeedAsync(_tenantA, Checkpoint.AwaitingDecision) };
        var b = new[] { await SeedAsync(_tenantB, Checkpoint.AwaitingDecision), await SeedAsync(_tenantB, Checkpoint.AwaitingDecision) };
        _transport.Terminal = (tenant, _) => tenant == _tenantA
            ? (Outcome.AuthenticationRejected, "PRODUCT_WORKFLOW_IDENTITY_NOT_GRANTED")
            : (Outcome.NonTerminal, "WORKFLOW_DECISION_NOT_TERMINAL");
        using var services = Services();

        await Runner(services).RunCycleAsync();

        Assert.Equal(1, _transport.Calls(_tenantA));
        Assert.Equal(2, _transport.Calls(_tenantB));
        Assert.Single(await StoredAsync(a), item => item.ServiceFaultCount == 1);
        Assert.All(await StoredAsync(b), item =>
        {
            Assert.Equal("WORKFLOW_DECISION_NOT_TERMINAL", item.LastFailureCode);
            Assert.Equal(0, item.ServiceFaultCount);
        });
        Assert.Contains(_log.Entries, entry => entry.Level == LogLevel.Warning
            && entry.Message.Contains(_tenantA.ToString(), StringComparison.Ordinal)
            && entry.Message.Contains("skipped the rest of tenant", StringComparison.Ordinal));
        Assert.DoesNotContain(_log.Entries, entry => entry.Message.Contains("cycle stopped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cycle_WhenTheTransportIsNotConfigured_TouchesNoOperation()
    {
        var operations = new[]
        {
            await SeedAsync(_tenantA, Checkpoint.AwaitingDecision), await SeedAsync(_tenantB, Checkpoint.StartOutcomeUnknown)
        };
        var before = await StoredAsync(operations);
        _readiness.Fault = "PRODUCT_WORKFLOW_IDENTITY_CONFIGURATION_INVALID";
        using var services = Services();

        var result = await Runner(services).RunCycleAsync();

        var after = await StoredAsync(operations);
        Assert.Equal(new ProductIdentityWorkflowRecoveryRunResult(0, 0, 0, 0, 0), result);
        Assert.Equal(0, _transport.Calls(_tenantA) + _transport.Calls(_tenantB));
        Assert.Equal(before.Select(item => (item.Checkpoint, item.Version, item.LeaseGeneration, item.UpdatedAtUtcTicksV1)),
            after.Select(item => (item.Checkpoint, item.Version, item.LeaseGeneration, item.UpdatedAtUtcTicksV1)));
        var line = Assert.Single(_log.Entries, entry => entry.Message.Contains("cycle skipped", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Error, line.Level);
        Assert.Contains("PRODUCT_WORKFLOW_IDENTITY_CONFIGURATION_INVALID", line.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cycle_AnExceptionIsNoLongerSwallowedSilently_AndItsMessageIsNotLogged()
    {
        var operation = await SeedAsync(_tenantA, Checkpoint.AwaitingDecision);
        _transport.Terminal = (tenant, _) => tenant == _tenantA
            ? throw new InvalidOperationException("a message that could carry the-raw-client-secret")
            : (Outcome.NonTerminal, "WORKFLOW_DECISION_NOT_TERMINAL");
        using var services = Services();

        var result = await Runner(services).RunCycleAsync();

        Assert.Equal(1, result.FailedCount);
        // The step that threw gave its lease back: the operation is not held until the lease runs out.
        Assert.Null((await Repository(_tenantA).GetByOperationIdAsync(operation.OperationId))!.LeaseOwner);
        var line = Assert.Single(_log.Entries, entry => entry.Message.Contains(operation.OperationId.ToString(), StringComparison.Ordinal)
            && entry.Message.Contains("failed with", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Error, line.Level);
        Assert.Contains("System.InvalidOperationException", line.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("the-raw-client-secret", line.Message, StringComparison.Ordinal);
    }

    // ── fixtures ──────────────────────────────────────────────────────────────────────────────────────────────────

    private ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_database);
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<IGlobalProductIdentityWorkflowOperationRepository>(provider =>
            new GlobalProductIdentityWorkflowOperationRepository(
                _database, provider.GetRequiredService<ITenantContext>()));
        services.AddScoped<IProductLegalEntityScopeRolloutStateRepository>(provider =>
            new ProductLegalEntityScopeRolloutStateRepository(
                _database, provider.GetRequiredService<ITenantContext>()));
        services.AddSingleton<IGlobalProductIdentityWorkflowTenantPartitionDiscovery>(
            new GlobalProductIdentityWorkflowTenantPartitionDiscoveryRepository(_database));
        services.AddScoped<IProductLegalEntityScopePolicyRepository>(provider =>
            new ProductLegalEntityScopePolicyRepository(
                _database, provider.GetRequiredService<ITenantContext>()));
        // The worker's legal-entity scope rule (S1, WP-MDM-GSKU-ACCEPT-01 FIX1): the runner asks it for every tenant.
        services.AddScoped<Diten.MdmService.Application.Features.ProductLegalEntityScopes.ProductIdentityBackgroundScope>();
        services.AddScoped<IGlobalProductRepository>(provider =>
            new GlobalProductRepository(_database, provider.GetRequiredService<ITenantContext>()));
        services.AddSingleton<IProductIdentityWorkflowClient>(_transport);
        services.AddSingleton<IProductIdentityWorkflowTransportReadiness>(_readiness);
        services.AddSingleton(new ProductIdentityWorkflowStartConfiguration(
            _templateId, null, [_approverId], "IDENTITY_APPROVAL", true, true, null));
        services.AddSingleton<TimeProvider>(_clock);
        services.AddScoped<ProductIdentityWorkflowStartRequestFactory>();
        services.AddSingleton<ILoggerFactory>(_log);
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddGlobalProductIdentityWorkflowProcessor(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }

    private ProductIdentityWorkflowRecoveryRunner Runner(ServiceProvider services) => new(
        services.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(new ProductIdentityWorkflowWorkerOptions
        {
            Enabled = true,
            PollIntervalSeconds = 1,
            TenantPageSize = 10,
            OperationPageSize = 1, // one operation per page: "skip the rest of the tenant" has to hold across pages
            LeaseSeconds = 60,
            RetryDelaySeconds = 30,
            LeaseOwner = $"test-worker-{_tenantA:N}"
        }),
        _clock,
        services.GetRequiredService<ILogger<ProductIdentityWorkflowRecoveryRunner>>());

    private async Task<GlobalProductIdentityWorkflowProcessingResult> SubmitAsync(
        ServiceProvider services, GlobalProductIdentityWorkflowOperation operation)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(operation.TenantId);
        return await scope.ServiceProvider.GetRequiredService<GlobalProductIdentityWorkflowProcessor>()
            .StartInteractiveAsync(operation.TenantId, operation.GlobalProductId, 0, operation.OperationId,
                _makerId, "maker-token", Lease, Retry);
    }

    private async Task<GlobalProductIdentityWorkflowProcessingResult> WithdrawAsync(
        ServiceProvider services, GlobalProductIdentityWorkflowOperation operation, Guid commandId,
        TimeSpan? retry = null)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(operation.TenantId);
        return await scope.ServiceProvider.GetRequiredService<GlobalProductIdentityWorkflowProcessor>()
            .WithdrawInteractiveAsync(operation.TenantId, operation.GlobalProductId, 1, commandId, _makerId,
                "REQUESTER_WITHDRAWAL", null, "maker-token", Lease, retry ?? Retry);
    }

    // The part of Platform's cancellation contract these tests depend on (TrustedWorkflowCancellationCoordinator):
    // a cancellation is accepted for the CURRENT versions only; once it is recorded, the same key is replayed only
    // with the same versions and anything else is a conflict; a cancelled approval is not open, and holds no decision.
    private Engine UseEngine()
    {
        var engine = new Engine(Cancelled);
        _transport.Preflight = (_, request) => engine.Preflight(request);
        _transport.Cancel = (_, request) => engine.Cancel(request);
        _transport.Terminal = (_, _) => engine.Terminal();
        return engine;
    }

    private sealed class Engine(Func<ProductIdentityWorkflowCancellationRequest, object> evidence)
    {
        private (string Key, int Instance, int Task)? _recorded;
        private (string Key, int Instance, int Task)? _held;
        public int InstanceVersion { get; set; } = 3;
        public int TaskVersion { get; set; } = 5;
        public bool HoldCancellations { get; set; }
        public bool LoseTheAnswer { get; set; }
        public (Outcome Outcome, string Code)? Refuse { get; set; }
        public int CancelCalls { get; private set; }

        public void LandHeldCancellation() => (_recorded, _held) = (_held, null);

        public object Preflight(object request) => _recorded is null
            ? PreflightOpen(request, InstanceVersion, TaskVersion)
            : (Outcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND");

        public object Terminal() => _recorded is null
            ? (Outcome.NonTerminal, "WORKFLOW_DECISION_NOT_TERMINAL")
            : (Outcome.Conflict, "WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT");

        public object Cancel(object request)
        {
            CancelCalls++;
            if (Refuse is { } refused) return refused; // refused before anything is looked at
            var asked = (ProductIdentityWorkflowCancellationRequest)request;
            var facts = (asked.IdempotencyKey, asked.ExpectedWorkflowInstanceVersion, asked.ExpectedApprovalTaskVersion);
            if (_recorded is { } recorded)
                return recorded == facts ? evidence(asked) : (Outcome.Conflict, "WORKFLOW_TRUSTED_CANCEL_CONFLICT");
            if (asked.ExpectedWorkflowInstanceVersion != InstanceVersion || asked.ExpectedApprovalTaskVersion != TaskVersion)
                return (Outcome.Conflict, "WORKFLOW_TRUSTED_CANCEL_CONFLICT");
            if (HoldCancellations)
            {
                _held = facts;
                return (Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT");
            }
            _recorded = facts;
            return LoseTheAnswer ? (Outcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT") : evidence(asked);
        }
    }

    private async Task RecoverAsync(ServiceProvider services, GlobalProductIdentityWorkflowOperation operation)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(operation.TenantId);
        await scope.ServiceProvider.GetRequiredService<GlobalProductIdentityWorkflowProcessor>()
            .RecoverAsync(operation, "worker", Lease, Retry);
    }

    private async Task<GlobalProduct> ProductAsync(GlobalProductIdentityWorkflowOperation operation) =>
        await _database.GetCollection<GlobalProduct>("mdm_global_products")
            .Find(item => item.TenantId == operation.TenantId && item.Id == operation.GlobalProductId).SingleAsync();

    private static ProductIdentityWorkflowCancellationPreflight PreflightOpen(
        object request, int instanceVersion = 3, int taskVersion = 5)
    {
        var asked = (ProductIdentityWorkflowCancellationPreflightRequest)request;
        return new(asked.WorkflowInstanceId, asked.ApprovalTaskId, asked.ExpectedObjectType, asked.ExpectedObjectId,
            "GP-REF", instanceVersion, taskVersion, "Active", "WaitingApproval");
    }

    private ProductIdentityWorkflowTerminalEvidence Approved(GlobalProductIdentityWorkflowOperation operation) => new(
        operation.WorkflowInstanceId!.Value, operation.ApprovalTaskId!.Value, operation.WorkflowTemplateId!.Value,
        operation.WorkflowTemplateVersionId!.Value, "GlobalProduct", operation.ObjectId, operation.ObjectRef,
        "Approve", _approverId.ToString("D"), "APPROVED", _clock.GetUtcNow(), 2, "Approved", "Completed", "corr");

    private ProductIdentityWorkflowCancellationEvidence Cancelled(ProductIdentityWorkflowCancellationRequest request) => new(
        request.WorkflowInstanceId, request.ApprovalTaskId, _templateId, _templateVersionId,
        request.ExpectedObjectType, request.ExpectedObjectId, "GP-REF", "Cancel", _makerId,
        request.ReasonCode, request.Comment, _clock.GetUtcNow(), 3, Guid.NewGuid(), "Cancelled",
        "Cancelled", 4, 6, false, "corr");

    private GlobalProductIdentityWorkflowOperationRepository Repository(Guid tenantId) =>
        new(_database, new Tenant(tenantId));

    private async Task<List<GlobalProductIdentityWorkflowOperation>> StoredAsync(
        IEnumerable<GlobalProductIdentityWorkflowOperation> operations)
    {
        var stored = new List<GlobalProductIdentityWorkflowOperation>();
        foreach (var operation in operations)
        {
            stored.Add((await Repository(operation.TenantId).GetByOperationIdAsync(operation.OperationId))!);
        }
        return stored;
    }

    private static async Task ScheduleAsync(
        GlobalProductIdentityWorkflowOperationRepository repository,
        GlobalProductIdentityWorkflowOperation operation,
        DateTimeOffset? nextAttempt)
    {
        var stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        var now = stored.UpdatedAtUtcTicksV1 + 1;
        var claim = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [stored.Checkpoint], "seed-schedule", now, now + Lease.Ticks, IgnoreRetrySchedule: true));
        Assert.NotNull(claim);
        Assert.True(await repository.AdvanceAsync(claim!, new(stored.Checkpoint,
            ProductIdentityWorkflowRecoveryDisposition.Retryable, now + 1, nextAttempt?.UtcTicks, ReleaseLease: true)));
    }

    private async Task<GlobalProductIdentityWorkflowOperation> SeedAsync(
        Guid tenantId, Checkpoint target, bool withPendingProduct = false, bool withDraftProduct = false)
    {
        var productId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        var startLogId = Guid.NewGuid();
        var objectRef = $"GP-{productId:N}"[..20];
        var operation = new GlobalProductIdentityWorkflowOperation
        {
            Id = operationId,
            TenantId = tenantId,
            OperationId = operationId,
            GlobalProductId = productId,
            ExpectedProductVersion = 0,
            MakerSubjectId = _makerId,
            WorkflowTemplateId = _templateId,
            CandidatePrincipalIds = [_approverId],
            ReasonCode = "IDENTITY_APPROVAL",
            CommentRequired = true,
            EvidenceRequired = true,
            ObjectType = "GlobalProduct",
            ObjectId = productId.ToString("D"),
            ObjectRef = objectRef,
            StartIdempotencyKey = $"resilience:{operationId:D}",
            OperationFingerprint = $"fingerprint-{operationId:N}",
            CreatedAtUtcTicksV1 = _clock.GetUtcNow().UtcTicks
        };
        if (withPendingProduct || withDraftProduct)
        {
            await _database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(new()
            {
                Id = productId,
                TenantId = tenantId,
                CanonicalCode = objectRef,
                GlobalProductName = $"Resilience {productId:N}",
                GlobalProductNameNormalized = $"RESILIENCE {productId:N}".ToUpperInvariant(),
                LifecycleStatus = withPendingProduct
                    ? ProductIdentityLifecycleStatus.PendingIdentityApproval
                    : ProductIdentityLifecycleStatus.Draft,
                WorkflowBinding = withPendingProduct
                    ? new ProductIdentityWorkflowBinding
                    {
                        WorkflowInstanceId = instanceId,
                        WorkflowTemplateId = _templateId,
                        WorkflowTemplateVersionId = _templateVersionId,
                        ApprovalTaskId = taskId,
                        AssignmentSnapshotId = snapshotId,
                        StartTransitionLogId = startLogId,
                        ObjectType = "GlobalProduct",
                        ObjectId = productId,
                        ObjectRef = objectRef,
                        SubmitterSubjectId = _makerId,
                        StartIdempotencyKey = operation.StartIdempotencyKey,
                        StartRequestFingerprint = operation.OperationFingerprint,
                        SubmittedAtUtc = _clock.GetUtcNow()
                    }
                    : null,
                Version = withPendingProduct ? 1 : 0
            });
        }

        var repository = Repository(tenantId);
        Assert.True((await repository.ReserveAsync(operation)).Succeeded);
        Checkpoint[] path = target switch
        {
            Checkpoint.StartOutcomeUnknown => [Checkpoint.StartOutcomeUnknown],
            Checkpoint.AwaitingMakerReplay => [Checkpoint.StartOutcomeUnknown, Checkpoint.AwaitingMakerReplay],
            Checkpoint.AwaitingDecision =>
                [Checkpoint.StartOutcomeUnknown, Checkpoint.WorkflowStarted, Checkpoint.LocalPendingApplied, Checkpoint.AwaitingDecision],
            Checkpoint.WithdrawalRequested =>
                [Checkpoint.StartOutcomeUnknown, Checkpoint.WorkflowStarted, Checkpoint.LocalPendingApplied,
                    Checkpoint.AwaitingDecision, Checkpoint.WithdrawalRequested],
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };
        var now = _clock.GetUtcNow().UtcTicks - TimeSpan.FromMinutes(5).Ticks;
        var current = Checkpoint.Prepared;
        foreach (var next in path)
        {
            var claim = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
                [current], $"seed-{(int)next}", now++, now + Lease.Ticks));
            Assert.NotNull(claim);
            var started = next is not (Checkpoint.StartOutcomeUnknown or Checkpoint.AwaitingMakerReplay);
            Assert.True(await repository.AdvanceAsync(claim!, new(
                next,
                next == Checkpoint.AwaitingMakerReplay
                    ? ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay
                    : ProductIdentityWorkflowRecoveryDisposition.None,
                now++,
                WorkflowInstanceId: started ? instanceId : null,
                WorkflowTemplateId: started ? _templateId : null,
                WorkflowTemplateVersionId: started ? _templateVersionId : null,
                ApprovalTaskId: started ? taskId : null,
                AssignmentSnapshotId: started ? snapshotId : null,
                StartTransitionLogId: started ? startLogId : null,
                WorkflowStartedAtUtcTicksV1: started ? operation.CreatedAtUtcTicksV1 : null,
                WithdrawalCommandId: next == Checkpoint.WithdrawalRequested ? Guid.NewGuid() : null,
                ReleaseLease: true)));
            current = next;
        }

        return (await repository.GetByOperationIdAsync(operationId))!;
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(DateTimeOffset.UtcNow.UtcTicks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class Readiness : IProductIdentityWorkflowTransportReadiness
    {
        public string? Fault { get; set; }
        public string? FindConfigurationFault() => Fault;
    }

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }

    // Answers by tenant and counts by tenant, so another class's leftover tenant in the shared test database can
    // neither be miscounted nor trip a breaker: an unknown tenant is simply told "not decided yet".
    private sealed class Transport : IProductIdentityWorkflowClient
    {
        private readonly Dictionary<Guid, int> _calls = [];
        // A responder answers either (Outcome, code) — a failure — or the value a success carries.
        public Func<Guid, object, object>? Start { get; set; }
        public Func<Guid, object, object>? StartResult { get; set; }
        public Func<Guid, object, object>? Terminal { get; set; }
        public Func<Guid, object, object>? Preflight { get; set; }
        public Func<Guid, object, object>? Cancel { get; set; }

        public int Calls(Guid tenantId) => _calls.GetValueOrDefault(tenantId);

        private Task<ProductIdentityWorkflowTransportResult<T>> Answer<T>(
            Guid tenantId, object request, Func<Guid, object, object>? responder)
        {
            _calls[tenantId] = _calls.GetValueOrDefault(tenantId) + 1;
            var answer = responder?.Invoke(tenantId, request)
                ?? (Outcome.NonTerminal, "WORKFLOW_DECISION_NOT_TERMINAL");
            return Task.FromResult(answer is T value
                ? ProductIdentityWorkflowTransportResult<T>.Success(value)
                : ProductIdentityWorkflowTransportResult<T>.Fail(
                    ((ValueTuple<Outcome, string>)answer).Item1, ((ValueTuple<Outcome, string>)answer).Item2));
        }

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> StartAsync(
            Guid tenantId, ProductIdentityWorkflowStartRequest request, string delegatedUserToken,
            CancellationToken cancellationToken = default) =>
            Answer<ProductIdentityWorkflowStartResult>(tenantId, request, Start);

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> GetStartResultAsync(
            Guid tenantId, ProductIdentityWorkflowStartResultRequest request,
            CancellationToken cancellationToken = default) =>
            Answer<ProductIdentityWorkflowStartResult>(tenantId, request, StartResult);

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>> GetTerminalEvidenceAsync(
            Guid tenantId, ProductIdentityWorkflowTerminalEvidenceRequest request,
            CancellationToken cancellationToken = default) =>
            Answer<ProductIdentityWorkflowTerminalEvidence>(tenantId, request, Terminal);

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>> GetCancellationPreflightAsync(
            Guid tenantId, ProductIdentityWorkflowCancellationPreflightRequest request,
            CancellationToken cancellationToken = default) =>
            Answer<ProductIdentityWorkflowCancellationPreflight>(tenantId, request, Preflight);

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationEvidence>> CancelAsync(
            Guid tenantId, ProductIdentityWorkflowCancellationRequest request, string delegatedUserToken,
            CancellationToken cancellationToken = default) =>
            Answer<ProductIdentityWorkflowCancellationEvidence>(tenantId, request, Cancel);
    }

    private sealed class Log : ILoggerFactory, ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public void AddProvider(ILoggerProvider provider) { }
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (Entries)
            {
                Entries.Add((logLevel, formatter(state, exception) + (exception is null ? string.Empty : " " + exception)));
            }
        }
    }
}
