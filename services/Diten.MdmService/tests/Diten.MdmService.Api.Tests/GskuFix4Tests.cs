using System.Net;
using System.Reflection;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>WP-MDM-GSKU-ACCEPT-01 FIX4, on this host's throwaway mongod (BL-527).</summary>
public sealed partial class GskuLifecycleHttpTests
{
    // ── Item 1 (b): a stuck correction's way out is its requester's same command ────────────────────────────────

    [Theory]
    [InlineData("decided")]
    [InlineData("open")]
    [InlineData("closed")]
    public async Task F4_A_stuck_correction_is_settled_by_its_requesters_same_command(string engine)
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        Assert.True((await CorrectAsync(approved.Id, approved.Version, 4m, key)).IsSuccessStatusCode);
        var instance = host.Engine.Latest("GskuCorrection");
        await StuckCorrectionAsync(approved.Id, "GSKU_CORRECTION_GSKU_DRIFT");
        var corrections = new GskuCorrectionWorkflowOperationRepository(host.Database, Tenant());
        Assert.Equal(1, await corrections.CountManualReconciliationAsync()); // stuck: counted (item 4)
        var detail = await RawDetailAsync(approved.Id);
        Assert.Equal("STUCK", detail.GetProperty("lifecycleRequestState").GetString());
        Assert.Contains("REPEAT_REQUEST", detail.GetProperty("availableActions").EnumerateArray().Select(x => x.GetString()));
        if (engine == "decided") host.Engine.Approve(instance, host.Approver);
        if (engine == "closed") host.Engine.CloseWithoutDecision(instance);

        var repeat = await CorrectAsync(approved.Id, approved.Version, 4m, key);

        var after = await CorrectionOf(approved.Id);
        switch (engine)
        {
            case "decided":
                Assert.True(repeat.IsSuccessStatusCode, await repeat.Content.ReadAsStringAsync());
                Assert.Equal(GskuCorrectionWorkflowCheckpoint.Completed, after.Checkpoint);
                Assert.Equal(4m, (await DetailAsync(approved.Id)).Quantity);
                break;
            case "open":
                Assert.Equal(HttpStatusCode.Accepted, repeat.StatusCode);
                Assert.Equal(GskuCorrectionWorkflowCheckpoint.AwaitingDecision, after.Checkpoint);
                break;
            default:
                Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
                Assert.Equal("GSKU_CORRECTION_CLOSED_WITHOUT_DECISION", Envelope.FirstError(await Envelope.ReadAsync(repeat)));
                Assert.Contains("REQUEST_CORRECTION", (await DetailAsync(approved.Id)).Actions);
                Assert.Equal(0, await corrections.CountManualReconciliationAsync()); // ended: not counted (item 4)
                break;
        }
    }

    // No approval on record and the engine never received the start: the requester's repeat ends it and frees the GSKU.
    [Fact]
    public async Task F4_A_stuck_correction_whose_start_never_arrived_is_ended_by_its_requesters_repeat()
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.False((await CorrectAsync(approved.Id, approved.Version, 4m, key)).IsSuccessStatusCode);
        await StuckCorrectionAsync(approved.Id, GskuCorrectionWorkflowProcessor.RetryLimitReached);

        var repeat = await CorrectAsync(approved.Id, approved.Version, 4m, key);

        Assert.Equal("GSKU_CORRECTION_START_NOT_RECEIVED", Envelope.FirstError(await Envelope.ReadAsync(repeat)));
        Assert.Contains("REQUEST_CORRECTION", (await DetailAsync(approved.Id)).Actions);
    }

    // The same way out on a retirement request (RRP).
    [Fact]
    public async Task F4_A_stuck_retirement_request_is_settled_by_its_requesters_same_command()
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        Assert.True((await RetireAsync(approved.Id, approved.Version, key)).IsSuccessStatusCode);
        var rows = host.Database.GetCollection<GskuRetirementRequestOperation>(GskuRetirementRequestOperationRepository.CollectionName);
        await rows.UpdateOneAsync(x => x.GskuId == approved.Id, Builders<GskuRetirementRequestOperation>.Update
            .Set(x => x.Checkpoint, GskuRetirementRequestCheckpoint.ManualReconciliationRequired)
            .Set(x => x.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
            .Set(x => x.LastFailureCode, "GSKU_RETIREMENT_REQUEST_GSKU_DRIFT"));
        host.Engine.Approve(host.Engine.Latest(GskuRetirementRequestWorkflowStartRequestFactory.ObjectType), host.Approver);

        var repeat = await RetireAsync(approved.Id, approved.Version, key);

        Assert.True(repeat.IsSuccessStatusCode, await repeat.Content.ReadAsStringAsync());
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, (await DetailAsync(approved.Id)).Status);
    }

    // Only the requester is offered (and served) the way out: another user of the same tenant, or the requester with
    // another version, makes another command — refused, and the stuck request stays as it is.
    [Fact]
    public async Task F4_Another_user_or_a_stale_version_does_not_reach_the_stuck_request()
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        Assert.True((await CorrectAsync(approved.Id, approved.Version, 4m, key)).IsSuccessStatusCode);
        await StuckCorrectionAsync(approved.Id, "GSKU_CORRECTION_GSKU_DRIFT");
        var other = host.Token(_tenant, Guid.NewGuid(), MakerPermissions);

        var otherDetail = (await Envelope.ReadAsync(await host.Client(other).GetAsync($"/api/gskus/{approved.Id:D}")))
            .GetProperty("data");
        var otherUser = await SendAsync(HttpMethod.Post, $"/api/gskus/{approved.Id:D}/correction-requests",
            new { expectedGskuVersion = approved.Version, packQuantity = 4m, packUomCode = "C62" }, other, key);
        var staleVersion = await CorrectAsync(approved.Id, approved.Version - 1, 4m, Guid.NewGuid());

        Assert.DoesNotContain("REPEAT_REQUEST", otherDetail.GetProperty("availableActions").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal("STUCK", otherDetail.GetProperty("lifecycleRequestState").GetString());
        Assert.False(otherUser.IsSuccessStatusCode);
        Assert.False(staleVersion.IsSuccessStatusCode);
        var still = await CorrectionOf(approved.Id);
        Assert.Equal(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired, still.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, still.RecoveryDisposition);
    }

    // Two requests racing on the stuck request: the one that finds the lease held is told "busy" and does NOT give
    // back a lease that is not its own.
    [Fact]
    public async Task F4_A_repeat_that_finds_the_lease_held_leaves_it_to_its_owner()
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        Assert.True((await CorrectAsync(approved.Id, approved.Version, 4m, key)).IsSuccessStatusCode);
        await StuckCorrectionAsync(approved.Id, "GSKU_CORRECTION_GSKU_DRIFT");
        var held = DateTimeOffset.UtcNow.AddMinutes(1).UtcTicks;
        await host.Database.GetCollection<GskuCorrectionWorkflowOperation>(GskuCorrectionWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(x => x.GskuId == approved.Id, Builders<GskuCorrectionWorkflowOperation>.Update
                .Set(x => x.LeaseOwner, "the-other-request").Set(x => x.LeaseUntilUtcTicksV1, held));

        var repeat = await CorrectAsync(approved.Id, approved.Version, 4m, key);

        Assert.Equal("GSKU_CORRECTION_BUSY", Envelope.FirstError(await Envelope.ReadAsync(repeat)));
        var after = await CorrectionOf(approved.Id);
        Assert.Equal("the-other-request", after.LeaseOwner);
        Assert.Equal(held, after.LeaseUntilUtcTicksV1);
    }

    // ── Item 2: the maker's repeats neither count nor jump the back-off ─────────────────────────────────────────

    [Fact]
    public async Task F4_Thirty_repeats_of_the_same_request_do_not_quarantine_it()
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_START_IDEMPOTENCY_CONFLICT", false);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict; // Platform refuses the lookup (409)
        host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
        try
        {
            Assert.False((await CorrectAsync(approved.Id, approved.Version, 4m, key)).IsSuccessStatusCode);
            for (var repeat = 0; repeat < 30; repeat++)
            {
                // Each repeat finds the request due (the back-off is checked on its own below): only the count is measured.
                await DueNowAsync(approved.Id);
                Assert.False((await CorrectAsync(approved.Id, approved.Version, 4m, key)).IsSuccessStatusCode);
            }

            var operation = await CorrectionOf(approved.Id);
            Assert.Equal(0, operation.FailedAttemptCount);
            Assert.Equal(GskuCorrectionWorkflowCheckpoint.StartOutcomeUnknown, operation.Checkpoint);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    // A repeat before the worker's next attempt is due is answered without touching the request: the back-off holds.
    [Fact]
    public async Task F4_A_repeat_before_the_next_attempt_does_not_jump_the_back_off()
    {
        var approved = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_START_IDEMPOTENCY_CONFLICT", false);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Conflict; // Platform refuses the lookup (409)
        host.Engine.LookupFailureCode = "WORKFLOW_START_RECOVERY_CONFLICT";
        try
        {
            Assert.False((await CorrectAsync(approved.Id, approved.Version, 4m, key)).IsSuccessStatusCode);
            var due = DateTimeOffset.UtcNow.AddMinutes(5).UtcTicks;
            await host.Database.GetCollection<GskuCorrectionWorkflowOperation>(GskuCorrectionWorkflowOperationRepository.CollectionName)
                .UpdateOneAsync(x => x.GskuId == approved.Id,
                    Builders<GskuCorrectionWorkflowOperation>.Update.Set(x => x.NextAttemptAtUtcTicksV1, due));
            var before = await CorrectionOf(approved.Id);

            var repeat = await CorrectAsync(approved.Id, approved.Version, 4m, key);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, repeat.StatusCode);
            var after = await CorrectionOf(approved.Id);
            Assert.Equal(due, after.NextAttemptAtUtcTicksV1);
            Assert.Equal(before.Version, after.Version);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    private Task DueNowAsync(Guid gsku) =>
        host.Database.GetCollection<GskuCorrectionWorkflowOperation>(GskuCorrectionWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(x => x.GskuId == gsku, Builders<GskuCorrectionWorkflowOperation>.Update
                .Set(x => x.NextAttemptAtUtcTicksV1, DateTimeOffset.UtcNow.AddSeconds(-1).UtcTicks));

    // ── Item 3: the streak is counted in the store ($inc), not written from a snapshot ──────────────────────────

    [Fact]
    public async Task F4_Two_workers_with_the_same_old_snapshot_both_count()
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", true);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Timeout;
        try
        {
            Assert.False((await RequestCorrectionAsync(approved.Id, approved.Version, 4m)).IsSuccessStatusCode);
            var snapshot = await CorrectionOf(approved.Id);
            var execution = new GskuCorrectionExecutionConfiguration(TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100));

            await CorrectionProcessor(new VersionedReferences(), crashAfterApply: false).RecoverAsync(snapshot, "worker-a", execution, default);
            await CorrectionProcessor(new VersionedReferences(), crashAfterApply: false).RecoverAsync(snapshot, "worker-b", execution, default);

            Assert.Equal(2, (await CorrectionOf(approved.Id)).FailedAttemptCount);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    // ── Item 7: the same submit sent twice — the loser gets the winner's answer ─────────────────────────────────

    // The same submit sent twice at once: the other one's transaction holds the reservation, this one meets a write
    // conflict before the other commits. Re-reading at once finds nothing (FIX3: 503); it tries again briefly and gets
    // the other one's reservation as its answer.
    [Fact]
    public async Task F4_A_submit_that_loses_to_the_same_submit_gets_the_replay()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        var plain = host.Database;
        var stored = await plain.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == gsku).SingleAsync();
        var revision = await plain.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
            .Find(x => x.Id == stored.ProductDefinitionRevisionId).SingleAsync();
        var operation = ReservableOperation(stored, revision, version);
        // The other submit's reservation exactly as the store writes it: reserved once, then taken back out.
        var shaped = await new FirstGskuIdentityWorkflowOperationRepository(plain, Tenant()).ReserveAsync(Clone(operation));
        Assert.True(shaped.Succeeded, shaped.ErrorCode);
        var reserved = await plain.GetCollection<FirstGskuIdentityWorkflowOperation>(Operations)
            .Find(x => x.OperationId == operation.OperationId).SingleAsync();
        await plain.GetCollection<FirstGskuIdentityWorkflowOperation>(Operations).DeleteOneAsync(x => x.Id == reserved.Id);
        await plain.GetCollection<Gsku>("mdm_gskus").UpdateOneAsync(x => x.Id == gsku,
            Builders<Gsku>.Update.Inc(x => x.IdentitySubmitReservations, -1));
        Task? committed = null;
        var hooked = Hooked("insert", Operations, () => committed = CommitLater(plain, session =>
        {
            plain.GetCollection<FirstGskuIdentityWorkflowOperation>(Operations).InsertOne(session, reserved);
            plain.GetCollection<Gsku>("mdm_gskus").UpdateOne(session, x => x.Id == gsku,
                Builders<Gsku>.Update.Inc(x => x.IdentitySubmitReservations, 1));
        }));

        var loser = await new FirstGskuIdentityWorkflowOperationRepository(hooked, Tenant()).ReserveAsync(Clone(operation));
        await committed!;

        Assert.True(loser.Succeeded, loser.ErrorCode);
        Assert.True(loser.IsReplay);
        Assert.Equal(1, await plain.GetCollection<FirstGskuIdentityWorkflowOperation>(Operations)
            .CountDocumentsAsync(x => x.OperationId == operation.OperationId));
    }

    [Fact]
    public async Task F4_A_global_product_submit_that_loses_to_the_same_submit_gets_the_replay()
    {
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var plain = host.Database;
        var operation = ProductOperation(product, 0, GlobalProductIdentityWorkflowCheckpoint.Prepared, null);
        var operations = plain.GetCollection<GlobalProductIdentityWorkflowOperation>(
            GlobalProductIdentityWorkflowOperationRepository.CollectionName);
        Task? committed = null;
        var hooked = Hooked("insert", GlobalProductIdentityWorkflowOperationRepository.CollectionName, () =>
            committed = CommitLater(plain, session =>
            {
                operations.InsertOne(session, CloneProductOperation(operation));
                plain.GetCollection<GlobalProduct>(GlobalProducts).UpdateOne(session, x => x.Id == product,
                    Builders<GlobalProduct>.Update.Inc(x => x.IdentitySubmitReservations, 1));
            }));

        var loser = await new GlobalProductIdentityWorkflowOperationRepository(hooked, Tenant())
            .ReserveAsync(CloneProductOperation(operation));
        await committed!;

        Assert.True(loser.Succeeded, loser.ErrorCode);
        Assert.True(loser.IsReplay);
        Assert.Equal(1, await operations.CountDocumentsAsync(x => x.OperationId == operation.OperationId));
    }

    // The other submit: its writes in a transaction that commits a moment later, while this one is already conflicting.
    private static Task CommitLater(IMongoDatabase database, Action<IClientSessionHandle> writes)
    {
        var session = database.Client.StartSession();
        session.StartTransaction();
        writes(session);
        return Task.Run(async () =>
        {
            await Task.Delay(300);
            await session.CommitTransactionAsync();
            session.Dispose();
        });
    }

    // ── Item 9: an overflowing page is a 400, not a negative skip and a 500 ─────────────────────────────────────

    [Fact]
    public async Task F4_A_page_number_beyond_the_bound_is_refused()
    {
        var response = await host.Client(MakerToken()).GetAsync($"/api/gskus?pageNumber={Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries.GetGskusQuery.MaxPageNumber + 1}&pageSize=100");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Item 10: FIX2's parked requests (AwaitingMakerReplay, the lock held) are settled by the worker ──────────

    [Fact]
    public async Task F4_A_request_FIX2_parked_holding_the_lock_is_settled_by_the_worker()
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartThrows = true;
        Assert.False((await RequestCorrectionAsync(approved.Id, approved.Version, 4m)).IsSuccessStatusCode);
        await host.Database.GetCollection<GskuCorrectionWorkflowOperation>(GskuCorrectionWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(x => x.GskuId == approved.Id, Builders<GskuCorrectionWorkflowOperation>.Update
                .Set(x => x.Checkpoint, GskuCorrectionWorkflowCheckpoint.AwaitingMakerReplay)
                .Set(x => x.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay)
                .Set(x => x.LastFailureCode, "GSKU_CORRECTION_MAKER_REPLAY_REQUIRED"));
        Assert.DoesNotContain("REQUEST_CORRECTION", (await DetailAsync(approved.Id)).Actions);

        await CorrectionCyclesUntilAsync(approved.Id, detail => detail.Actions.Contains("REQUEST_CORRECTION"));

        Assert.Equal("GSKU_CORRECTION_START_NOT_RECEIVED", (await CorrectionOf(approved.Id)).LastFailureCode);
    }

    // ── Item 12: rules FIX3 left untested ───────────────────────────────────────────────────────────────────────

    // B14 on the retirement request: a step that throws gives its lease back.
    [Fact]
    public async Task F4_A_retirement_request_step_that_throws_gives_its_lease_back()
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RetireAsync(approved.Id, approved.Version, Guid.NewGuid())).IsSuccessStatusCode);
        host.Engine.NextTerminalEvidenceThrows = true;

        await host.RunAsync<Diten.MdmService.Api.Services.ProductItemSkuMaster.GskuRetirementRequestRecoveryRunner>(
            runner => runner.RunCycleAsync());

        var operation = await RetirementOf(approved.Id);
        Assert.Null(operation.LeaseOwner);
        Assert.False(host.Engine.NextTerminalEvidenceThrows);
    }

    // A step whose write is refused gives its lease back — the correction, the retirement request and the pair
    // retirement (FIX3 had only the identity flow tested).
    [Fact]
    public async Task F4_A_correction_whose_write_is_refused_gives_its_lease_back()
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RequestCorrectionAsync(approved.Id, approved.Version, 4m)).IsSuccessStatusCode);
        var tenant = Tenant();
        var refusing = DispatchProxy.Create<IGskuCorrectionWorkflowOperationRepository, RefuseFirstAdvance<IGskuCorrectionWorkflowOperationRepository>>();
        ((RefuseFirstAdvance<IGskuCorrectionWorkflowOperationRepository>)(object)refusing).Inner =
            new GskuCorrectionWorkflowOperationRepository(host.Database, tenant);
        var processor = new GskuCorrectionWorkflowProcessor(refusing, new GskuRepository(host.Database, tenant),
            new ProductDefinitionRevisionRepository(host.Database, tenant), host.Engine, new VersionedReferences(),
            new GskuCorrectionWorkflowStartRequestFactory(new(null, "GSKU-CORRECTION", [host.Approver], "GSKU_CORRECTION",
                false, false, null, null, "GSKU-IDENTITY"), TimeProvider.System),
            TimeProvider.System);

        var result = await processor.RecoverAsync(await CorrectionOf(approved.Id), "refused",
            new GskuCorrectionExecutionConfiguration(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);

        Assert.Equal("GSKU_CORRECTION_CONCURRENCY_CONFLICT", result.ErrorCode);
        Assert.Null((await CorrectionOf(approved.Id)).LeaseOwner);
    }

    [Fact]
    public async Task F4_A_pair_retirement_whose_write_is_refused_gives_its_lease_back()
    {
        var approved = await ApprovedGskuAsync();
        var tenant = Tenant();
        var operations = new FirstGskuIdentityRetirementOperationRepository(host.Database, tenant);
        var refusing = DispatchProxy.Create<IFirstGskuIdentityRetirementOperationRepository, RefuseFirstAdvance<IFirstGskuIdentityRetirementOperationRepository>>();
        ((RefuseFirstAdvance<IFirstGskuIdentityRetirementOperationRepository>)(object)refusing).Inner = operations;
        var processor = new FirstGskuIdentityRetirementProcessor(refusing, new GskuRepository(host.Database, tenant),
            new ProductDefinitionRevisionRepository(host.Database, tenant), TimeProvider.System);
        var operationId = Guid.NewGuid();

        var result = await processor.StartAsync(approved.Id, approved.Version, operationId, host.Approver,
            "PRODUCT_DISCONTINUED", "pair-owner", TimeSpan.FromMinutes(1));

        Assert.False(result.Succeeded);
        Assert.Null((await operations.GetByOperationIdAsync(operationId))?.LeaseOwner);
    }

    // FIX3 S3 and S8 on the retirement request.
    [Fact]
    public async Task F4_A_retirement_request_cut_off_before_its_start_is_ended_by_the_worker()
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartThrows = true;
        Assert.False((await RetireAsync(approved.Id, approved.Version, Guid.NewGuid())).IsSuccessStatusCode);

        await RetirementCyclesUntilAsync(approved.Id, detail => detail.Actions.Contains("REQUEST_RETIREMENT"));

        Assert.Equal("GSKU_RETIREMENT_REQUEST_START_NOT_RECEIVED", (await RetirementOf(approved.Id)).LastFailureCode);
    }

    [Fact]
    public async Task F4_A_retirement_requests_nothing_arrived_is_not_trusted_inside_the_settle_window()
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "PRODUCT_WORKFLOW_TIMEOUT", false);
        Assert.False((await RetireAsync(approved.Id, approved.Version, Guid.NewGuid())).IsSuccessStatusCode);
        await Task.Delay(1100);
        await host.RunAsync<Diten.MdmService.Api.Services.ProductItemSkuMaster.GskuRetirementRequestRecoveryRunner>(
            runner => runner.RunCycleAsync());

        Assert.Equal(GskuRetirementRequestWorkflowProcessor.StartSettling, (await RetirementOf(approved.Id)).LastFailureCode);
        Assert.DoesNotContain("REQUEST_RETIREMENT", (await DetailAsync(approved.Id)).Actions);
        await RetirementCyclesUntilAsync(approved.Id, detail => detail.Actions.Contains("REQUEST_RETIREMENT"));
    }

    // Item 8: a ceiling below the retry delay (valid before FIX3) is raised, not a failure of every cycle.
    [Fact]
    public void F4_A_ceiling_below_the_retry_delay_is_raised_not_refused()
    {
        var correction = new Diten.MdmService.Api.Configuration.GskuCorrectionWorkflowWorkerOptions
        {
            RetryDelaySeconds = 60, MaxRetryDelaySeconds = 30
        };
        var retirement = new Diten.MdmService.Api.Configuration.GskuRetirementRequestWorkflowWorkerOptions
        {
            RetryDelaySeconds = 60, MaxRetryDelaySeconds = 30
        };

        Assert.True(correction.IsValid());
        Assert.True(retirement.IsValid());
        Assert.Equal(60, correction.EffectiveMaxRetryDelaySeconds);
        Assert.Equal(60, retirement.EffectiveMaxRetryDelaySeconds);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> CorrectAsync(Guid gsku, int version, decimal quantity, Guid key) =>
        SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/correction-requests",
            new { expectedGskuVersion = version, packQuantity = quantity, packUomCode = "C62" }, MakerToken(), key);

    private Task<HttpResponseMessage> RetireAsync(Guid gsku, int version, Guid key) =>
        SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/retirement-requests",
            new { expectedGskuVersion = version, requestReason = "PRODUCT_DISCONTINUED" }, MakerToken(), key);

    private Task StuckCorrectionAsync(Guid gsku, string code) =>
        host.Database.GetCollection<GskuCorrectionWorkflowOperation>(GskuCorrectionWorkflowOperationRepository.CollectionName)
            .UpdateOneAsync(x => x.GskuId == gsku, Builders<GskuCorrectionWorkflowOperation>.Update
                .Set(x => x.Checkpoint, GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired)
                .Set(x => x.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
                .Set(x => x.LastFailureCode, code)
                .Set(x => x.LeaseOwner, null)
                .Set(x => x.LeaseUntilUtcTicksV1, null));

    private Task<GskuRetirementRequestOperation> RetirementOf(Guid gsku) =>
        host.Database.GetCollection<GskuRetirementRequestOperation>(GskuRetirementRequestOperationRepository.CollectionName)
            .Find(x => x.GskuId == gsku).SortByDescending(x => x.CreatedAtUtcTicksV1).FirstAsync();

    private static FirstGskuIdentityWorkflowOperation Clone(FirstGskuIdentityWorkflowOperation operation) =>
        MongoDB.Bson.Serialization.BsonSerializer.Deserialize<FirstGskuIdentityWorkflowOperation>(
            MongoDB.Bson.BsonExtensionMethods.ToBsonDocument(operation));

    private static GlobalProductIdentityWorkflowOperation CloneProductOperation(GlobalProductIdentityWorkflowOperation operation) =>
        MongoDB.Bson.Serialization.BsonSerializer.Deserialize<GlobalProductIdentityWorkflowOperation>(
            MongoDB.Bson.BsonExtensionMethods.ToBsonDocument(operation));

    // Refuses the first AdvanceAsync (as a concurrent writer would); every other call goes to the real repository.
    public class RefuseFirstAdvance<T> : DispatchProxy where T : class
    {
        public T Inner { get; set; } = null!;
        private int _refused;

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == "AdvanceAsync" && Interlocked.Exchange(ref _refused, 1) == 0)
                return Task.FromResult(false);
            return method.Invoke(Inner, args);
        }
    }
}
