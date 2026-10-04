using System.Net;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>WP-MDM-GSKU-ACCEPT-01 FIX3 addendum, S3–S10, on this host's throwaway mongod (BL-527).</summary>
public sealed partial class GskuLifecycleHttpTests
{
    // ── S3 ───────────────────────────────────────────────────────────────────────────────────────────────────────

    // A correction whose maker's request was cut off after the lock was taken and before the start was sent: the
    // worker does not start it (no maker token), does not park it holding the lock, and — nothing having arrived at
    // the engine — gives the GSKU back and ends the request.
    [Fact]
    public async Task S3_A_correction_cut_off_before_its_start_is_ended_by_the_worker_and_the_gsku_freed()
    {
        var approved = await ApprovedGskuAsync();
        var startsBefore = host.Engine.StartCalls;
        host.Engine.NextStartThrows = true;
        Assert.False((await RequestCorrectionAsync(approved.Id, approved.Version, 4m)).IsSuccessStatusCode);
        var cutOff = await CorrectionOf(approved.Id);
        Assert.Equal(GskuCorrectionWorkflowCheckpoint.Prepared, cutOff.Checkpoint);
        Assert.DoesNotContain("REQUEST_CORRECTION", (await DetailAsync(approved.Id)).Actions);

        var freed = await CorrectionCyclesUntilAsync(approved.Id, detail => detail.Actions.Contains("REQUEST_CORRECTION"));

        var ended = await CorrectionOf(approved.Id);
        Assert.Equal(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired, ended.Checkpoint);
        Assert.Equal("GSKU_CORRECTION_START_NOT_RECEIVED", ended.LastFailureCode);
        Assert.Equal(startsBefore + 1, host.Engine.StartCalls); // the worker never started it
        Assert.Equal(1m, freed.Quantity);
    }

    // ── S4 ───────────────────────────────────────────────────────────────────────────────────────────────────────

    // The maker's repeat holds the submit's lease (its start may be at the engine right now): an edit does not set
    // it aside — it is refused, coded.
    [Fact]
    public async Task S4_An_edit_does_not_set_aside_a_submit_under_a_live_lease()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        var operation = await InsertIdentityOperationAsync(gsku, version, FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
            leaseOwner: "makers-repeat", leaseUntil: DateTimeOffset.UtcNow.AddMinutes(1));

        var edit = await SendAsync(HttpMethod.Put, $"/api/gskus/{gsku:D}",
            new { expectedVersion = version, packQuantity = 3m, packUomCode = "C62" }, MakerToken());

        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal(GskuRepository.SubmitInFlight, Envelope.FirstError(await Envelope.ReadAsync(edit)));
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
            (await IdentityOperationById(operation)).Checkpoint);
    }

    // ── S5 ───────────────────────────────────────────────────────────────────────────────────────────────────────

    // On a mongod that cannot run a transaction (a developer's plain mongod), a submit's reservation and a draft edit
    // — GSKU's and Global Product's — answer with a code (FIX2: an uncoded 500).
    [Fact]
    public async Task S5_Without_transactions_the_four_writes_answer_with_a_code()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        var product = await SeedProductAsync(ProductIdentityLifecycleStatus.Draft);
        var standalone = host.StandaloneDatabase;
        var stored = await host.Database.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == gsku).SingleAsync();
        var revision = await host.Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
            .Find(x => x.Id == stored.ProductDefinitionRevisionId).SingleAsync();
        await standalone.GetCollection<Gsku>("mdm_gskus").InsertOneAsync(stored);
        await standalone.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions").InsertOneAsync(revision);
        await standalone.GetCollection<GlobalProduct>(GlobalProducts).InsertOneAsync(await ProductAsync(product));

        var draft = (await new GskuRepository(standalone, Tenant()).GetByIdAsync(gsku))!;
        draft.PackQuantity = 5;
        draft.AuditIntents.Add(new LocalAuditIntent
        {
            IntentId = Guid.NewGuid(), TenantId = _tenant, AggregateType = AuditAggregateType.Gsku, AggregateId = gsku,
            Operation = ProductAuditOperation.GskuDraftUpdated, IdempotencyKey = Guid.NewGuid().ToString("D"),
            EvidenceHash = "standalone", PreVersion = version, PostVersion = version + 1
        });
        var edit = await new GskuRepository(standalone, Tenant()).UpdateDraftAsync(draft, version);
        var reserve = await new FirstGskuIdentityWorkflowOperationRepository(standalone, Tenant())
            .ReserveAsync(ReservableOperation(stored, revision, version));
        var productEdit = await EditProductAsync(new GlobalProductRepository(standalone, Tenant()), product, 0, "Standalone");
        var productReserve = await new GlobalProductIdentityWorkflowOperationRepository(standalone, Tenant())
            .ReserveAsync(ProductOperation(product, 0, GlobalProductIdentityWorkflowCheckpoint.Prepared, null));

        Assert.Equal(ProductIdentityStore.Unavailable, edit.ErrorCode);
        Assert.Equal(ProductIdentityStore.Unavailable, reserve.ErrorCode);
        Assert.Equal(ProductIdentityStore.Unavailable, productEdit.ErrorCode);
        Assert.Equal(ProductIdentityStore.Unavailable, productReserve.ErrorCode);
    }

    // A transient abort of the reservation that is NOT an edit of the draft (here: another write to the same
    // document that leaves the draft as it was) is the store's, not "the draft changed" (FIX2 said changed for all).
    [Fact]
    public async Task S5_A_transient_abort_without_an_edit_is_said_as_the_store()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        var plain = host.Database;
        var hooked = Hooked("update", "mdm_gskus", () =>
            plain.GetCollection<Gsku>("mdm_gskus").UpdateOne(x => x.Id == gsku,
                Builders<Gsku>.Update.Set(x => x.UpdatedAt, DateTimeOffset.UtcNow)));
        var stored = await plain.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == gsku).SingleAsync();
        var revision = await plain.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
            .Find(x => x.Id == stored.ProductDefinitionRevisionId).SingleAsync();

        var reserved = await new FirstGskuIdentityWorkflowOperationRepository(hooked, Tenant())
            .ReserveAsync(ReservableOperation(stored, revision, version));

        Assert.False(reserved.Succeeded);
        Assert.Equal(ProductIdentityStore.Unavailable, reserved.ErrorCode);
    }

    // ── S6 ───────────────────────────────────────────────────────────────────────────────────────────────────────

    // At apply time a contract mismatch between the two services says nothing about the approved values: the
    // correction waits, the GSKU keeps its lock, and once Platform answers again the approved correction is applied
    // (FIX2 ended it "not applicable" and the approval was silently lost).
    [Fact]
    public async Task S6_A_contract_mismatch_at_apply_time_waits_and_keeps_the_approved_correction()
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RequestCorrectionAsync(approved.Id, approved.Version, 6m)).IsSuccessStatusCode);
        host.Engine.Approve(host.Engine.Latest("GskuCorrection"), host.Approver);
        host.PlatformReference.FailEnvelope = (HttpStatusCode.Conflict, "REFERENCE_CONTRACT_MISMATCH");
        try
        {
            for (var cycle = 0; cycle < 3; cycle++)
            {
                await host.RunAsync<Diten.MdmService.Api.Services.ProductItemSkuMaster.GskuCorrectionRecoveryRunner>(
                    runner => runner.RunCycleAsync());
                await Task.Delay(1100);
            }
            var waiting = await DetailAsync(approved.Id);
            Assert.DoesNotContain("REQUEST_CORRECTION", waiting.Actions);
            Assert.Equal(1m, waiting.Quantity);
        }
        finally
        {
            host.PlatformReference.FailEnvelope = null;
        }
        Assert.Equal(6m, (await CorrectionCyclesUntilAsync(approved.Id, detail => detail.Quantity == 6m)).Quantity);
    }

    // A value no longer selectable at apply time is the one case that ends the correction unapplied.
    [Fact]
    public async Task S6_A_value_no_longer_selectable_at_apply_time_ends_the_correction()
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RequestCorrectionAsync(approved.Id, approved.Version, 2m)).IsSuccessStatusCode);
        host.Engine.Approve(host.Engine.Latest("GskuCorrection"), host.Approver);
        host.PlatformReference.NotSelectable["C62"] = true;
        try
        {
            var freed = await CorrectionCyclesUntilAsync(approved.Id, detail => detail.Actions.Contains("REQUEST_CORRECTION"));
            Assert.Equal(1m, freed.Quantity);
            Assert.Equal(GskuCorrectionWorkflowProcessor.NotApplicable, (await CorrectionOf(approved.Id)).LastFailureCode);
        }
        finally
        {
            host.PlatformReference.NotSelectable.Clear();
        }
    }

    // ── S7 ───────────────────────────────────────────────────────────────────────────────────────────────────────

    // A "success" whose evidence cannot be used, again and again: the lock is kept while it is asked again, and after
    // the retry limit the correction goes to manual reconciliation with the code the screen says in words.
    [Fact]
    public async Task S7_An_unusable_start_answer_is_quarantined_after_the_retry_limit()
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Success, "UNUSABLE", false);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Success;
        try
        {
            Assert.False((await RequestCorrectionAsync(approved.Id, approved.Version, 3m)).IsSuccessStatusCode);
            var operations = new GskuCorrectionWorkflowOperationRepository(host.Database, Tenant());
            var operation = await CorrectionOf(approved.Id);
            Assert.Equal(GskuCorrectionWorkflowCheckpoint.StartOutcomeUnknown, operation.Checkpoint);
            var processor = CorrectionProcessor(new VersionedReferences(), crashAfterApply: false);
            var execution = new GskuCorrectionExecutionConfiguration(TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(100), QuarantineAfterFailedAttempts: 3, MaxRetryDelay: TimeSpan.FromMilliseconds(200));
            for (var attempt = 0; attempt < 30 && operation.Checkpoint != GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired; attempt++)
            {
                await Task.Delay(1100);
                await processor.RecoverAsync(operation, "s7", execution, default);
                operation = (await operations.GetByOperationIdAsync(operation.OperationId))!;
            }

            Assert.Equal(GskuCorrectionWorkflowProcessor.RetryLimitReached, operation.LastFailureCode);
            Assert.DoesNotContain("REQUEST_CORRECTION", (await DetailAsync(approved.Id)).Actions);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
    }

    // ── S8 ───────────────────────────────────────────────────────────────────────────────────────────────────────

    // A start that timed out, then "nothing under this key": inside the settle window the GSKU keeps its lock (the
    // engine may still be on it); after it, the GSKU is given back.
    [Fact]
    public async Task S8_Nothing_arrived_is_not_trusted_inside_the_settle_window()
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "WORKFLOW_TIMEOUT", false);
        Assert.False((await RequestCorrectionAsync(approved.Id, approved.Version, 9m)).IsSuccessStatusCode);
        await Task.Delay(1100);
        await host.RunAsync<Diten.MdmService.Api.Services.ProductItemSkuMaster.GskuCorrectionRecoveryRunner>(
            runner => runner.RunCycleAsync());

        var settling = await CorrectionOf(approved.Id);
        Assert.Equal(GskuCorrectionWorkflowProcessor.StartSettling, settling.LastFailureCode);
        Assert.DoesNotContain("REQUEST_CORRECTION", (await DetailAsync(approved.Id)).Actions);

        await CorrectionCyclesUntilAsync(approved.Id, detail => detail.Actions.Contains("REQUEST_CORRECTION"));
        Assert.Equal("GSKU_CORRECTION_START_NOT_RECEIVED", (await CorrectionOf(approved.Id)).LastFailureCode);
    }

    // ── S9 ───────────────────────────────────────────────────────────────────────────────────────────────────────

    // IDR:123 measured for real: the reservation's own touch of the draft (not a racer's) makes an edit that read
    // before it conflict and refuse.
    [Fact]
    public async Task S9_An_edit_racing_a_real_reservation_is_refused_by_the_reservations_own_touch()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        var plain = host.Database;
        var stored = await plain.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == gsku).SingleAsync();
        var revision = await plain.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
            .Find(x => x.Id == stored.ProductDefinitionRevisionId).SingleAsync();
        FirstGskuIdentityWorkflowReserveResult? reserved = null;
        var hooked = Hooked("findAndModify", "mdm_gskus", () =>
            reserved = new FirstGskuIdentityWorkflowOperationRepository(plain, Tenant())
                .ReserveAsync(ReservableOperation(stored, revision, version)).GetAwaiter().GetResult());
        var draft = (await new GskuRepository(plain, Tenant()).GetByIdAsync(gsku))!;
        draft.PackQuantity = 8;
        draft.AuditIntents.Add(new LocalAuditIntent
        {
            IntentId = Guid.NewGuid(), TenantId = _tenant, AggregateType = AuditAggregateType.Gsku, AggregateId = gsku,
            Operation = ProductAuditOperation.GskuDraftUpdated, IdempotencyKey = Guid.NewGuid().ToString("D"),
            EvidenceHash = "real-reservation", PreVersion = version, PostVersion = version + 1
        });

        var edit = await new GskuRepository(hooked, Tenant()).UpdateDraftAsync(draft, version);

        Assert.True(reserved!.Succeeded, reserved.ErrorCode);
        Assert.False(edit.Succeeded);
        Assert.Equal(GskuRepository.SubmitInFlight, edit.ErrorCode);
        Assert.Equal(1, (await plain.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == gsku).SingleAsync()).IdentitySubmitReservations);
    }

    // GR (MutateLifecycleAsync) and ProductDefinitionRevisionRepository on the real store: a pair member at its
    // audit-intent capacity makes the pending write wait (AUDIT_INTENT_CAPACITY_REACHED), never quarantine.
    [Theory]
    [InlineData("mdm_gskus")]
    [InlineData("mdm_product_definition_revisions")]
    public async Task S9_A_pair_member_at_its_audit_capacity_waits_on_the_real_store(string member)
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        var stored = await host.Database.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == gsku).SingleAsync();
        await FillAuditIntentsAsync(member, member == "mdm_gskus" ? gsku : stored.ProductDefinitionRevisionId);

        await SubmitAsync(gsku, version);

        var operation = await IdentityOperationOf(gsku);
        Assert.NotEqual(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, operation.Checkpoint);
        Assert.Equal("AUDIT_INTENT_CAPACITY_REACHED", operation.LastFailureCode);
    }

    // IDR's not-applicable write on the real store: at capacity it waits too (here reached through E1).
    [Fact]
    public async Task S9_The_not_applicable_write_at_audit_capacity_waits_on_the_real_store()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(gsku, version)).StatusCode);
        await FillAuditIntentsAsync("mdm_gskus", gsku);
        host.Engine.CloseWithoutDecision(host.Engine.Open("gsku", gsku.ToString("D")));

        var operation = await IdentityOperationOf(gsku);
        for (var cycle = 0; cycle < 10 && operation.LastFailureCode != "AUDIT_INTENT_CAPACITY_REACHED"; cycle++)
        {
            await host.RunAsync<Diten.MdmService.Api.Services.ProductItemSkuMaster.FirstGskuIdentityWorkflowRecoveryRunner>(
                runner => runner.RunCycleAsync());
            operation = await IdentityOperationOf(gsku);
            if (operation.LastFailureCode != "AUDIT_INTENT_CAPACITY_REACHED") await Task.Delay(1100);
        }

        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision, operation.Checkpoint);
        Assert.Equal("AUDIT_INTENT_CAPACITY_REACHED", operation.LastFailureCode);
    }

    // IDP:1663 / IDR:829 — a completed withdrawal whose intents were delivered and compacted to receipts is still
    // recognised by its repeat (the read-back accepts the receipt).
    [Fact]
    public async Task S9_A_withdrawal_repeat_after_its_intents_were_compacted_is_still_a_replay()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(gsku, version)).StatusCode);
        var pending = await DetailAsync(gsku);
        var key = Guid.NewGuid();
        var body = new { expectedGskuVersion = pending.Version, reasonCode = "MAKER_CORRECTION" };
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/identity-approval/withdraw",
            body, MakerToken(), key)).StatusCode);
        var stored = await host.Database.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == gsku).SingleAsync();
        await CompactAsync<Gsku>("mdm_gskus", gsku, ProductAuditOperation.GskuIdentityApprovalWithdrawn);
        await CompactAsync<ProductDefinitionRevision>("mdm_product_definition_revisions", stored.ProductDefinitionRevisionId,
            ProductAuditOperation.ProductDefinitionRevisionIdentityApprovalWithdrawn);

        var repeat = await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/identity-approval/withdraw", body, MakerToken(), key);

        Assert.True(repeat.StatusCode == HttpStatusCode.OK, await repeat.Content.ReadAsStringAsync());
    }

    // RRP gets FIX2 items 4 and 5 too: an unreadable start answer keeps the lock and the decision still applies; an
    // approval closed without a decision frees the GSKU.
    [Fact]
    public async Task S9_A_retirement_request_with_an_unreadable_start_answer_keeps_the_lock_and_still_applies()
    {
        var approved = await ApprovedGskuAsync();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Invalid,
            ProductIdentityWorkflowFaultPolicy.ResponseUnreadable, true);
        Assert.False((await RequestRetirementAsync(approved.Id, approved.Version)).IsSuccessStatusCode);
        Assert.DoesNotContain("REQUEST_RETIREMENT", (await DetailAsync(approved.Id)).Actions);

        await RetirementCyclesUntilAsync(approved.Id, _ => host.Engine.Instances(GskuRetirementRequestWorkflowStartRequestFactory.ObjectType)
            .Any(x => x.ObjectId != null && x.ApprovedBy is null && !x.Cancelled && !x.ClosedWithoutDecision));
        host.Engine.Approve(host.Engine.Latest(GskuRetirementRequestWorkflowStartRequestFactory.ObjectType), host.Approver);

        Assert.Equal(ProductIdentityLifecycleStatus.Retired,
            (await RetirementCyclesUntilAsync(approved.Id, detail => detail.Status == ProductIdentityLifecycleStatus.Retired)).Status);
    }

    [Fact]
    public async Task S9_A_retirement_request_closed_without_a_decision_frees_the_gsku()
    {
        var approved = await ApprovedGskuAsync();
        Assert.True((await RequestRetirementAsync(approved.Id, approved.Version)).IsSuccessStatusCode);
        host.Engine.CloseWithoutDecision(host.Engine.Latest(GskuRetirementRequestWorkflowStartRequestFactory.ObjectType));

        var freed = await RetirementCyclesUntilAsync(approved.Id, detail => detail.Actions.Contains("REQUEST_RETIREMENT"));

        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, freed.Status);
    }

    // RES:150 — a retired value and a value withdrawn from new use are each "not selectable", separately.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task S9_Each_of_the_two_conditions_makes_a_value_not_selectable(bool retired, bool withdrawnFromNewUse)
    {
        if (retired) host.PlatformReference.Retired["KGM"] = true;
        if (withdrawnFromNewUse) host.PlatformReference.NotSelectable["KGM"] = true;
        try
        {
            using var scope = host.Factory.Services.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<IWorkflowVerifiedGskuReferenceResolver>()
                .ResolveLatestAsync(_tenant, "SCALAR_QUANTITY_APPLIES", "KGM");

            Assert.False(result.IsSuccessful);
            Assert.Equal(VerifiedGskuReferenceResolveResult.NotSelectable, result.FailureCode);
        }
        finally
        {
            host.PlatformReference.Retired.Clear();
            host.PlatformReference.NotSelectable.Clear();
        }
    }

    // FIX2 item 6 (the submitter's reconciling repeat): another tenant's repeat finds nothing; a caller without the
    // submit permission is refused before anything is read.
    [Fact]
    public async Task S9_The_reconciling_repeat_is_tenant_bound_and_needs_the_submit_permission()
    {
        var (gsku, key) = await QuarantinedPendingAsync("FIRST_GSKU_IDENTITY_PAIR_INCONSISTENT");
        var submittedAt = (await IdentityOperationOf(gsku)).ExpectedGskuVersion;

        var otherTenant = await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/submit", new { expectedVersion = submittedAt },
            host.Token(Guid.NewGuid(), _maker, MakerPermissions), key);
        var noPermission = await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/submit", new { expectedVersion = submittedAt },
            host.Token(_tenant, _maker, "mdm.gskus.read"), key);

        Assert.Equal(HttpStatusCode.NotFound, otherTenant.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, noPermission.StatusCode);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, (await IdentityOperationOf(gsku)).Checkpoint);
    }

    // BL-527: the two classifications FIX2 added to the shared fixed-name database test, here on the throwaway mongod.
    [Theory]
    [InlineData(FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted, FirstGskuIdentitySubmitStateKind.InFlight)]
    [InlineData(FirstGskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart, FirstGskuIdentitySubmitStateKind.Ended)]
    public async Task S9_The_latest_submit_is_classified_on_the_real_store(FirstGskuIdentityWorkflowCheckpoint checkpoint,
        FirstGskuIdentitySubmitStateKind expected)
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        await InsertIdentityOperationAsync(gsku, version, checkpoint);

        Assert.Equal(expected, (await new FirstGskuIdentityWorkflowOperationRepository(host.Database, Tenant())
            .GetSubmitStateAsync(gsku, version)).Kind);
        Assert.Equal(FirstGskuIdentitySubmitStateKind.None, (await new FirstGskuIdentityWorkflowOperationRepository(
            host.Database, Tenant(Guid.NewGuid())).GetSubmitStateAsync(gsku, version)).Kind);
    }

    // S10: the latest submit is the one of the highest version, whatever the clocks said when they were written.
    [Fact]
    public async Task S10_The_latest_submit_is_chosen_by_version_not_by_clock()
    {
        var (gsku, version) = await CreateAsync(await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved));
        await InsertIdentityOperationAsync(gsku, version, FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted,
            createdAt: DateTimeOffset.UtcNow.AddHours(-1));
        await InsertIdentityOperationAsync(gsku, version - 1, FirstGskuIdentityWorkflowCheckpoint.Completed,
            createdAt: DateTimeOffset.UtcNow); // an older submit, written by a clock that runs ahead

        Assert.Equal(FirstGskuIdentitySubmitStateKind.InFlight, (await new FirstGskuIdentityWorkflowOperationRepository(
            host.Database, Tenant()).GetSubmitStateAsync(gsku, version)).Kind);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    private Task<HttpResponseMessage> RequestRetirementAsync(Guid gsku, int version) =>
        PostAsync($"/api/gskus/{gsku:D}/retirement-requests",
            new { expectedGskuVersion = version, requestReason = "PRODUCT_DISCONTINUED" });

    private Task<Detail> RetirementCyclesUntilAsync(Guid gsku, Func<Detail, bool> done) =>
        CyclesUntilAsync(gsku, done,
            () => host.RunAsync<Diten.MdmService.Api.Services.ProductItemSkuMaster.GskuRetirementRequestRecoveryRunner>(
                runner => runner.RunCycleAsync()));

    private async Task<Guid> InsertIdentityOperationAsync(Guid gsku, int version, FirstGskuIdentityWorkflowCheckpoint checkpoint,
        string? leaseOwner = null, DateTimeOffset? leaseUntil = null, DateTimeOffset? createdAt = null)
    {
        var stored = await host.Database.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == gsku).SingleAsync();
        var revision = await host.Database.GetCollection<ProductDefinitionRevision>("mdm_product_definition_revisions")
            .Find(x => x.Id == stored.ProductDefinitionRevisionId).SingleAsync();
        var operation = ReservableOperation(stored, revision, version);
        operation.Id = Guid.NewGuid();
        operation.TenantId = _tenant;
        operation.Checkpoint = checkpoint;
        operation.WorkflowInstanceId = null;
        operation.LeaseOwner = leaseOwner;
        operation.LeaseUntilUtcTicksV1 = leaseUntil?.UtcTicks;
        operation.CreatedAtUtcTicksV1 = (createdAt ?? DateTimeOffset.UtcNow).UtcTicks;
        operation.TemporalStorageVersion = FirstGskuIdentityWorkflowOperation.CurrentTemporalStorageVersion;
        await host.Database.GetCollection<FirstGskuIdentityWorkflowOperation>(FirstGskuIdentityWorkflowOperationRepository.CollectionName)
            .InsertOneAsync(operation);
        return operation.OperationId;
    }

    private Task<FirstGskuIdentityWorkflowOperation> IdentityOperationById(Guid operationId) =>
        host.Database.GetCollection<FirstGskuIdentityWorkflowOperation>(FirstGskuIdentityWorkflowOperationRepository.CollectionName)
            .Find(x => x.OperationId == operationId).FirstAsync();

    private async Task FillAuditIntentsAsync(string collection, Guid id)
    {
        var rows = host.Database.GetCollection<MongoDB.Bson.BsonDocument>(collection);
        var filter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("_id",
            new MongoDB.Bson.BsonBinaryData(id, MongoDB.Bson.GuidRepresentation.Standard));
        var current = (await rows.Find(filter).SingleAsync())["AuditIntents"].AsBsonArray.Count;
        var fillers = Enumerable.Range(0, AuditIntentLimits.MaxPerAggregate - current).Select(_ =>
            MongoDB.Bson.BsonExtensionMethods.ToBsonDocument(new LocalAuditIntent
            {
                IntentId = Guid.NewGuid(), TenantId = _tenant, AggregateId = id,
                IdempotencyKey = Guid.NewGuid().ToString("D"), EvidenceHash = "capacity-filler"
            }));
        await rows.UpdateOneAsync(filter, Builders<MongoDB.Bson.BsonDocument>.Update.PushEach("AuditIntents", fillers));
    }

    private async Task CompactAsync<T>(string collection, Guid id, ProductAuditOperation operation) where T : EntityBase, IAuditIntentAggregate
    {
        var rows = host.Database.GetCollection<T>(collection);
        var aggregate = await rows.Find(x => x.Id == id).SingleAsync();
        var intent = aggregate.AuditIntents.Single(x => x.Operation == operation);
        await rows.UpdateOneAsync(x => x.Id == id, Builders<T>.Update
            .PullFilter(x => x.AuditIntents, x => x.IntentId == intent.IntentId)
            .Push(x => x.AuditIntentReceipts, new LocalAuditIntentReceipt
            {
                SourceService = "mdm", IntentId = intent.IntentId, TenantId = intent.TenantId,
                IdempotencyKey = intent.IdempotencyKey, EvidenceHash = intent.EvidenceHash,
                CentralAcknowledgement = "ack", CentralIdempotencyKey = intent.IdempotencyKey, ContractVersion = "1",
                AcknowledgedAt = DateTimeOffset.UtcNow, DeliveredAt = DateTimeOffset.UtcNow, CompactedAt = DateTimeOffset.UtcNow,
                CompactReceiptReference = "receipt"
            }));
    }
}
