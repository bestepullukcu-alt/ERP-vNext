using System.Net;
using System.Net.Http.Json;
using Diten.MdmService.Api.Services.ProductItemSkuMaster;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-LSKU-ACCEPT-01 FIX2 — items 1–13 and CT's sabotage gaps (L2–L15) over HTTP; FIX1's description follows:
/// WP-MDM-LSKU-ACCEPT-01 FIX1 — the LSKU blockers over HTTP against the real MDM Api and a throwaway mongod (the GSKU
/// round's host): what the maker and the worker meet, read back from the store. Engine answers are the ones the real
/// client produces (FakePlatformCodeGuardTests holds every fed code to Platform's own).
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed class LskuFix2HttpTests(MdmHttpHost host)
{
    private static readonly string[] MakerPermissions =
    [
        "mdm.gskus.read", "mdm.gskus.create", "mdm.gskus.update", "mdm.gskus.submit", "mdm.gskus.withdraw",
        "mdm.gskus.request-correction", "mdm.gskus.request-retirement",
        "mdm.lskus.read", "mdm.lskus.create", "mdm.lskus.submit", "mdm.lskus.withdraw", "mdm.lskus.request-retirement"
    ];

    private const string RecoverKey = "mdm.product-identity.lifecycle-operations.recover";

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _maker = Guid.NewGuid();

    // ---------------------------------------------------------------------------------------------------- item 1

    // The gauge counts stale admissions over every tenant WITHOUT a tenant context (FIX1 asked a tenant-scoped repository
    // and never counted), on its own worker — no LSKU worker is involved. One GSKU is written without IsDeleted (item 13).
    [Fact]
    public async Task Item01_The_admission_gauge_counts_stale_admissions_of_two_tenants_on_its_own_worker()
    {
        var gauge = host.Factory.Services.GetRequiredService<GskuChildAdmissionGauge>();
        var metrics = host.Factory.Services.GetRequiredService<ProductIdentityRecoveryMetrics>();
        var before = await gauge.RecordAsync(CancellationToken.None);
        var gskus = host.Database.GetCollection<Gsku>("mdm_gskus");
        var legacy = Guid.NewGuid();
        foreach (var (tenant, id) in new[] { (Guid.NewGuid(), Guid.NewGuid()), (Guid.NewGuid(), legacy) })
        {
            var command = "STALE-" + id.ToString("N");
            await gskus.InsertOneAsync(new Gsku
            {
                Id = id, TenantId = tenant, CanonicalCode = "GS-" + id.ToString("N")[..10],
                ChildCreationAdmissions =
                [
                    new GskuChildCreationAdmission
                    {
                        ChildKind = GskuChildIdentityKind.Lsku, CreationCommandId = command,
                        RequestFingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(id, GskuChildIdentityKind.Lsku, command, "TR"),
                        AcquiredAtUtc = DateTimeOffset.UtcNow.AddHours(-2)
                    }
                ]
            });
        }
        await host.Database.GetCollection<MongoDB.Bson.BsonDocument>("mdm_gskus").UpdateOneAsync(
            new MongoDB.Bson.BsonDocument("_id", new MongoDB.Bson.BsonBinaryData(legacy, MongoDB.Bson.GuidRepresentation.Standard)),
            new MongoDB.Bson.BsonDocument("$unset", new MongoDB.Bson.BsonDocument("IsDeleted", "")));
        var worker = new GskuChildAdmissionGaugeWorker(gauge, Options.Create(new GskuChildAdmissionGaugeOptions()),
            NullLogger<GskuChildAdmissionGaugeWorker>.Instance);

        await worker.RunOnceAsync(CancellationToken.None);

        Assert.Equal(before + 2, metrics.Current(GskuChildAdmissionGauge.Flow));
    }

    // ---------------------------------------------------------------------------------------------------- item 2 (a)

    // The withdrawal's cancel goes unanswered → the worker (no token) parks it for the maker → the maker repeats the SAME
    // withdrawal and the engine accepts the cancel: the store takes AwaitingMakerReplay → WithdrawalObserved, the LSKU is
    // a draft again with the withdrawal's intent (FIX1: refused, the LSKU pending for good with a dead approval).
    [Fact]
    public async Task Item02_A_withdrawal_whose_cancel_was_unanswered_is_applied_on_the_makers_repeat()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "LU");
        var version = (await LskuAsync(lsku)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(lsku, version)).StatusCode);
        var key = Guid.NewGuid();
        host.Engine.NextCancelFails = ProductIdentityWorkflowTransportOutcome.Timeout;
        var first = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/identity-approval/withdraw",
            new { expectedVersion = version + 1, reasonCode = "CHANGED_MIND" }, MakerToken(), key);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, first.StatusCode);
        await IdentityCyclesUntilAsync(lsku, version, op => op.Checkpoint == LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay);

        var repeat = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/identity-approval/withdraw",
            new { expectedVersion = version + 1, reasonCode = "CHANGED_MIND" }, MakerToken(), key);

        Assert.True(repeat.StatusCode == HttpStatusCode.OK, await repeat.Content.ReadAsStringAsync());
        var row = await LskuAsync(lsku);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, row.LifecycleStatus);
        Assert.Single(row.AuditIntents, x => x.Operation == ProductAuditOperation.LskuIdentityApprovalWithdrawn);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.Completed, (await IdentityOperationAsync(lsku, version)).Checkpoint);
    }

    // Found by the transition census: a withdrawal parked for its maker is not a start waiting for its replay — a repeat
    // of the ORIGINAL submit is refused and touches nothing (before: it took the start path, AMR → WorkflowStarted).
    [Fact]
    public async Task Item02_A_repeat_of_the_submit_during_its_withdrawal_is_refused_and_changes_nothing()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "LV");
        var version = (await LskuAsync(lsku)).Version;
        var submitKey = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Accepted, (await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit",
            new { expectedVersion = version }, MakerToken(), submitKey)).StatusCode);
        host.Engine.NextCancelFails = ProductIdentityWorkflowTransportOutcome.Timeout;
        await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/identity-approval/withdraw",
            new { expectedVersion = version + 1, reasonCode = "CHANGED_MIND" }, MakerToken());
        await IdentityCyclesUntilAsync(lsku, version, op => op.Checkpoint == LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay);

        var repeat = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit",
            new { expectedVersion = version }, MakerToken(), submitKey);

        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        Assert.Equal(LskuIdentityWorkflowProcessor.WithdrawalUnderWay, Envelope.FirstError(await Envelope.ReadAsync(repeat)));
        var operation = await IdentityOperationAsync(lsku, version);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay, operation.Checkpoint);
        Assert.NotNull(operation.WithdrawalCommandId);
    }

    // ---------------------------------------------------------------------------------------------------- item 3 / L8 / H2

    // A start lookup the engine refuses about the record (tenant mismatch) after a start whose answer was lost: stuck with
    // the fence held — never abandoned (L8), answered with ITS cause in this request and not reconciled in the same
    // request (H2) — and the identity gauge counts it (item 3).
    [Fact]
    public async Task Item03_A_refused_start_lookup_is_stuck_unabandoned_unreconciled_and_counted()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "MT");
        var draft = await LskuAsync(lsku);
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "FAKE_ENGINE_START_TIMEOUT", false);
        host.Engine.NextLookupFailure = (ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH");

        var submit = await SubmitAsync(lsku, draft.Version);

        Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode);
        Assert.Equal("WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH", Envelope.FirstError(await Envelope.ReadAsync(submit)));
        var operation = await IdentityOperationAsync(lsku, draft.Version);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, operation.Checkpoint);
        Assert.NotNull(operation.LastStartSentAtUtcTicksV1);
        var row = await LskuAsync(lsku);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, row.LifecycleStatus);
        Assert.Equal(draft.Version, row.Version);
        Assert.Equal(LskuLifecycleOperationKind.IdentitySubmit, row.ActiveLifecycleOperation?.Kind);
        LskuIdentityWorkflowRecoveryRunResult? cycle = null;
        await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(async runner => cycle = await runner.RunCycleAsync());
        Assert.True(cycle!.Cycle!.InManualReconciliation >= 1);
        Assert.True(cycle.Cycle.NeedingAPerson >= 1);
        var metrics = host.Factory.Services.GetRequiredService<ProductIdentityRecoveryMetrics>();
        Assert.True(metrics.Current(LskuIdentityWorkflowRecoveryRunner.Flow) >= 1);
        Assert.True(metrics.NeedingAPerson(LskuIdentityWorkflowRecoveryRunner.Flow) >= 1);
    }

    // ---------------------------------------------------------------------------------------------------- item 4 / L2

    // A creation answered 202 (row written, its code still pending, its admission held) and then cancelled: the code is
    // burned, the admission goes, and the repair finds nothing left to release.
    [Fact]
    public async Task Item04_A_draft_cancelled_after_a_pending_creation_burns_its_code_and_frees_the_gsku()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "MK");
        var row = await LskuAsync(lsku);
        // As a 202 left it: the code pending, the admission still on the GSKU.
        await host.Database.GetCollection<CodeReservation>("mdm_code_reservations").UpdateOneAsync(x => x.Id == row.CodeReservationId,
            Builders<CodeReservation>.Update.Set(x => x.BindingState, CodeReservationBindingState.PendingIdentityWrite));
        await PushAdmissionAsync(gsku, row.CreationCommandId, "MK", DateTimeOffset.UtcNow.AddHours(-1));

        var cancel = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/draft/cancel",
            new { expectedVersion = row.Version, reasonCode = "CREATED_BY_MISTAKE" }, MakerToken());

        Assert.True(cancel.StatusCode == HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
        Assert.True((await Envelope.ReadAsync(cancel)).GetProperty("data").GetProperty("admissionReleased").GetBoolean());
        Assert.Empty(await AdmissionsAsync(gsku));
        var code = await host.Database.GetCollection<CodeReservation>("mdm_code_reservations").Find(x => x.Id == row.CodeReservationId).SingleAsync();
        Assert.Equal(CodeReservationBindingState.Burned, code.BindingState);
        var repair = await host.Client(host.Token(_tenant, Guid.NewGuid(), RecoverKey))
            .PostAsync("/api/lskus/operator/child-admission-repairs", null);
        Assert.Equal(0, (await Envelope.ReadAsync(repair)).GetProperty("data").GetProperty("found").GetInt32());
    }

    // ---------------------------------------------------------------------------------------------------- item 5

    // X consumed its code and its write went unanswered (no row); Y won the market. X's repeat is refused as the same
    // market — AFTER finishing its own traces: its code is burned and its admission released.
    [Fact]
    public async Task Item05_A_repeat_refused_early_burns_its_own_code_and_releases_its_admission()
    {
        // FIX3 item 5: the seeded code is THIS request's (its actor) — another actor's code is IDEMPOTENCY_KEY_CONFLICT.
        var gsku = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        var commandId = key.ToString("D").ToUpperInvariant();
        var reservations = new CodeReservationRepository(host.Database, new Tenant(_tenant));
        var reserved = await reservations.ReserveAsync(CodeBearingEntityType.Lsku, commandId, _maker.ToString("D"), commandId);
        Assert.True((await reservations.ConsumeForIdentityAsync(reserved.Id, CodeBearingEntityType.Lsku, Guid.NewGuid(),
            reserved.Version, commandId, _maker.ToString("D"), commandId)).Succeeded);
        await PushAdmissionAsync(gsku, commandId, "SM", DateTimeOffset.UtcNow.AddMinutes(-1));
        await CreatedLskuAsync(gsku, "SM"); // Y

        var repeat = await CreateLskuAsync(gsku, "SM", key);

        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        Assert.Equal("LSKU_MARKET_ALREADY_EXISTS", Envelope.FirstError(await Envelope.ReadAsync(repeat)));
        Assert.Equal(CodeReservationBindingState.Burned,
            (await host.Database.GetCollection<CodeReservation>("mdm_code_reservations").Find(x => x.Id == reserved.Id).SingleAsync()).BindingState);
        Assert.DoesNotContain(commandId, await AdmissionsAsync(gsku));
    }

    // The operator path for an admission held ONLY by a code consumed for an identity whose row never came (past the
    // cutoff): the repair burns that code, then releases the admission.
    [Fact]
    public async Task Item05_The_repair_burns_a_stale_unbound_code_then_releases_its_admission()
    {
        var gsku = await ApprovedGskuAsync();
        var commandId = "LOST-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        var reservations = new CodeReservationRepository(host.Database, new Tenant(_tenant));
        var reserved = await reservations.ReserveAsync(CodeBearingEntityType.Lsku, commandId, "test", commandId);
        Assert.True((await reservations.ConsumeForIdentityAsync(reserved.Id, CodeBearingEntityType.Lsku, Guid.NewGuid(),
            reserved.Version, commandId, "test", commandId)).Succeeded);
        await host.Database.GetCollection<CodeReservation>("mdm_code_reservations").UpdateOneAsync(x => x.Id == reserved.Id,
            Builders<CodeReservation>.Update.Set(x => x.ConsumedAt, DateTimeOffset.UtcNow.AddHours(-2)));
        await PushAdmissionAsync(gsku, commandId, "SN", DateTimeOffset.UtcNow.AddHours(-2));

        var repair = await host.Client(host.Token(_tenant, Guid.NewGuid(), RecoverKey))
            .PostAsync("/api/lskus/operator/child-admission-repairs", null);

        Assert.Equal(1, (await Envelope.ReadAsync(repair)).GetProperty("data").GetProperty("released").GetInt32());
        Assert.DoesNotContain(commandId, await AdmissionsAsync(gsku));
        Assert.Equal(CodeReservationBindingState.Burned,
            (await host.Database.GetCollection<CodeReservation>("mdm_code_reservations").Find(x => x.Id == reserved.Id).SingleAsync()).BindingState);
    }

    // ---------------------------------------------------------------------------------------------------- item 6

    [Fact]
    public async Task Item06_The_same_draft_cancel_twice_answers_200_and_says_it_is_a_replay()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "MD");
        var version = (await LskuAsync(lsku)).Version;
        var key = Guid.NewGuid();
        var body = new { expectedVersion = version, reasonCode = "CREATED_BY_MISTAKE" };

        var first = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/draft/cancel", body, MakerToken(), key);
        var second = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/draft/cancel", body, MakerToken(), key);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.True(second.StatusCode == HttpStatusCode.OK, await second.Content.ReadAsStringAsync());
        Assert.True((await Envelope.ReadAsync(second)).GetProperty("data").GetProperty("isReplay").GetBoolean());
    }

    // ---------------------------------------------------------------------------------------------------- item 7

    // The provider refusing THIS SERVICE (403) waits — the approver's decision is kept, the LSKU stays pending; a
    // provider contract conflict stays stuck with its cause. Neither throws the approval away.
    [Fact]
    public async Task Item07_A_provider_that_refuses_the_service_waits_and_a_contract_conflict_is_stuck()
    {
        var gsku = await ApprovedGskuAsync();
        var waiting = await CreatedLskuAsync(gsku, "AL");
        var stuck = await CreatedLskuAsync(gsku, "BA");
        var waitingVersion = (await LskuAsync(waiting)).Version;
        var stuckVersion = (await LskuAsync(stuck)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(waiting, waitingVersion)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(stuck, stuckVersion)).StatusCode);
        host.Markets.Refusals["AL"] = (403, "REFERENCE_PROVIDER_FORBIDDEN");
        host.Markets.Refusals["BA"] = (409, "REFERENCE_CONTRACT_MISMATCH");
        try
        {
            host.Engine.Approve(host.Engine.Open("lsku", waiting.ToString("D")), host.Approver);
            host.Engine.Approve(host.Engine.Open("lsku", stuck.ToString("D")), host.Approver);
            var held = await IdentityCyclesUntilAsync(stuck, stuckVersion,
                op => op.Checkpoint == LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired);
            var waits = await IdentityCyclesUntilAsync(waiting, waitingVersion,
                op => op.LastFailureCode == "REFERENCE_PROVIDER_FORBIDDEN");

            Assert.Equal("REFERENCE_CONTRACT_MISMATCH", held.LastFailureCode);
            Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waits.RecoveryDisposition);
            Assert.Equal("REFERENCE_PROVIDER_FORBIDDEN", waits.LastFailureCode);
            Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await LskuAsync(waiting)).LifecycleStatus);
            Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await LskuAsync(stuck)).LifecycleStatus);
        }
        finally
        {
            host.Markets.Refusals.TryRemove("AL", out _);
            host.Markets.Refusals.TryRemove("BA", out _);
        }
    }

    // ---------------------------------------------------------------------------------------------------- item 8 / M3

    // The cancel ↔ first start race: a cancel that arrives while the start is on its way meets the submit's fence and is
    // refused — the engine's approval is never left open for a deleted draft.
    [Fact]
    public async Task Item08_A_cancel_during_the_first_start_is_refused_by_the_submits_fence()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "RS");
        var version = (await LskuAsync(lsku)).Version;
        HttpResponseMessage? cancelDuringStart = null;
        host.Engine.OnNextStart = async () => cancelDuringStart = await SendAsync(HttpMethod.Post,
            $"/api/lskus/{lsku:D}/draft/cancel", new { expectedVersion = version, reasonCode = "RACE" }, MakerToken());

        var submit = await SubmitAsync(lsku, version);

        Assert.Equal(HttpStatusCode.Accepted, submit.StatusCode);
        Assert.NotNull(cancelDuringStart);
        Assert.Equal(HttpStatusCode.Conflict, cancelDuringStart!.StatusCode);
        Assert.Equal("LSKU_SUBMIT_IN_FLIGHT", Envelope.FirstError(await Envelope.ReadAsync(cancelDuringStart)));
        var row = await LskuAsync(lsku);
        Assert.False(row.IsDeleted);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, row.LifecycleStatus);
        Assert.Null(row.ActiveLifecycleOperation); // the fence went with the pending write
    }

    // A start that WAS sent (its answer lost) is never abandoned on a repeat, even when the draft changed since: the
    // engine is asked instead (only a start never sent may end the submit).
    [Fact]
    public async Task Item08_A_sent_start_is_not_abandoned_when_the_draft_changed_since()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "RO");
        var version = (await LskuAsync(lsku)).Version;
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "FAKE_ENGINE_START_TIMEOUT", false);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Timeout;
        try
        {
            var first = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(), key);
            Assert.False(first.IsSuccessStatusCode);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
        Assert.NotNull((await IdentityOperationAsync(lsku, version)).LastStartSentAtUtcTicksV1);
        // The draft changes under the sent start (its fence taken away, its version moved — an old write path).
        await host.Database.GetCollection<Lsku>("mdm_lskus").UpdateOneAsync(x => x.Id == lsku,
            Builders<Lsku>.Update.Set(x => x.ActiveLifecycleOperation, null).Inc(x => x.Version, 1));
        await host.Database.GetCollection<LskuIdentityWorkflowOperation>("mdm_lsku_identity_workflow_operations").UpdateOneAsync(
            x => x.TenantId == _tenant && x.LskuId == lsku, Builders<LskuIdentityWorkflowOperation>.Update.Set(x => x.NextAttemptAtUtcTicksV1, null));

        await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(), key);

        Assert.NotEqual(LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
            (await IdentityOperationAsync(lsku, version)).Checkpoint);
    }

    // Item 8, the store's half: while a submit's fence is on the draft, the store itself refuses the cancel by name —
    // whatever the handler in front of it decided.
    [Fact]
    public async Task Item08_The_store_refuses_a_draft_cancel_while_the_submit_fence_is_held()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "FN");
        var row = await LskuAsync(lsku);
        var repository = new LskuRepository(host.Database, new Tenant(_tenant));
        Assert.True((await repository.FenceIdentitySubmitAsync(lsku, row.Version, Guid.NewGuid())).Succeeded);
        var audit = Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.LskuIdentityLifecycleAuditIntentFactory
            .CreateDraftCancelled(row, row.Version, Guid.NewGuid(), _maker, "CREATED_BY_MISTAKE", DateTimeOffset.UtcNow);

        var cancelled = await repository.CancelDraftAsync(lsku, row.Version, audit);

        Assert.False(cancelled.Succeeded);
        Assert.Equal("LSKU_SUBMIT_IN_FLIGHT", cancelled.ErrorCode);
        Assert.False((await LskuAsync(lsku)).IsDeleted);
    }

    // ---------------------------------------------------------------------------------------------------- item 9

    // The same command leaks twice (a release in between): each repair has its own intent (keyed by the admission's
    // acquisition), so the second is not refused as an idempotency conflict of the first.
    [Fact]
    public async Task Item09_Two_leaks_of_one_command_are_two_repairs_with_their_own_intents()
    {
        var gsku = await ApprovedGskuAsync();
        const string command = "TWICE-LEAKED";
        await PushAdmissionAsync(gsku, command, "TW", DateTimeOffset.UtcNow.AddHours(-3));
        var first = await host.Client(host.Token(_tenant, Guid.NewGuid(), RecoverKey))
            .PostAsync("/api/lskus/operator/child-admission-repairs", null);
        await PushAdmissionAsync(gsku, command, "TW", DateTimeOffset.UtcNow.AddHours(-2));

        var second = await host.Client(host.Token(_tenant, Guid.NewGuid(), RecoverKey))
            .PostAsync("/api/lskus/operator/child-admission-repairs", null);

        Assert.Equal(1, (await Envelope.ReadAsync(first)).GetProperty("data").GetProperty("released").GetInt32());
        Assert.Equal(1, (await Envelope.ReadAsync(second)).GetProperty("data").GetProperty("released").GetInt32());
        var released = (await GskuAsync(gsku)).AuditIntents
            .Where(x => x.Operation == ProductAuditOperation.GskuChildCreationAdmissionReleased).ToList();
        Assert.Equal(2, released.Count);
        // Two intents of their own: the same key with other evidence would collide at the central log (one row lost).
        Assert.Equal(2, released.Select(x => x.IdempotencyKey).Distinct().Count());
        Assert.Equal(2, released.Select(x => x.IntentId).Distinct().Count());
    }

    // Measured: Acquire does NOT renew an existing admission's time (an admission of the same command and request is a
    // replay) — so the second leak above is a second admission only after a release, never a renewed one.
    [Fact]
    public async Task Item09_Acquire_of_an_existing_admission_is_a_replay_that_keeps_its_time()
    {
        var gsku = await ApprovedGskuAsync();
        const string command = "KEEPS-ITS-TIME";
        var acquiredAt = DateTimeOffset.UtcNow.AddHours(-1);
        await PushAdmissionAsync(gsku, command, "KT", acquiredAt);
        var repository = new GskuRepository(host.Database, new Tenant(_tenant));

        var again = await repository.AcquireChildCreationAdmissionAsync(gsku, GskuChildIdentityKind.Lsku, command,
            GskuChildCreationAdmission.ComputeRequestFingerprint(gsku, GskuChildIdentityKind.Lsku, command, "KT"),
            DateTimeOffset.UtcNow);

        Assert.True(again.IsReplay);
        var kept = (await GskuAsync(gsku)).ChildCreationAdmissions.Single(x => x.CreationCommandId == command);
        Assert.Equal(acquiredAt.UtcTicks / TimeSpan.TicksPerMillisecond, kept.AcquiredAtUtc.UtcTicks / TimeSpan.TicksPerMillisecond);
    }

    // Item 9: the maker's repeat of a leaked creation and the operator's repair at the same time — whichever wins, the
    // end is coherent: at most one row for the market, a row's code confirmed, and no admission of the command left.
    [Fact]
    public async Task Item09_A_creation_replay_racing_the_repair_ends_coherent()
    {
        var gsku = await ApprovedGskuAsync();
        foreach (var market in new[] { "RA", "RB", "RC", "RD", "RE" })
        {
            var key = Guid.NewGuid();
            var commandId = key.ToString("D").ToUpperInvariant();
            await PushAdmissionAsync(gsku, commandId, market, DateTimeOffset.UtcNow.AddHours(-2));

            var create = CreateLskuAsync(gsku, market, key);
            var repair = host.Client(host.Token(_tenant, Guid.NewGuid(), RecoverKey))
                .PostAsync("/api/lskus/operator/child-admission-repairs", null);
            await Task.WhenAll(create, repair);

            Assert.True((await create).StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
                market + ": " + await (await create).Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, (await repair).StatusCode);
            var rows = await host.Database.GetCollection<Lsku>("mdm_lskus")
                .Find(x => x.TenantId == _tenant && x.GskuId == gsku && x.MarketCode == market && !x.IsDeleted).ToListAsync();
            Assert.True(rows.Count <= 1, market);
            foreach (var row in rows)
            {
                var code = await host.Database.GetCollection<CodeReservation>("mdm_code_reservations")
                    .Find(x => x.Id == row.CodeReservationId).SingleAsync();
                Assert.Equal(CodeReservationBindingState.Confirmed, code.BindingState);
            }
            Assert.DoesNotContain(commandId, await AdmissionsAsync(gsku));
        }
    }

    // ---------------------------------------------------------------------------------------------------- item 10 / L9

    // A submit parked for its maker because the engine refused the start before processing it (nothing exists there):
    // the draft cancel closes it once the settle window has passed, and the cancel succeeds.
    [Fact]
    public async Task Item10_A_draft_cancel_closes_a_submit_the_engine_never_started()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "CY");
        var version = (await LskuAsync(lsku)).Version;
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN", false);
        await SubmitAsync(lsku, version);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay, (await IdentityOperationAsync(lsku, version)).Checkpoint);
        await Task.Delay(TimeSpan.FromSeconds(2.5)); // past the settle window of this host

        var cancel = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/draft/cancel",
            new { expectedVersion = version, reasonCode = "NOT_NEEDED" }, MakerToken());

        Assert.True(cancel.StatusCode == HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
        var closed = await IdentityOperationAsync(lsku, version);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart, closed.Checkpoint);
        Assert.Equal(LskuIdentityWorkflowProcessor.ClosedByDraftCancel, closed.LastFailureCode);
    }

    // L9: a submit whose start may have reached the engine (its answer lost, the engine silent) refuses the cancel.
    [Fact]
    public async Task Item10_A_draft_with_a_submit_in_flight_cannot_be_cancelled()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "CV");
        var version = (await LskuAsync(lsku)).Version;
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "FAKE_ENGINE_START_TIMEOUT", false);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Timeout;
        try
        {
            Assert.False((await SubmitAsync(lsku, version)).IsSuccessStatusCode);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
        Assert.Equal(LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown, (await IdentityOperationAsync(lsku, version)).Checkpoint);
        // Past the settle window (and the engine answering again, "nothing under this key"): the refusal is the
        // checkpoint's — an unanswered start is never closed by a draft cancel — not the window's.
        await Task.Delay(TimeSpan.FromSeconds(2.5));

        var cancel = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/draft/cancel",
            new { expectedVersion = version, reasonCode = "NOT_NEEDED" }, MakerToken());

        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        Assert.Equal("LSKU_SUBMIT_IN_FLIGHT", Envelope.FirstError(await Envelope.ReadAsync(cancel)));
        Assert.False((await LskuAsync(lsku)).IsDeleted);
    }

    // The requester's repeat on a stuck submit whose start the engine has nothing for: inside the settle window it is NOT
    // abandoned (the engine may still be on it); after the window it is (H2 row 1: the draft's version moves).
    [Fact]
    public async Task Item10_Reconcile_abandons_a_start_the_engine_has_nothing_for_only_after_the_settle_window()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "CW");
        var draft = await LskuAsync(lsku);
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "FAKE_ENGINE_START_TIMEOUT", false);
        host.Engine.NextLookupFailure = (ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH");
        Assert.Equal(HttpStatusCode.Conflict,
            (await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = draft.Version }, MakerToken(), key)).StatusCode);

        var inWindow = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = draft.Version }, MakerToken(), key);

        Assert.Equal(HttpStatusCode.Conflict, inWindow.StatusCode);
        Assert.Equal(LskuIdentityWorkflowProcessor.ManualReconciliationNeeded, Envelope.FirstError(await Envelope.ReadAsync(inWindow)));
        Assert.Equal(LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, (await IdentityOperationAsync(lsku, draft.Version)).Checkpoint);
        await Task.Delay(TimeSpan.FromSeconds(2.5));

        var afterWindow = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = draft.Version }, MakerToken(), key);

        Assert.Equal(HttpStatusCode.Conflict, afterWindow.StatusCode);
        Assert.Equal("LSKU_IDENTITY_SUBMIT_ABANDONED", Envelope.FirstError(await Envelope.ReadAsync(afterWindow)));
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart, (await IdentityOperationAsync(lsku, draft.Version)).Checkpoint);
        var row = await LskuAsync(lsku);
        Assert.Equal(draft.Version + 1, row.Version);
        Assert.Null(row.ActiveLifecycleOperation);
    }

    // ---------------------------------------------------------------------------------------------------- item 11: H1 / H2

    // H1: a stuck operation whose rejection is already on the LSKU — the requester's repeat finds it there and completes;
    // the LSKU is not written again. Negative: a recorded decision of another actor does not match → stays stuck, 409.
    [Fact]
    public async Task Item11_H1_A_stuck_operation_whose_rejection_is_on_the_lsku_completes_on_the_repeat()
    {
        var (lsku, version, key) = await RejectedThenStuckAsync("HB");
        var before = await LskuAsync(lsku);

        var repeat = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(), key);

        Assert.True(repeat.IsSuccessStatusCode, await repeat.Content.ReadAsStringAsync());
        Assert.Equal(LskuIdentityWorkflowCheckpoint.Completed, (await IdentityOperationAsync(lsku, version)).Checkpoint);
        var after = await LskuAsync(lsku);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, after.LifecycleStatus);
    }

    [Fact]
    public async Task Item11_H1_A_recorded_decision_of_another_actor_stays_stuck()
    {
        var (lsku, version, key) = await RejectedThenStuckAsync("HC");
        // The decision on the LSKU names another actor than the one the operation recorded (the operation's own
        // evidence stays intact, so only the comparison can tell them apart).
        await host.Database.GetCollection<Lsku>("mdm_lskus").UpdateOneAsync(x => x.Id == lsku,
            Builders<Lsku>.Update.Set(x => x.IdentityWorkflowBinding!.TerminalDecision!.DecisionActorSubjectId, Guid.NewGuid()));

        var repeat = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(), key);

        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        Assert.Equal(LskuIdentityWorkflowProcessor.ManualReconciliationNeeded, Envelope.FirstError(await Envelope.ReadAsync(repeat)));
        var operation = await IdentityOperationAsync(lsku, version);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, operation.Checkpoint);
        // It STAYED stuck on its own cause: it was never put to "applied" (whose verification would have sent it back
        // with DECISION_INCONSISTENT).
        Assert.Equal(StuckCause, operation.LastFailureCode);
    }

    // H2 rows: a stuck operation with an instance and the LSKU pending on it goes back to awaiting the decision (and is
    // applied); one whose start proof is on record while the LSKU is still the reserved draft goes back to started.
    [Fact]
    public async Task Item11_H2_A_stuck_operation_with_a_pending_lsku_goes_back_to_awaiting_its_decision()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "HD");
        var version = (await LskuAsync(lsku)).Version;
        var key = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Accepted,
            (await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(), key)).StatusCode);
        await ParkAsync(lsku, "LSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT");
        host.Engine.Approve(host.Engine.Open("lsku", lsku.ToString("D")), host.Approver);

        var repeat = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(), key);

        Assert.True(repeat.IsSuccessStatusCode, await repeat.Content.ReadAsStringAsync());
        Assert.NotEqual(LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, (await IdentityOperationAsync(lsku, version)).Checkpoint);
        await IdentityCyclesUntilAsync(lsku, version, op => op.Checkpoint == LskuIdentityWorkflowCheckpoint.Completed);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await LskuAsync(lsku)).LifecycleStatus);
    }

    [Fact]
    public async Task Item11_H2_A_stuck_started_operation_whose_draft_is_not_pending_goes_back_to_started()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "HE");
        var version = (await LskuAsync(lsku)).Version;
        var key = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Accepted,
            (await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(), key)).StatusCode);
        // The pending write is undone by hand (as if it had never landed): the LSKU is the reserved draft again.
        await host.Database.GetCollection<Lsku>("mdm_lskus").UpdateOneAsync(x => x.Id == lsku,
            Builders<Lsku>.Update.Set(x => x.LifecycleStatus, ProductIdentityLifecycleStatus.Draft)
                .Set(x => x.Version, version).Set(x => x.IdentityWorkflowBinding, null).PopLast(x => x.AuditIntents));
        await ParkAsync(lsku, "LSKU_IDENTITY_WORKFLOW_LOCAL_PENDING_INCONSISTENT");

        var repeat = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(), key);

        var after = await IdentityOperationAsync(lsku, version);
        Assert.True(repeat.IsSuccessStatusCode,
            await repeat.Content.ReadAsStringAsync() + $" {after.Checkpoint} {after.LastFailureCode}");
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await LskuAsync(lsku)).LifecycleStatus);
    }

    // ---------------------------------------------------------------------------------------------------- CT L5 / L6 / L7

    [Fact]
    public async Task L05_An_approval_closed_without_a_decision_makes_the_submit_not_applicable()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "CK");
        var version = (await LskuAsync(lsku)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(lsku, version)).StatusCode);
        host.Engine.CloseWithoutDecision(host.Engine.Open("lsku", lsku.ToString("D")));

        var operation = await IdentityCyclesUntilAsync(lsku, version,
            op => op.Checkpoint is LskuIdentityWorkflowCheckpoint.DecisionNotApplicable or LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired);

        Assert.Equal(LskuIdentityWorkflowCheckpoint.DecisionNotApplicable, operation.Checkpoint);
        Assert.Equal(LskuIdentityWorkflowProcessor.ClosedWithoutDecision, operation.LastFailureCode);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await LskuAsync(lsku)).LifecycleStatus);
    }

    [Fact]
    public async Task L06_Inconsistent_engine_evidence_is_stuck_and_the_requesters_repeat_reads_the_engine_again()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "IL");
        var version = (await LskuAsync(lsku)).Version;
        var key = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Accepted,
            (await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(), key)).StatusCode);
        var instance = host.Engine.Open("lsku", lsku.ToString("D"));
        instance.EvidenceInconsistent = true;
        await IdentityCyclesUntilAsync(lsku, version, op => op.Checkpoint == LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await LskuAsync(lsku)).LifecycleStatus);
        // The engine's facts are put right and the approver decides; the requester's same submit reads it again.
        instance.EvidenceInconsistent = false;
        host.Engine.Approve(instance, host.Approver);

        var repeat = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(), key);

        Assert.True(repeat.IsSuccessStatusCode, await repeat.Content.ReadAsStringAsync());
        Assert.NotEqual(LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, (await IdentityOperationAsync(lsku, version)).Checkpoint);
        await IdentityCyclesUntilAsync(lsku, version, op => op.Checkpoint == LskuIdentityWorkflowCheckpoint.Completed);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await LskuAsync(lsku)).LifecycleStatus);
    }

    [Fact]
    public async Task L07_An_lsku_at_its_audit_intent_cap_waits_and_is_never_stuck()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "CP");
        var version = (await LskuAsync(lsku)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(lsku, version)).StatusCode);
        await FillAuditIntentsAsync(lsku);
        host.Engine.Approve(host.Engine.Open("lsku", lsku.ToString("D")), host.Approver);

        var operation = await IdentityCyclesUntilAsync(lsku, version,
            op => op.LastFailureCode == "AUDIT_INTENT_CAPACITY_REACHED" || op.Checkpoint == LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired);
        for (var cycle = 0; cycle < 3; cycle++)
        {
            await Task.Delay(1200);
            await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
        }
        operation = await IdentityOperationAsync(lsku, version);

        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, operation.RecoveryDisposition);
        Assert.NotEqual(LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, operation.Checkpoint);
        Assert.Equal("AUDIT_INTENT_CAPACITY_REACHED", operation.LastFailureCode);
    }

    // ---------------------------------------------------------------------------------------------------- CT L11 / L12 / L13

    [Fact]
    public async Task L11_The_repair_refuses_an_age_below_its_floor()
    {
        var response = await host.Client(host.Token(_tenant, Guid.NewGuid(), RecoverKey))
            .PostAsync("/api/lskus/operator/child-admission-repairs?minimumAgeMinutes=4", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("LSKU_ADMISSION_REPAIR_REQUEST_INVALID", Envelope.FirstError(await Envelope.ReadAsync(response)));
    }

    [Fact]
    public async Task L12_The_retirement_store_refuses_manual_reconciliation_as_retryable()
    {
        var (lsku, version) = await ApprovedLskuAsync("ST");
        Assert.Equal(HttpStatusCode.Accepted, (await RequestRetirementAsync(lsku, version)).StatusCode);
        var collection = host.Database.GetCollection<LskuRetirementRequestOperation>("mdm_lsku_retirement_request_operations");
        var operation = await collection.Find(x => x.TenantId == _tenant && x.LskuId == lsku).SingleAsync();
        var repository = new LskuRetirementRequestOperationRepository(host.Database, new Tenant(_tenant));
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var claim = await repository.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
            [operation.Checkpoint], "l12", now, now + TimeSpan.FromMinutes(1).Ticks);
        Assert.NotNull(claim);

        var written = await repository.AdvanceAsync(claim!, new(LskuRetirementRequestCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.Retryable, now + 1, now + TimeSpan.FromMinutes(1).Ticks, "X", ReleaseLease: true));

        Assert.False(written);
        Assert.Equal(operation.Checkpoint, (await collection.Find(x => x.Id == operation.Id).SingleAsync()).Checkpoint);
    }

    [Fact]
    public async Task L13_The_identity_store_refuses_manual_reconciliation_as_retryable()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "SU");
        var version = (await LskuAsync(lsku)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(lsku, version)).StatusCode);
        var operation = await IdentityOperationAsync(lsku, version);
        var repository = new LskuIdentityWorkflowOperationRepository(host.Database, new Tenant(_tenant));
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var claim = await repository.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [operation.Checkpoint], "l13", now, now + TimeSpan.FromMinutes(1).Ticks));
        Assert.NotNull(claim);

        var written = await repository.AdvanceAsync(claim!, new(LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.Retryable, now + 1, now + TimeSpan.FromMinutes(1).Ticks, "X", ReleaseLease: true));

        Assert.False(written);
        Assert.Equal(operation.Checkpoint, (await IdentityOperationAsync(lsku, version)).Checkpoint);
    }

    // ---------------------------------------------------------------------------------------------------- item 13

    // A retirement request written before FIX1 (no FailedAttemptCount, no LastStartSentAtUtcTicksV1) goes end to end:
    // its refused start frees the LSKU as any other.
    [Fact]
    public async Task Item13_A_retirement_request_without_the_fix1_fields_still_goes_end_to_end()
    {
        var (lsku, version) = await ApprovedLskuAsync("LG");
        // A legacy request is reserved by hand from a real one: request, then strip its new fields, then let it start.
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "FAKE_ENGINE_START_TIMEOUT", false);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Timeout;
        try
        {
            Assert.False((await RequestRetirementAsync(lsku, version)).IsSuccessStatusCode);
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }
        await host.Database.GetCollection<MongoDB.Bson.BsonDocument>("mdm_lsku_retirement_request_operations").UpdateOneAsync(
            new MongoDB.Bson.BsonDocument("LskuId", new MongoDB.Bson.BsonBinaryData(lsku, MongoDB.Bson.GuidRepresentation.Standard)),
            new MongoDB.Bson.BsonDocument("$unset", new MongoDB.Bson.BsonDocument
            {
                { "FailedAttemptCount", "" }, { "LastStartSentAtUtcTicksV1", "" }
            }));

        var operations = host.Database.GetCollection<LskuRetirementRequestOperation>("mdm_lsku_retirement_request_operations");
        LskuRetirementRequestOperation operation = null!;
        for (var cycle = 0; cycle < 40; cycle++)
        {
            await host.RunAsync<LskuRetirementRequestRecoveryRunner>(runner => runner.RunCycleAsync());
            operation = await operations.Find(x => x.TenantId == _tenant && x.LskuId == lsku).SingleAsync();
            if (!operation.IsActive) break;
            await Task.Delay(250);
        }

        Assert.False(operation.IsActive);
        Assert.Null((await LskuAsync(lsku)).ActiveLifecycleOperation);
    }

    // Item 13: an identity submit written before FIX2 has no LastStartSentAtUtcTicksV1 — its start may have been sent.
    // When its draft has moved on (no fence), the repeat asks the engine instead of abandoning; only the explicit
    // "never sent" (0, written at reservation since FIX2) abandons.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Item13_A_submit_without_the_start_sent_field_is_never_abandoned_on_its_absence(bool legacy, bool abandoned)
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, legacy ? "LH" : "LI");
        var version = (await LskuAsync(lsku)).Version;
        var key = Guid.NewGuid();
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Forbidden, "WORKFLOW_DELEGATED_USER_FORBIDDEN", false);
        await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(), key);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay, (await IdentityOperationAsync(lsku, version)).Checkpoint);
        var operations = host.Database.GetCollection<MongoDB.Bson.BsonDocument>("mdm_lsku_identity_workflow_operations");
        var filter = new MongoDB.Bson.BsonDocument("LskuId", new MongoDB.Bson.BsonBinaryData(lsku, MongoDB.Bson.GuidRepresentation.Standard));
        await operations.UpdateOneAsync(filter, legacy
            ? new MongoDB.Bson.BsonDocument("$unset", new MongoDB.Bson.BsonDocument("LastStartSentAtUtcTicksV1", ""))
            : new MongoDB.Bson.BsonDocument("$set", new MongoDB.Bson.BsonDocument("LastStartSentAtUtcTicksV1", 0L)));
        // The draft moved on under the submit (its fence gone, its version moved).
        await host.Database.GetCollection<Lsku>("mdm_lskus").UpdateOneAsync(x => x.Id == lsku,
            Builders<Lsku>.Update.Set(x => x.ActiveLifecycleOperation, null).Inc(x => x.Version, 1));

        var repeat = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(), key);

        Assert.Equal(abandoned, (await IdentityOperationAsync(lsku, version)).Checkpoint
            == LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart);
        if (abandoned)
        {
            // Item 12: the draft changed — said as such, not as an engine refusal.
            Assert.Equal(LskuIdentityWorkflowProcessor.SubmitTargetChanged, Envelope.FirstError(await Envelope.ReadAsync(repeat)));
        }
    }

    // Item 13 (CT addendum): a GSKU written before ChildCreationAdmissions existed (the field $unset) is corrected,
    // retired and gets a child end to end — "no admission" matches a missing list too, not only an empty one.
    [Fact]
    public async Task Item13_A_gsku_without_the_admissions_field_is_corrected_retired_and_gets_a_child()
    {
        var corrected = await ApprovedGskuAsync();
        var retired = await ApprovedGskuAsync();
        var parent = await ApprovedGskuAsync();
        foreach (var id in new[] { corrected, retired, parent }) await UnsetAdmissionsAsync(id);

        var correction = await SendAsync(HttpMethod.Post, $"/api/gskus/{corrected:D}/correction-requests",
            new { expectedGskuVersion = (await GskuAsync(corrected)).Version, packQuantity = 2.5m, packUomCode = "KGM" }, MakerToken());
        Assert.True(correction.IsSuccessStatusCode, await correction.Content.ReadAsStringAsync());
        host.Engine.Approve(host.Engine.Latest("GskuCorrection"), host.Approver);
        await GskuCyclesUntilAsync(corrected, gsku => gsku.PackUomCode == "KGM" && gsku.ActiveLifecycleOperation is null,
            () => host.RunAsync<GskuCorrectionRecoveryRunner>(runner => runner.RunCycleAsync()));

        var retirement = await SendAsync(HttpMethod.Post, $"/api/gskus/{retired:D}/retirement-requests",
            new { expectedGskuVersion = (await GskuAsync(retired)).Version, requestReason = "PRODUCT_DISCONTINUED" }, MakerToken());
        Assert.True(retirement.IsSuccessStatusCode, await retirement.Content.ReadAsStringAsync());
        host.Engine.Approve(host.Engine.Latest("GskuRetirementRequest"), host.Approver);
        await GskuCyclesUntilAsync(retired, gsku => gsku.LifecycleStatus == ProductIdentityLifecycleStatus.Retired,
            async () =>
            {
                await host.RunAsync<GskuRetirementRequestRecoveryRunner>(runner => runner.RunCycleAsync());
                await host.RunAsync<FirstGskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
            });

        await CreatedLskuAsync(parent, "UA");
        Assert.Empty(await AdmissionsAsync(parent));
    }

    private Task UnsetAdmissionsAsync(Guid gsku) =>
        host.Database.GetCollection<MongoDB.Bson.BsonDocument>("mdm_gskus").UpdateOneAsync(
            new MongoDB.Bson.BsonDocument("_id", new MongoDB.Bson.BsonBinaryData(gsku, MongoDB.Bson.GuidRepresentation.Standard)),
            new MongoDB.Bson.BsonDocument("$unset", new MongoDB.Bson.BsonDocument("ChildCreationAdmissions", "")));

    private async Task GskuCyclesUntilAsync(Guid id, Func<Gsku, bool> done, Func<Task> cycle)
    {
        var gsku = await GskuAsync(id);
        for (var attempt = 0; attempt < 40 && !done(gsku); attempt++)
        {
            await cycle();
            gsku = await GskuAsync(id);
            if (!done(gsku)) await Task.Delay(250);
        }
        Assert.True(done(gsku), $"Not reached: {gsku.LifecycleStatus} {gsku.PackUomCode} {gsku.ActiveLifecycleOperation?.Kind}");
    }

    // ---------------------------------------------------------------------------------------------------- CT L4 / item 5 (handler)

    // L4: the store throws before any code is ours — the creation's exit by exception gives the admission back (and the
    // exception still surfaces).
    [Fact]
    public async Task L04_An_exception_before_a_code_is_reserved_gives_the_admission_back()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "L4-" + Guid.NewGuid().ToString("N");
        using var scope = TenantScope();
        var handler = CreateHandler(scope, reservations: Intercept<ICodeReservationRepository>(
            scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
            (method, _) => method.Name == nameof(ICodeReservationRepository.ReserveAsync)
                ? throw new IOException("store went away") : null));

        await Assert.ThrowsAsync<IOException>(() => handler.Handle(CreateCommand(gsku, "LF", key), CancellationToken.None));

        Assert.Empty(await AdmissionsAsync(gsku));
    }

    // L4, the other half: an exception while the creation's code is live (consumed for an identity, its write pending)
    // keeps the admission — that code can still become the child (CT's hold rule); the gauge and the repair see it.
    [Fact]
    public async Task L04_An_exception_while_the_code_is_live_keeps_the_admission()
    {
        var gsku = await ApprovedGskuAsync();
        var key = "L4B-" + Guid.NewGuid().ToString("N");
        using var scope = TenantScope();
        var handler = CreateHandler(scope, lskus: Intercept<ILskuRepository>(
            scope.ServiceProvider.GetRequiredService<ILskuRepository>(),
            (method, _) => method.Name == nameof(ILskuRepository.CreateDraftWithAdmissionAsync)
                ? throw new IOException("store went away") : null));

        await Assert.ThrowsAsync<IOException>(() => handler.Handle(CreateCommand(gsku, "LG", key), CancellationToken.None));

        Assert.Contains(key.ToUpperInvariant(), await AdmissionsAsync(gsku));
    }

    // Item 5: the repeat's own code cannot be burned (a concurrent write took its version) — the refusal is named and
    // the admission stays with the live code (never released next to a code that may still become the child).
    [Fact]
    public async Task Item05_A_burn_the_store_refuses_is_named_and_the_admission_stays()
    {
        // FIX3 item 5: the seeded code is THIS request's (its actor) — another actor's code is IDEMPOTENCY_KEY_CONFLICT.
        var gsku = await ApprovedGskuAsync();
        var key = "BR-" + Guid.NewGuid().ToString("N");
        var commandId = key.ToUpperInvariant();
        using var scope = TenantScope();
        var real = scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>();
        var reserved = await real.ReserveAsync(CodeBearingEntityType.Lsku, commandId, "lsku-fix2-handler-test", commandId);
        Assert.True((await real.ConsumeForIdentityAsync(reserved.Id, CodeBearingEntityType.Lsku, Guid.NewGuid(),
            reserved.Version, commandId, "lsku-fix2-handler-test", commandId)).Succeeded);
        await PushAdmissionAsync(gsku, commandId, "BR", DateTimeOffset.UtcNow.AddMinutes(-1));
        await CreatedLskuAsync(gsku, "BR"); // another creation took the market
        var handler = CreateHandler(scope, reservations: Intercept<ICodeReservationRepository>(real,
            (method, _) => method.Name == nameof(ICodeReservationRepository.BurnIdentityBindingAsync)
                ? Task.FromResult(new ReservationOperationResult(false, null, "CONCURRENCY_CONFLICT")) : null));

        var repeat = await handler.Handle(CreateCommand(gsku, "BR", key), CancellationToken.None);

        Assert.Equal(409, repeat.StatusCode);
        Assert.Contains(Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers
            .CreateLskuDraftHandler.BurnRefused, repeat.Errors);
        Assert.Contains(commandId, await AdmissionsAsync(gsku));
    }

    // ---------------------------------------------------------------------------------------------------- helpers (FIX2)
    private IServiceScope TenantScope()
    {
        var scope = host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(_tenant);
        return scope;
    }

    private static Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers.CreateLskuDraftHandler
        CreateHandler(IServiceScope scope, ICodeReservationRepository? reservations = null, ILskuRepository? lskus = null) =>
        ActivatorUtilities.CreateInstance<
            Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.CommandHandlers.CreateLskuDraftHandler>(
            scope.ServiceProvider,
            reservations ?? scope.ServiceProvider.GetRequiredService<ICodeReservationRepository>(),
            lskus ?? scope.ServiceProvider.GetRequiredService<ILskuRepository>(),
            new HandlerActor());

    private static Diten.MdmService.Application.Features.ProductItemSkuMaster.Commands.CreateLskuDraftCommand CreateCommand(
        Guid gsku, string market, string key) =>
        new(new Diten.MdmService.Application.Features.ProductItemSkuMaster.ProductItemSkuMasterModels.CreateLskuDraftRequest
        {
            GskuId = gsku, MarketCode = market, IdempotencyKey = key
        });

    private sealed class HandlerActor : Diten.MdmService.Application.Contracts.IProductIdentityActorContext
    {
        public string ActorId => "lsku-fix2-handler-test";
    }

    // A repository that forwards every call to the real one unless the interceptor answers it (non-null) or throws.
    private static T Intercept<T>(T inner, Func<System.Reflection.MethodInfo, object?[]?, object?> interceptor) where T : class
    {
        var proxy = System.Reflection.DispatchProxy.Create<T, InterceptProxy>();
        var state = (InterceptProxy)(object)proxy;
        state.Inner = inner;
        state.Interceptor = interceptor;
        return proxy;
    }

    public class InterceptProxy : System.Reflection.DispatchProxy
    {
        public object Inner { get; set; } = null!;
        public Func<System.Reflection.MethodInfo, object?[]?, object?> Interceptor { get; set; } = null!;

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
        {
            try
            {
                return Interceptor(targetMethod!, args) ?? targetMethod!.Invoke(Inner, args);
            }
            catch (System.Reflection.TargetInvocationException exception) when (exception.InnerException is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }


    private async Task<(Guid Lsku, int Version, Guid Key)> RejectedThenStuckAsync(string market)
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, market);
        var version = (await LskuAsync(lsku)).Version;
        var key = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Accepted,
            (await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken(), key)).StatusCode);
        host.Engine.Reject(host.Engine.Open("lsku", lsku.ToString("D")), host.Approver);
        await IdentityCyclesUntilAsync(lsku, version, op => op.Checkpoint == LskuIdentityWorkflowCheckpoint.Completed);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await LskuAsync(lsku)).LifecycleStatus);
        // Stuck after the rejection was applied (a lost lease before verification): back to manual reconciliation.
        await ParkAsync(lsku, StuckCause);
        return (lsku, version, key);
    }

    // A cause no step of the decision path writes, so a test can tell "stayed stuck" from "moved on and came back".
    private const string StuckCause = "LSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT";

    private Task ParkAsync(Guid lsku, string cause) =>
        host.Database.GetCollection<LskuIdentityWorkflowOperation>("mdm_lsku_identity_workflow_operations").UpdateOneAsync(
            x => x.TenantId == _tenant && x.LskuId == lsku,
            Builders<LskuIdentityWorkflowOperation>.Update
                .Set(x => x.Checkpoint, LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired)
                .Set(x => x.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
                .Set(x => x.LastFailureCode, cause).Set(x => x.NextAttemptAtUtcTicksV1, null)
                .Set(x => x.LeaseOwner, null).Set(x => x.LeaseUntilUtcTicksV1, null));

    // The LSKU at its audit-intent cap: clones of its own first intent (each field as the store wrote it).
    private async Task FillAuditIntentsAsync(Guid lsku)
    {
        var raw = host.Database.GetCollection<MongoDB.Bson.BsonDocument>("mdm_lskus");
        var id = new MongoDB.Bson.BsonDocument("_id", new MongoDB.Bson.BsonBinaryData(lsku, MongoDB.Bson.GuidRepresentation.Standard));
        var document = await raw.Find(id).SingleAsync();
        var template = document["AuditIntents"].AsBsonArray[0].AsBsonDocument;
        var full = new MongoDB.Bson.BsonArray(Enumerable.Range(0, AuditIntentLimits.MaxPerAggregate).Select(i =>
        {
            var clone = template.DeepClone().AsBsonDocument;
            clone["IntentId"] = new MongoDB.Bson.BsonBinaryData(Guid.NewGuid(), MongoDB.Bson.GuidRepresentation.Standard);
            clone["IdempotencyKey"] = template["IdempotencyKey"].AsString + ":filler-" + i;
            return clone;
        }));
        await raw.UpdateOneAsync(id, new MongoDB.Bson.BsonDocument("$set", new MongoDB.Bson.BsonDocument("AuditIntents", full)));
    }

    // ---------------------------------------------------------------------------------------------------- helpers

    private string MakerToken() => host.Token(_tenant, _maker, MakerPermissions);

    private async Task<Guid> SeedProductAsync(ProductIdentityLifecycleStatus status)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await host.Database.GetCollection<GlobalProduct>("mdm_global_products").InsertOneAsync(new GlobalProduct
        {
            Id = id, TenantId = _tenant, CanonicalCode = $"GP-{id:N}"[..16],
            GlobalProductName = $"Product {id:N}", GlobalProductNameNormalized = $"PRODUCT {id:N}".ToUpperInvariant(),
            CodeReservationId = Guid.NewGuid(), LifecycleStatus = status, CreatedAt = now, UpdatedAt = now
        });
        return id;
    }

    private async Task<Guid> ApprovedGskuAsync(Guid? product = null)
    {
        product ??= await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var created = await SendAsync(HttpMethod.Post, "/api/gskus/drafts",
            new { globalProductId = product, packQuantity = 1m, packUomCode = "C62" }, MakerToken());
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var data = (await Envelope.ReadAsync(created)).GetProperty("data");
        var gsku = data.GetProperty("gskuId").GetGuid();
        var submit = await SendAsync(HttpMethod.Post, $"/api/gskus/{gsku:D}/submit",
            new { expectedVersion = data.GetProperty("version").GetInt32() }, MakerToken());
        Assert.True(submit.StatusCode == HttpStatusCode.Accepted, await submit.Content.ReadAsStringAsync());
        host.Engine.Approve(host.Engine.Open("gsku", gsku.ToString("D")), host.Approver);
        for (var cycle = 0; cycle < 40; cycle++)
        {
            await host.RunAsync<FirstGskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
            if ((await GskuAsync(gsku)).LifecycleStatus == ProductIdentityLifecycleStatus.IdentityApproved) return gsku;
            await Task.Delay(250);
        }
        throw new Xunit.Sdk.XunitException("The GSKU was not approved.");
    }

    private Task<HttpResponseMessage> CreateLskuAsync(Guid gsku, string market, Guid? key = null) =>
        SendAsync(HttpMethod.Post, "/api/lskus/drafts", new { gskuId = gsku, marketCode = market }, MakerToken(), key);

    private async Task<Guid> CreatedLskuAsync(Guid gsku, string market)
    {
        var response = await CreateLskuAsync(gsku, market);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetProperty("lskuId").GetGuid();
    }

    private async Task<(Guid Id, int Version)> ApprovedLskuAsync(string market)
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, market);
        var version = (await LskuAsync(lsku)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(lsku, version)).StatusCode);
        host.Engine.Approve(host.Engine.Open("lsku", lsku.ToString("D")), host.Approver);
        await IdentityCyclesUntilAsync(lsku, version, op => op.Checkpoint == LskuIdentityWorkflowCheckpoint.Completed);
        var approved = await LskuAsync(lsku);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, approved.LifecycleStatus);
        return (lsku, approved.Version);
    }

    private Task<HttpResponseMessage> SubmitAsync(Guid lsku, int version) =>
        SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/submit", new { expectedVersion = version }, MakerToken());

    private Task<HttpResponseMessage> RequestRetirementAsync(Guid lsku, int version) =>
        SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/retirement-requests",
            new { expectedVersion = version, requestReason = "No longer sold in this market" }, MakerToken());

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object body, string token,
        Guid? key = null)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString("D"));
        return await host.Client(token).SendAsync(request);
    }

    private Task<Lsku> LskuAsync(Guid id) =>
        host.Database.GetCollection<Lsku>("mdm_lskus").Find(x => x.Id == id && x.TenantId == _tenant).SingleAsync();

    private Task<Gsku> GskuAsync(Guid id) =>
        host.Database.GetCollection<Gsku>("mdm_gskus").Find(x => x.Id == id && x.TenantId == _tenant).SingleAsync();

    private async Task<string[]> AdmissionsAsync(Guid gsku) =>
        (await GskuAsync(gsku)).ChildCreationAdmissions.Select(x => x.CreationCommandId).ToArray();

    private Task<LskuIdentityWorkflowOperation> IdentityOperationAsync(Guid lsku, int expectedVersion) =>
        host.Database.GetCollection<LskuIdentityWorkflowOperation>("mdm_lsku_identity_workflow_operations")
            .Find(x => x.TenantId == _tenant && x.LskuId == lsku && x.ExpectedLskuVersion == expectedVersion).SingleAsync();

    // The open retirement-request approval of THIS LSKU (other tests leave theirs open on the shared engine).
    private async Task<FakeWorkflowEngine.Instance> RetirementInstanceAsync(Guid lsku)
    {
        var operation = await host.Database.GetCollection<LskuRetirementRequestOperation>(
                "mdm_lsku_retirement_request_operations")
            .Find(x => x.TenantId == _tenant && x.LskuId == lsku && x.IsActive).SingleAsync();
        return host.Engine.Open("LskuRetirementRequest", operation.OperationId.ToString("D"));
    }

    private Task PushAdmissionAsync(Guid gsku, string command, string market, DateTimeOffset acquiredAt) =>
        host.Database.GetCollection<Gsku>("mdm_gskus").UpdateOneAsync(x => x.Id == gsku,
            Builders<Gsku>.Update.Push(x => x.ChildCreationAdmissions, new GskuChildCreationAdmission
            {
                ChildKind = GskuChildIdentityKind.Lsku, CreationCommandId = command,
                RequestFingerprint = GskuChildCreationAdmission.ComputeRequestFingerprint(
                    gsku, GskuChildIdentityKind.Lsku, command, market),
                AcquiredAtUtc = acquiredAt.ToUniversalTime()
            }));

    private async Task<LskuIdentityWorkflowOperation> IdentityCyclesUntilAsync(Guid lsku, int expectedVersion,
        Func<LskuIdentityWorkflowOperation, bool> done)
    {
        var operation = await IdentityOperationAsync(lsku, expectedVersion);
        for (var attempt = 0; attempt < 40 && !done(operation); attempt++)
        {
            await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
            operation = await IdentityOperationAsync(lsku, expectedVersion);
            if (!done(operation)) await Task.Delay(250);
        }
        Assert.True(done(operation), $"Not reached: {operation.Checkpoint} {operation.LastFailureCode}");
        return operation;
    }

    private sealed class Tenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
