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

        // The worker's lookup then learns that nothing arrived. That is not the chain answering the START: the
        // operation is the maker's again and the fault is not forgotten.
        _clock.Advance(TimeSpan.FromMinutes(1));
        _transport.StartResult = (_, _) => (Outcome.NotFound, "NOT_FOUND_NON_LEAKAGE");
        await RecoverAsync(services, stored);

        stored = (await repository.GetByOperationIdAsync(operation.OperationId))!;
        Assert.Equal(Checkpoint.AwaitingMakerReplay, stored.Checkpoint);
        Assert.Equal(1, stored.ServiceFaultCount);
        Assert.Equal(since, stored.ServiceFaultSinceUtcTicksV1);
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
        Assert.Null(stored.NextAttemptAtUtcTicksV1);

        // The requester gives up. The approver approves at the engine. The worker's next cycle — the real runner,
        // the real discovery — finds the operation and applies the decision.
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

        // First cycle: the approval is still open, so the withdrawal did not land — it is forgotten, and the
        // decision is polled in the same cycle (not decided yet).
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
        ServiceProvider services, GlobalProductIdentityWorkflowOperation operation, Guid commandId)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(operation.TenantId);
        return await scope.ServiceProvider.GetRequiredService<GlobalProductIdentityWorkflowProcessor>()
            .WithdrawInteractiveAsync(operation.TenantId, operation.GlobalProductId, 1, commandId, _makerId,
                "REQUESTER_WITHDRAWAL", null, "maker-token", Lease, Retry);
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

    private static ProductIdentityWorkflowCancellationPreflight PreflightOpen(object request)
    {
        var asked = (ProductIdentityWorkflowCancellationPreflightRequest)request;
        return new(asked.WorkflowInstanceId, asked.ApprovalTaskId, asked.ExpectedObjectType, asked.ExpectedObjectId,
            "GP-REF", 3, 5, "Active", "WaitingApproval");
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
