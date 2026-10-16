using System.Net;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-LSKU-ACCEPT-01 FIX5 (P-2026-10-07-LSKU-FIX5) over the real host and a throwaway replica-set mongod.
/// </summary>
public sealed partial class LskuFix3HttpTests
{
    // ---------------------------------------------------------------------------------------------------- FIX5 item 1

    // The maker's interactive repeat meets a start refused before processing; the abandon write is refused — the
    // streak does not move (it is not a worker try).
    [Fact]
    public async Task Fix5_Item01_A_refused_abandon_on_the_makers_repeat_does_not_move_the_streak()
    {
        var (lsku, version) = await ParkedNeverSentAsync("FA");
        // One short of the quarantine limit: a COUNTED try here would reach it and quarantine the operation
        // (STUCK_ON_RECORD_ANSWER); the maker's repeat is not counted, so it does not.
        await host.Database.GetCollection<LskuIdentityWorkflowOperation>("mdm_lsku_identity_workflow_operations")
            .UpdateOneAsync(x => x.TenantId == _tenant && x.LskuId == lsku, Builders<LskuIdentityWorkflowOperation>.Update
                .Set(x => x.FailedAttemptCount, ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts - 1));
        var before = await IdentityOperationAsync(lsku, version);
        using var scope = TenantScope();
        var operations = Intercept<ILskuIdentityWorkflowOperationRepository>(
            scope.ServiceProvider.GetRequiredService<ILskuIdentityWorkflowOperationRepository>(),
            (method, args) => method.Name == nameof(ILskuIdentityWorkflowOperationRepository.AbandonBeforeStartAsync)
                ? Task.FromResult(new LskuIdentityWithdrawalWriteResult(false, false, null,
                    "LSKU_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT"))
                : null);
        var processor = ActivatorUtilities.CreateInstance<LskuIdentityWorkflowProcessor>(scope.ServiceProvider, operations);
        // Refused before processing AND quarantine-class (400 VALIDATION_FAILED): the start is abandoned — the path under
        // test (a delegated-user 403 would park it for the maker instead, never reaching the abandon write).
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Invalid, "VALIDATION_FAILED", false);

        await processor.StartInteractiveAsync(_tenant, lsku, version, before.OperationId, _maker, "delegated-maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var after = await IdentityOperationAsync(lsku, version);
        Assert.True(after.UpdatedAtUtcTicksV1 > before.UpdatedAtUtcTicksV1, "the repeat was not processed");
        Assert.Equal(before.FailedAttemptCount, after.FailedAttemptCount);
        Assert.NotEqual(LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, after.Checkpoint);
        Assert.NotEqual(LskuIdentityWorkflowProcessor.StuckOnRecordAnswer, after.LastFailureCode);
    }

    // ---------------------------------------------------------------------------------------------------- FIX5 item 3

    // A legacy stuck submit (no start-sent mark, no instance) its maker repeats: the engine has nothing under its key.
    // Last updated an hour ago (read before the claim) → ended "nothing started"; a minute ago → it stays.
    [Theory]
    [InlineData(60, true)]
    [InlineData(1, false)]
    public async Task Fix5_Item03_Reconciliation_trusts_nothing_started_only_past_the_window_of_the_preclaim_update(
        int minutesAgo, bool ended)
    {
        var (lsku, version) = await ParkedNeverSentAsync(ended ? "FC" : "FD");
        await host.Database.GetCollection<BsonDocument>("mdm_lsku_identity_workflow_operations").UpdateOneAsync(
            new BsonDocument("LskuId", new BsonBinaryData(lsku, GuidRepresentation.Standard)),
            new BsonDocument
            {
                { "$unset", new BsonDocument("LastStartSentAtUtcTicksV1", "") },
                { "$set", new BsonDocument
                    {
                        { "Checkpoint", (int)LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired },
                        { "RecoveryDisposition", (int)ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired },
                        { "LastFailureCode", "LSKU_IDENTITY_WORKFLOW_START_OUTCOME_UNKNOWN" },
                        { "UpdatedAtUtcTicksV1", DateTimeOffset.UtcNow.AddMinutes(-minutesAgo).UtcTicks }
                    }
                }
            });

        // The maker's repeat, on a processor with the production window (2 minutes; the host runs with 2 seconds).
        var stuck = await IdentityOperationAsync(lsku, version);
        using (var scope = TenantScope())
        {
            var execution = scope.ServiceProvider.GetRequiredService<LskuIdentityWorkflowExecutionConfiguration>()
                with { StartSettleWindow = TimeSpan.FromMinutes(2) };
            var processor = ActivatorUtilities.CreateInstance<LskuIdentityWorkflowProcessor>(scope.ServiceProvider, execution);
            await processor.StartInteractiveAsync(_tenant, lsku, version, stuck.OperationId, _maker, "delegated-maker-token",
                TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        }

        var operation = await IdentityOperationAsync(lsku, version);
        if (ended)
        {
            Assert.Equal(LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart, operation.Checkpoint);
            Assert.Equal(LskuIdentityWorkflowProcessor.ReconciledNothingStarted, operation.LastFailureCode);
        }
        else
        {
            Assert.Equal(LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, operation.Checkpoint);
        }
    }

    // ---------------------------------------------------------------------------------------------------- FIX5 item 4

    // A double click: one key, two concurrent requests; Platform stamps each resolution anew (another ResolvedAtUtc) —
    // the same command is recognised by its identity facts: a 201 and its replay, never a 409.
    [Fact]
    public async Task Fix5_Item04_Two_concurrent_requests_of_one_key_are_a_create_and_its_replay_never_a_conflict()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "DC-" + Guid.NewGuid().ToString("N");
        var stamp = 0;

        async Task<int> OneAsync()
        {
            using var scope = TenantScope();
            var markets = Intercept<IVerifiedMarketReferenceResolver>(
                scope.ServiceProvider.GetRequiredService<IVerifiedMarketReferenceResolver>(), (method, args) =>
                {
                    if (method.Name != nameof(IVerifiedMarketReferenceResolver.ResolveLatestAsync)) return null;
                    var real = host.Markets.ResolveLatestAsync((string)args![0]!).GetAwaiter().GetResult();
                    return Task.FromResult(real.Selection is null ? real : VerifiedMarketReferenceResolveResult.Success(
                        real.Selection with { ResolvedAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(Interlocked.Increment(ref stamp)) }));
                });
            var handler = ActivatorUtilities.CreateInstance<
                Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers.CreateLskuDraftHandler>(
                scope.ServiceProvider, markets, new HandlerActor());
            var result = await handler.Handle(CreateCommand(gsku, "DC", key), CancellationToken.None);
            return result.StatusCode;
        }

        var statuses = await Task.WhenAll(OneAsync(), OneAsync());

        Assert.DoesNotContain(409, statuses);
        Assert.Contains(201, statuses);
        Assert.Equal(1L, await host.Database.GetCollection<Lsku>("mdm_lskus")
            .CountDocumentsAsync(x => x.TenantId == _tenant && x.GskuId == gsku && x.MarketCode == "DC"));
    }

    // ---------------------------------------------------------------------------------------------------- FIX5 K5

    // A creation cut off after its code was consumed; its repeat meets a 404 whose reason is NOT Platform's
    // "market not offered" — not a definite refusal: the command's code stays pending (never burned).
    [Fact]
    public async Task Fix5_K5_A_404_with_another_reason_on_the_repeat_never_burns_the_commands_own_code()
    {
        var gsku = await ApprovedGskuAsync();
        var (captured, _) = await CutOffBeforeTheRowAsync(gsku, "FE");
        host.Markets.Refusals["FE"] = (404, "REFERENCE_SET_NOT_FOUND");
        try
        {
            using var scope = TenantScope();
            var repeat = await CreateHandler(scope).Handle(CreateCommand(gsku, "FE", captured.CreationCommandId),
                CancellationToken.None);
            Assert.Equal(404, repeat.StatusCode);
        }
        finally
        {
            host.Markets.Refusals.TryRemove("FE", out _);
        }

        var code = await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.Id == captured.CodeReservationId).SingleAsync();
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, code.BindingState);
    }

    // ---------------------------------------------------------------------------------------------------- FIX5 K3 (store)

    // Measured: the store's EARLY refusal of a move (not allowed / not coherent) kept the lease to its expiry; a move
    // that was to give the lease back now gives it back there too.
    [Fact]
    public async Task Fix5_K3_A_move_the_store_refuses_early_still_gives_the_lease_back()
    {
        var (lsku, version) = await ParkedNeverSentAsync("FF");
        var operation = await IdentityOperationAsync(lsku, version);
        var store = new LskuIdentityWorkflowOperationRepository(host.Database, new Tenant(_tenant));
        var now = DateTimeOffset.UtcNow;
        var claim = await store.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [operation.Checkpoint], "fix5-k3", now.UtcTicks, now.AddMinutes(1).UtcTicks));
        Assert.NotNull(claim);

        // AwaitingMakerReplay -> Completed is not in the store's table: refused before any write.
        var moved = await store.AdvanceAsync(claim!, new(LskuIdentityWorkflowCheckpoint.Completed,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, ReleaseLease: true));

        Assert.False(moved);
        var after = await IdentityOperationAsync(lsku, version);
        Assert.Null(after.LeaseOwner);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay, after.Checkpoint);
    }

    // ---------------------------------------------------------------------------------------------------- FIX5 K1 + CT X8

    // K1: with the identity gauges switched off, the admission count still records.
    [Fact]
    public async Task Fix5_K1_The_admission_count_records_with_the_identity_gauges_switched_off()
    {
        using var metrics = new ProductIdentityRecoveryMetrics();
        var services = host.Factory.Services;
        var admission = new GskuChildAdmissionGauge(services.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            NullLogger<GskuChildAdmissionGauge>.Instance, metrics);
        var worker = new GskuChildAdmissionGaugeWorker(admission,
            Options.Create(new GskuChildAdmissionGaugeOptions { Enabled = true, LskuIdentityEnabled = false }),
            NullLogger<GskuChildAdmissionGaugeWorker>.Instance,
            new LskuIdentityGauge(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<LskuIdentityGauge>.Instance, metrics));

        await worker.RunOnceAsync(CancellationToken.None);

        Assert.True(metrics.Current(GskuChildAdmissionGauge.Flow).HasValue);
        Assert.False(metrics.Current(LskuIdentityWorkflowRecoveryRunner.Flow).HasValue);
    }

    // CT X8: the worker STARTS its loop when only the identity gauges are on (the admission count off).
    [Fact]
    public async Task Fix5_X8_The_gauge_worker_runs_its_loop_with_only_the_identity_gauges_on()
    {
        using var metrics = new ProductIdentityRecoveryMetrics();
        var services = host.Factory.Services;
        var worker = new GskuChildAdmissionGaugeWorker(services.GetRequiredService<GskuChildAdmissionGauge>(),
            Options.Create(new GskuChildAdmissionGaugeOptions { Enabled = false, LskuIdentityEnabled = true, IntervalSeconds = 30 }),
            NullLogger<GskuChildAdmissionGaugeWorker>.Instance,
            new LskuIdentityGauge(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<LskuIdentityGauge>.Instance, metrics));

        await ((IHostedService)worker).StartAsync(CancellationToken.None);
        try
        {
            for (var wait = 0; wait < 50 && !metrics.Current(LskuIdentityWorkflowRecoveryRunner.Flow).HasValue; wait++)
                await Task.Delay(100);
        }
        finally
        {
            await ((IHostedService)worker).StopAsync(CancellationToken.None);
        }

        Assert.True(metrics.Current(LskuIdentityWorkflowRecoveryRunner.Flow).HasValue);
    }

    // ---------------------------------------------------------------------------------------------------- FIX5 K6

    // A commit whose result is unknown (UnknownTransactionCommitResult) is not a refusal: ambiguous (202), the code not
    // burned and the admission kept (formerly a 500).
    [Fact]
    public async Task Fix5_K6_A_commit_with_an_unknown_result_is_ambiguous_and_burns_nothing()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "CU-" + Guid.NewGuid().ToString("N");
        var admin = host.Database.Client.GetDatabase("admin");
        var armed = false;
        var rows = new LskuRepository(host.Database, new Tenant(_tenant))
        {
            BeforeReservationWriteForTests = async () =>
            {
                if (armed) return;
                armed = true;
                await admin.RunCommandAsync<BsonDocument>(new BsonDocument
                {
                    { "configureFailPoint", "failCommand" },
                    { "mode", new BsonDocument("times", 2) },
                    { "data", new BsonDocument
                        {
                            { "failCommands", new BsonArray { "commitTransaction" } },
                            // 91 ShutdownInProgress: a server error only the commit-unknown label classifies (code 50 is
                            // MaxTimeMSExpired, which the driver raises as MongoExecutionTimeoutException — already caught).
                            { "errorCode", 91 },
                            { "errorLabels", new BsonArray { "UnknownTransactionCommitResult" } }
                        }
                    }
                });
            }
        };
        int status;
        try
        {
            using var scope = TenantScope();
            status = (await CreateHandler(scope, lskus: rows).Handle(CreateCommand(gsku, "CU", key), CancellationToken.None)).StatusCode;
        }
        finally
        {
            await admin.RunCommandAsync<BsonDocument>(new BsonDocument
            {
                { "configureFailPoint", "failCommand" }, { "mode", "off" }
            });
        }

        Assert.True(armed);
        Assert.Equal(202, status);
        var code = await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
            .Find(x => x.TenantId == _tenant && x.ReservationCommandId == key.ToUpperInvariant()).SingleAsync();
        Assert.Equal(CodeReservationBindingState.PendingIdentityWrite, code.BindingState);
        Assert.Contains(key.ToUpperInvariant(), await AdmissionsAsync(gsku));
    }
}
