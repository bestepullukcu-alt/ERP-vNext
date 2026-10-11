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
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-LSKU-ACCEPT-01 FIX1 — the LSKU blockers over HTTP against the real MDM Api and a throwaway mongod (the GSKU
/// round's host): what the maker and the worker meet, read back from the store. Engine answers are the ones the real
/// client produces (FakePlatformCodeGuardTests holds every fed code to Platform's own).
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed class LskuFix1HttpTests(MdmHttpHost host)
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

    // ---------------------------------------------------------------------------------------------------- B1

    [Fact]
    public async Task B1_A_second_lsku_for_the_same_market_is_refused_before_an_admission_is_taken()
    {
        var gsku = await ApprovedGskuAsync();
        await CreatedLskuAsync(gsku, "TR");
        var before = await AdmissionsAsync(gsku);
        var codes = host.Database.GetCollection<CodeReservation>("mdm_code_reservations");
        var codesBefore = await codes.CountDocumentsAsync(x => x.TenantId == _tenant);

        var duplicate = await CreateLskuAsync(gsku, "TR");

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("LSKU_MARKET_ALREADY_EXISTS", Envelope.FirstError(await Envelope.ReadAsync(duplicate)));
        Assert.Equal(before, await AdmissionsAsync(gsku));
        Assert.Empty(await AdmissionsAsync(gsku));
        // Refused before anything was taken: no admission and no code (not even a burned one).
        Assert.Equal(codesBefore, await codes.CountDocumentsAsync(x => x.TenantId == _tenant));
    }

    // B1 (b): a creation that ends where nothing can become the child any more gives the admission back — here the
    // command's code is burned (an earlier attempt's), so its consume is refused and no row will ever come.
    [Fact]
    public async Task B1_A_creation_whose_code_is_burned_gives_the_admission_back()
    {
        var gsku = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        var commandId = key.ToString("D").ToUpperInvariant();
        var reservations = new CodeReservationRepository(host.Database, new Tenant(_tenant));
        var reserved = await reservations.ReserveAsync(CodeBearingEntityType.Lsku, commandId, "test", commandId);
        await host.Database.GetCollection<CodeReservation>("mdm_code_reservations").UpdateOneAsync(x => x.Id == reserved.Id,
            Builders<CodeReservation>.Update.Set(x => x.ReservationState, CodeReservationState.Consumed)
                .Set(x => x.BindingState, CodeReservationBindingState.Burned)
                .Set(x => x.ConsumedEntityId, Guid.NewGuid()).Set(x => x.ConsumeCommandId, "an-earlier-attempt"));

        var response = await CreateLskuAsync(gsku, "DE", key);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(await AdmissionsAsync(gsku));
        Assert.Null(await host.Database.GetCollection<Lsku>("mdm_lskus").Find(x => x.TenantId == _tenant).FirstOrDefaultAsync());
    }

    // CT 2026-10-06: an admission is never released while its command holds a live reservation (the repeat, or a write
    // whose outcome is unknown, may still produce the child) — not by the creation, not by the repair; it is counted.
    [Fact]
    public async Task B1_A_live_reservation_keeps_the_admission_and_the_count_shows_it()
    {
        var gsku = await ApprovedGskuAsync();
        var key = Guid.NewGuid();
        var commandId = key.ToString("D").ToUpperInvariant();
        var reservations = new CodeReservationRepository(host.Database, new Tenant(_tenant));
        var reserved = await reservations.ReserveAsync(CodeBearingEntityType.Lsku, commandId, "test", commandId);
        Assert.True((await reservations.ConsumeForIdentityAsync(reserved.Id, CodeBearingEntityType.Lsku, Guid.NewGuid(),
            reserved.Version, "another-command", "test", "another-command")).Succeeded);

        var response = await CreateLskuAsync(gsku, "NL", key);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal([commandId], await AdmissionsAsync(gsku));
        // Two hours old: past the repair's cutoff and the gauge's.
        await host.Database.GetCollection<Gsku>("mdm_gskus").UpdateOneAsync(x => x.Id == gsku,
            Builders<Gsku>.Update.PullFilter(x => x.ChildCreationAdmissions, x => x.CreationCommandId == commandId));
        await PushAdmissionAsync(gsku, commandId, "NL", DateTimeOffset.UtcNow.AddHours(-2));
        var repair = await host.Client(host.Token(_tenant, Guid.NewGuid(), RecoverKey))
            .PostAsync("/api/lskus/operator/child-admission-repairs", null);
        Assert.Equal(0, (await Envelope.ReadAsync(repair)).GetProperty("data").GetProperty("released").GetInt32());
        Assert.Equal([commandId], await AdmissionsAsync(gsku));
        // FIX3 M5: counted by the tenant-free census the gauge reads (other tenants of the shared host may add theirs).
        Assert.True(await new ChildCreationAdmissionCensusRepository(host.Database)
            .CountStaleAsync(DateTimeOffset.UtcNow.AddMinutes(-15)) >= 1);
    }

    [Fact]
    public async Task B1_The_repair_releases_a_leaked_admission_and_leaves_held_ones_alone()
    {
        var gsku = await ApprovedGskuAsync();
        var live = await CreatedLskuAsync(gsku, "FR");
        var liveRow = await LskuAsync(live);
        var old = DateTimeOffset.UtcNow.AddHours(-2);
        // A leaked admission (no row ever came), a young one (a creation still in flight), and an old one whose row
        // exists (completed elsewhere, its pull lost) — only the first is the repair's.
        await PushAdmissionAsync(gsku, "LEAKED-COMMAND", "SE", old);
        await PushAdmissionAsync(gsku, "IN-FLIGHT-COMMAND", "NO", DateTimeOffset.UtcNow);
        await PushAdmissionAsync(gsku, liveRow.CreationCommandId, "FR", old);
        var gskuVersion = (await GskuAsync(gsku)).Version;

        var repair = await host.Client(host.Token(_tenant, Guid.NewGuid(), RecoverKey))
            .PostAsync("/api/lskus/operator/child-admission-repairs", null);

        Assert.True(repair.StatusCode == HttpStatusCode.OK, await repair.Content.ReadAsStringAsync());
        var data = (await Envelope.ReadAsync(repair)).GetProperty("data");
        Assert.Equal(1, data.GetProperty("found").GetInt32()); // the search skips what a live row holds
        Assert.Equal(1, data.GetProperty("released").GetInt32());
        var after = await GskuAsync(gsku);
        Assert.Equal(new HashSet<string>(["IN-FLIGHT-COMMAND", liveRow.CreationCommandId]),
            after.ChildCreationAdmissions.Select(x => x.CreationCommandId).ToHashSet());
        Assert.Equal(gskuVersion + 1, after.Version);
        Assert.Single(after.AuditIntents, x => x.Operation == ProductAuditOperation.GskuChildCreationAdmissionReleased);

        // The write refuses on its own too: an admission a live row holds is never released, even when asked directly.
        var intent = after.AuditIntents.Single(x => x.Operation == ProductAuditOperation.GskuChildCreationAdmissionReleased);
        var direct = await new GskuRepository(host.Database, new Tenant(_tenant)).RepairOrphanChildCreationAdmissionAsync(
            new GskuOrphanChildAdmission(gsku, after.Version, GskuChildIdentityKind.Lsku, liveRow.CreationCommandId,
                GskuChildCreationAdmission.ComputeRequestFingerprint(gsku, GskuChildIdentityKind.Lsku, liveRow.CreationCommandId, "FR"),
                old.ToUniversalTime()),
            new LocalAuditIntent
                {
                    IntentId = Guid.NewGuid(), TenantId = _tenant, AggregateType = AuditAggregateType.Gsku, AggregateId = gsku,
                    PreVersion = after.Version, PostVersion = after.Version + 1,
                    Operation = ProductAuditOperation.GskuChildCreationAdmissionReleased, ActorId = intent.ActorId,
                    CorrelationId = "direct", CausationId = "direct", CommandId = "direct", Sequence = after.Version + 1,
                    TimestampUtc = intent.TimestampUtc, TimestampUtcTicksV1 = intent.TimestampUtcTicksV1,
                    TemporalStorageVersion = intent.TemporalStorageVersion, EvidenceHash = intent.EvidenceHash,
                    SnapshotReference = intent.SnapshotReference, DeliveryState = AuditIntentDeliveryState.Pending,
                    IdempotencyKey = "direct-repair-of-a-held-admission"
                });
        Assert.False(direct.Succeeded);
        Assert.Equal(ChildCreationAdmissionCodes.ChildRowExists, direct.ErrorCode);
        Assert.Contains(liveRow.CreationCommandId, await AdmissionsAsync(gsku));

        // The same call again finds nothing more to release.
        var again = await host.Client(host.Token(_tenant, Guid.NewGuid(), RecoverKey))
            .PostAsync("/api/lskus/operator/child-admission-repairs", null);
        Assert.Equal(0, (await Envelope.ReadAsync(again)).GetProperty("data").GetProperty("released").GetInt32());
    }

    [Fact]
    public async Task B1_The_repair_is_refused_to_a_caller_without_the_recover_key()
    {
        var response = await host.Client(host.Token(_tenant, _maker, MakerPermissions))
            .PostAsync("/api/lskus/operator/child-admission-repairs", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------------------------------------------- B2

    [Fact]
    public async Task B2_A_retirement_start_the_engine_refused_frees_the_lsku_for_a_second_request()
    {
        var (lsku, version) = await ApprovedLskuAsync("IT");
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Forbidden,
            "WORKFLOW_TRUSTED_CONSUMER_FORBIDDEN", false);

        var refused = await RequestRetirementAsync(lsku, version);

        Assert.False(refused.IsSuccessStatusCode, await refused.Content.ReadAsStringAsync());
        var row = await LskuAsync(lsku);
        Assert.Null(row.ActiveLifecycleOperation);
        var ended = await host.Database.GetCollection<LskuRetirementRequestOperation>(
            "mdm_lsku_retirement_request_operations").Find(x => x.TenantId == _tenant && x.LskuId == lsku).SingleAsync();
        Assert.False(ended.IsActive);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);

        var second = await RequestRetirementAsync(lsku, row.Version);

        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.NotNull((await LskuAsync(lsku)).ActiveLifecycleOperation);
    }

    [Fact]
    public async Task B2_A_retirement_start_the_engine_never_received_is_released_after_the_settle_window()
    {
        var (lsku, version) = await ApprovedLskuAsync("PL");
        // The start is lost on the way (nothing is created), and the engine cannot say so at first.
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "FAKE_ENGINE_START_TIMEOUT", false);
        host.Engine.LookupFailure = ProductIdentityWorkflowTransportOutcome.Timeout;
        try
        {
            var response = await RequestRetirementAsync(lsku, version);
            Assert.False(response.IsSuccessStatusCode);
            Assert.NotNull((await LskuAsync(lsku)).ActiveLifecycleOperation); // unknown: the lock stays
        }
        finally
        {
            host.Engine.LookupFailure = null;
        }

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
        Assert.Equal(LskuRetirementRequestWorkflowProcessor.StartNotReceived, operation.LastFailureCode);
        Assert.Null((await LskuAsync(lsku)).ActiveLifecycleOperation);
    }

    // Rule "!admitted.Succeeded": a request whose LSKU lock cannot be taken (here: the LSKU's audit intents are at their
    // cap, so the lock write is refused) ends at once — nothing is started at the engine, no lock is held, and the
    // request is not active.
    [Fact]
    public async Task B2_A_retirement_request_the_lsku_cannot_admit_starts_nothing()
    {
        var (lsku, version) = await ApprovedLskuAsync("AT");
        var lskus = host.Database.GetCollection<Lsku>("mdm_lskus");
        var row = await LskuAsync(lsku);
        var template = row.AuditIntents[0];
        var full = Enumerable.Range(0, AuditIntentLimits.MaxPerAggregate).Select(i => new LocalAuditIntent
        {
            IntentId = Guid.NewGuid(), TenantId = template.TenantId, AggregateType = template.AggregateType,
            AggregateId = template.AggregateId, PreVersion = template.PreVersion, PostVersion = template.PostVersion,
            Operation = template.Operation, ActorId = template.ActorId, CorrelationId = template.CorrelationId,
            CausationId = template.CausationId, CommandId = template.CommandId, Sequence = template.Sequence,
            TimestampUtc = template.TimestampUtc, TimestampUtcTicksV1 = template.TimestampUtcTicksV1,
            TemporalStorageVersion = template.TemporalStorageVersion, EvidenceHash = template.EvidenceHash,
            SnapshotReference = template.SnapshotReference, DeliveryState = template.DeliveryState,
            IdempotencyKey = template.IdempotencyKey + ":filler-" + i
        }).ToList();
        await lskus.UpdateOneAsync(x => x.Id == lsku, Builders<Lsku>.Update.Set(x => x.AuditIntents, full));
        var starts = host.Engine.StartCalls;

        var response = await RequestRetirementAsync(lsku, version);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(starts, host.Engine.StartCalls);
        var ended = await host.Database.GetCollection<LskuRetirementRequestOperation>(
            "mdm_lsku_retirement_request_operations").Find(x => x.TenantId == _tenant && x.LskuId == lsku).SingleAsync();
        Assert.False(ended.IsActive);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, ended.RecoveryDisposition);
        Assert.Null((await LskuAsync(lsku)).ActiveLifecycleOperation);
    }

    // ---------------------------------------------------------------------------------------------------- B3

    [Fact]
    public async Task B3_A_submit_refused_before_processing_is_abandoned_and_the_resubmit_succeeds()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "BE");
        var draft = await LskuAsync(lsku);
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Forbidden,
            "WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH", false);

        var refused = await SubmitAsync(lsku, draft.Version);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var body = await Envelope.ReadAsync(refused);
        Assert.Equal("LSKU_IDENTITY_SUBMIT_ABANDONED", Envelope.FirstError(body));
        Assert.Equal("WORKFLOW_TRUSTED_CONSUMER_TENANT_MISMATCH", body.GetProperty("errors")[1].GetString());
        var abandoned = await IdentityOperationAsync(lsku, draft.Version);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart, abandoned.Checkpoint);
        var reopened = await LskuAsync(lsku);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, reopened.LifecycleStatus);
        Assert.Equal(draft.Version + 1, reopened.Version);

        // The resubmit is a new operation on the new version (the Web derives the operation id from the version).
        var resubmit = await SubmitAsync(lsku, reopened.Version);

        Assert.Equal(HttpStatusCode.Accepted, resubmit.StatusCode);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await LskuAsync(lsku)).LifecycleStatus);
    }

    [Fact]
    public async Task B3_A_cancelled_draft_no_longer_blocks_the_gsku_retirement()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "DK");
        var gskus = new GskuRepository(host.Database, new Tenant(_tenant));
        Assert.Equal("DEPENDENT_IDENTITIES_EXIST", await gskus.FindRetirementBlockerAsync(gsku));

        var cancel = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/draft/cancel",
            new { expectedVersion = (await LskuAsync(lsku)).Version, reasonCode = "CREATED_BY_MISTAKE" }, MakerToken());

        Assert.True(cancel.StatusCode == HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
        var row = await host.Database.GetCollection<Lsku>("mdm_lskus").Find(x => x.Id == lsku).SingleAsync();
        Assert.True(row.IsDeleted);
        Assert.Single(row.AuditIntents, x => x.Operation == ProductAuditOperation.LskuDraftCancelled);
        Assert.Null(await gskus.FindRetirementBlockerAsync(gsku));
        Assert.Empty(await AdmissionsAsync(gsku));
    }

    [Fact]
    public async Task B3_A_draft_cancel_needs_the_create_key()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "FI");

        var cancel = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/draft/cancel",
            new { expectedVersion = (await LskuAsync(lsku)).Version, reasonCode = "X" },
            host.Token(_tenant, _maker, "mdm.lskus.read", "mdm.lskus.submit"));

        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
        Assert.False((await host.Database.GetCollection<Lsku>("mdm_lskus").Find(x => x.Id == lsku).SingleAsync()).IsDeleted);
    }

    // ---------------------------------------------------------------------------------------------------- B5

    [Fact]
    public async Task B5_A_self_approved_lsku_is_a_draft_again_not_quarantined()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "PT");
        var version = (await LskuAsync(lsku)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(lsku, version)).StatusCode);
        host.Engine.Approve(host.Engine.Open("lsku", lsku.ToString("D")), _maker);

        var operation = await IdentityCyclesUntilAsync(lsku, version,
            op => op.Checkpoint is LskuIdentityWorkflowCheckpoint.DecisionNotApplicable
                or LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired or LskuIdentityWorkflowCheckpoint.Completed);

        Assert.Equal(LskuIdentityWorkflowCheckpoint.DecisionNotApplicable, operation.Checkpoint);
        Assert.Equal(LskuIdentityWorkflowProcessor.SelfApprovalIgnored, operation.LastFailureCode);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, (await LskuAsync(lsku)).LifecycleStatus);
    }

    [Fact]
    public async Task B5_A_market_retired_before_the_approval_is_applied_makes_it_not_applicable()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "GR");
        var version = (await LskuAsync(lsku)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(lsku, version)).StatusCode);
        host.Markets.Retired["GR"] = true;
        try
        {
            host.Engine.Approve(host.Engine.Open("lsku", lsku.ToString("D")), host.Approver);
            var operation = await IdentityCyclesUntilAsync(lsku, version,
                op => op.Checkpoint is LskuIdentityWorkflowCheckpoint.DecisionNotApplicable
                    or LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired);

            Assert.Equal(LskuIdentityWorkflowCheckpoint.DecisionNotApplicable, operation.Checkpoint);
            Assert.Equal("REFERENCE_MARKET_NOT_FOUND", operation.LastFailureCode);
            var row = await LskuAsync(lsku);
            Assert.Equal(ProductIdentityLifecycleStatus.Draft, row.LifecycleStatus);
            Assert.Single(row.AuditIntents, x => x.Operation == ProductAuditOperation.LskuIdentityDecisionNotApplicable);
        }
        finally
        {
            host.Markets.Retired.TryRemove("GR", out _);
        }
    }

    [Fact]
    public async Task B5_A_withdrawal_the_engine_refuses_puts_the_submit_back_to_awaiting_the_decision()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "IE");
        var version = (await LskuAsync(lsku)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(lsku, version)).StatusCode);
        var instance = host.Engine.Open("lsku", lsku.ToString("D"));
        // The approver decided before the withdrawal reached the engine: Platform answers 409 to the cancel.
        host.Engine.Approve(instance, host.Approver);

        var withdraw = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/identity-approval/withdraw",
            new { expectedVersion = version + 1, reasonCode = "CHANGED_MIND" }, MakerToken());

        Assert.False(withdraw.IsSuccessStatusCode);
        var operation = await IdentityOperationAsync(lsku, version);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AwaitingDecision, operation.Checkpoint);
        Assert.Null(operation.WithdrawalCommandId);
        Assert.Null(operation.WithdrawalFingerprint);
        Assert.Null(operation.WithdrawalExpectedWorkflowInstanceVersion);
        Assert.Null(operation.WithdrawalObjectRef);
        // The decision is applied by the ordinary observation.
        await IdentityCyclesUntilAsync(lsku, version, op => op.Checkpoint == LskuIdentityWorkflowCheckpoint.Completed);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await LskuAsync(lsku)).LifecycleStatus);
    }

    [Fact]
    public async Task B5_A_self_approved_retirement_request_ends_and_frees_the_lsku()
    {
        var (lsku, version) = await ApprovedLskuAsync("HU");
        Assert.Equal(HttpStatusCode.Accepted, (await RequestRetirementAsync(lsku, version)).StatusCode);
        host.Engine.Approve(await RetirementInstanceAsync(lsku), _maker);

        var operations = host.Database.GetCollection<LskuRetirementRequestOperation>("mdm_lsku_retirement_request_operations");
        LskuRetirementRequestOperation operation = null!;
        for (var cycle = 0; cycle < 40; cycle++)
        {
            await host.RunAsync<LskuRetirementRequestRecoveryRunner>(runner => runner.RunCycleAsync());
            operation = await operations.Find(x => x.TenantId == _tenant && x.LskuId == lsku).SingleAsync();
            if (operation.Checkpoint == LskuRetirementRequestCheckpoint.ManualReconciliationRequired) break;
            await Task.Delay(250);
        }

        Assert.Equal(LskuRetirementRequestWorkflowProcessor.SelfApprovalIgnored, operation.LastFailureCode);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, operation.RecoveryDisposition);
        Assert.False(operation.IsActive);
        var row = await LskuAsync(lsku);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, row.LifecycleStatus);
        Assert.Null(row.ActiveLifecycleOperation);
    }

    // ---------------------------------------------------------------------------------------------------- S1

    [Fact]
    public async Task S1_Direct_retirement_is_refused_even_with_the_key()
    {
        var (lsku, version) = await ApprovedLskuAsync("CZ");

        var response = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/retire",
            new { expectedVersion = version, reasonCode = "X" },
            host.Token(_tenant, _maker, [.. MakerPermissions, "mdm.lskus.retire"]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("LSKU_DIRECT_RETIREMENT_SYSTEM_ONLY", Envelope.FirstError(await Envelope.ReadAsync(response)));
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await LskuAsync(lsku)).LifecycleStatus);
    }

    // ---------------------------------------------------------------------------------------------------- S2

    [Fact]
    public async Task S2_A_withdrawal_cancel_that_goes_unanswered_is_kept_for_a_retry_by_the_store()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "LT");
        var version = (await LskuAsync(lsku)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(lsku, version)).StatusCode);
        host.Engine.NextCancelFails = ProductIdentityWorkflowTransportOutcome.Timeout;

        var withdraw = await SendAsync(HttpMethod.Post, $"/api/lskus/{lsku:D}/identity-approval/withdraw",
            new { expectedVersion = version + 1, reasonCode = "CHANGED_MIND" }, MakerToken());

        // FIX0: the store refused the processor's own retry (another checkpoint with a next attempt) — 409, lease held.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, withdraw.StatusCode);
        var operation = await IdentityOperationAsync(lsku, version);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown, operation.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, operation.RecoveryDisposition);
        Assert.Null(operation.LeaseOwner);
    }

    // ---------------------------------------------------------------------------------------------------- S3 / S4

    [Fact]
    public async Task S3_A_retirement_decision_whose_write_keeps_conflicting_is_stuck_with_its_cause_and_counted()
    {
        var (lsku, version) = await ApprovedLskuAsync("SK");
        Assert.Equal(HttpStatusCode.Accepted, (await RequestRetirementAsync(lsku, version)).StatusCode);
        // Someone moved the LSKU's version under the request (an old write path): the decision's write is refused.
        await host.Database.GetCollection<Lsku>("mdm_lskus").UpdateOneAsync(x => x.Id == lsku,
            Builders<Lsku>.Update.Inc(x => x.Version, 5));
        host.Engine.Approve(await RetirementInstanceAsync(lsku), host.Approver);

        var operations = host.Database.GetCollection<LskuRetirementRequestOperation>("mdm_lsku_retirement_request_operations");
        LskuRetirementRequestOperation operation = null!;
        LskuRetirementRequestRecoveryRunResult? last = null;
        for (var cycle = 0; cycle < 60; cycle++)
        {
            await host.RunAsync<LskuRetirementRequestRecoveryRunner>(async runner => last = await runner.RunCycleAsync());
            operation = await operations.Find(x => x.TenantId == _tenant && x.LskuId == lsku).SingleAsync();
            if (operation.Checkpoint == LskuRetirementRequestCheckpoint.ManualReconciliationRequired) break;
            await Task.Delay(300);
        }

        Assert.Equal(LskuRetirementRequestCheckpoint.ManualReconciliationRequired, operation.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, operation.RecoveryDisposition);
        Assert.Equal(LskuRetirementRequestWorkflowProcessor.StuckOnRecordAnswer, operation.LastFailureCode);
        Assert.True(operation.IsActive); // stuck = the lock is held, a person's (BL-530)
        await host.RunAsync<LskuRetirementRequestRecoveryRunner>(async runner => last = await runner.RunCycleAsync());
        Assert.NotNull(last?.Cycle);
        Assert.True(last!.Cycle!.NeedingAPerson >= 1);
    }

    // ---------------------------------------------------------------------------------------------------- B4

    [Fact]
    public async Task B4_In_an_enforced_tenant_the_lsku_worker_applies_an_in_scope_decision_and_leaves_the_other_waiting()
    {
        var inScopeProduct = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var outOfScopeProduct = await SeedProductAsync(ProductIdentityLifecycleStatus.IdentityApproved);
        var first = await CreatedLskuAsync(await ApprovedGskuAsync(inScopeProduct), "EE");
        var second = await CreatedLskuAsync(await ApprovedGskuAsync(outOfScopeProduct), "LV");
        var firstVersion = (await LskuAsync(first)).Version;
        var secondVersion = (await LskuAsync(second)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(first, firstVersion)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(second, secondVersion)).StatusCode);

        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(_tenant, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-1));
        rollout.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        await host.Database.GetCollection<ProductLegalEntityScopeRolloutState>("mdm_product_legal_entity_scope_rollout_states")
            .InsertOneAsync(rollout);
        await host.Database.GetCollection<ProductLegalEntityScopePolicy>("mdm_product_legal_entity_scope_policies")
            .InsertOneAsync(ProductLegalEntityScopePolicy.Create(_tenant, inScopeProduct, Guid.NewGuid(),
                ProductLegalEntityScopeMode.GroupWide, [], Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1)));

        host.Engine.Approve(host.Engine.Open("lsku", first.ToString("D")), host.Approver);
        host.Engine.Approve(host.Engine.Open("lsku", second.ToString("D")), host.Approver);
        for (var cycle = 0; cycle < 40; cycle++)
        {
            await host.RunAsync<LskuIdentityWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());
            if ((await LskuAsync(first)).LifecycleStatus == ProductIdentityLifecycleStatus.IdentityApproved) break;
            await Task.Delay(250);
        }

        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, (await LskuAsync(first)).LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await LskuAsync(second)).LifecycleStatus);
    }

    // ---------------------------------------------------------------------------------------------------- rules

    // Rule "LskuRepository Ne(SubmitterSubjectId, DecisionActorSubjectId)": the store itself refuses a decision whose
    // actor is the submitter, whatever the processor does.
    [Fact]
    public async Task The_store_refuses_an_identity_decision_taken_by_its_submitter()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "SI");
        var version = (await LskuAsync(lsku)).Version;
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(lsku, version)).StatusCode);
        var pending = await LskuAsync(lsku);
        var binding = pending.IdentityWorkflowBinding!;
        var evidence = new ProductIdentityWorkflowDecisionEvidence
        {
            WorkflowInstanceId = binding.WorkflowInstanceId, ApprovalTaskId = binding.ApprovalTaskId,
            WorkflowTemplateId = binding.WorkflowTemplateId, WorkflowTemplateVersionId = binding.WorkflowTemplateVersionId,
            ObjectType = "lsku", ObjectId = lsku, ObjectRef = binding.ObjectRef,
            DecisionActorSubjectId = binding.SubmitterSubjectId, Decision = ProductIdentityDecisionKind.Approved,
            DecisionAtUtc = DateTimeOffset.UtcNow, TransitionSequence = 2, TaskStatus = "Approved", InstanceStatus = "Completed"
        };
        var audit = Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.LskuIdentityLifecycleAuditIntentFactory
            .CreateDecision(pending, pending.Version, evidence);
        var repository = new LskuRepository(host.Database, new Tenant(_tenant));

        var result = await repository.ReconcileIdentityDecisionAsync(lsku, pending.Version, evidence, audit);

        Assert.False(result.Succeeded);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, (await LskuAsync(lsku)).LifecycleStatus);
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
        // TEST ADAPTATION (S3b, R-04): this older flow opts in to the lot / serial flags a submit now requires.
        await S3bSubmitFlags.FillForLegacyFlowAsync(host, path);
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
