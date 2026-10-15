using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.MdmService.Api.Audit;
using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Api.ModuleRegistration;
using Diten.MdmService.Api.Services.ItemMaster;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ItemMaster.Activation;
using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU04 S2 FIX1 (P-2026-10-10-FU04-S2-FIX1) — the review's blocker and required items over HTTP, the real store
/// on the throwaway replica-set mongod. The approval WORKER reads the item lists through the REAL service-identity client
/// (Platform's endpoint is faked on the wire: FakeItemListPlatform checks MDM's service token and the resolver headers).
/// </summary>
[Collection(ItemActivationHttpCollection.Name)]
public sealed class ItemS2Fix1HttpTests(ItemActivationHttpHost host) : IAsyncLifetime
{
    private static readonly string[] All =
    [
        "mdm.items.read", "mdm.items.create", "mdm.items.update", "mdm.items.submit", "mdm.items.withdraw",
        "mdm.items.deactivate", "mdm.items.retire", "mdm.product-identity.lifecycle-operations.recover"
    ];

    private const string Items = "api/mdm/items";
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _maker = Guid.NewGuid();
    private readonly Guid _approver = Guid.NewGuid();

    public Task InitializeAsync()
    {
        host.ItemReferences.Reset();
        host.ItemListPlatform.Reset();
        host.ItemListPlatform.Bearers.Clear();
        host.AfterDecisionWrite = null;
        host.Engine.LookupFailure = null;
        host.Engine.NextStartFails = null;
        host.Engine.NextStartFailure = null;
        host.Engine.NextLookupFailure = null;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        host.ItemListPlatform.Reset();
        host.AfterDecisionWrite = null;
        host.Audit.Inner = null;
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------------------------------ item 1 (blocker)

    // The worker has no HttpContext: the approval's reference check goes to Platform with MDM's SERVICE token for the
    // tenant (the GSKU workflow resolver's pattern) — and the approved item becomes Active.
    [Fact]
    public async Task Fix1_Item1_The_worker_reads_the_lists_with_MDMs_service_identity_and_the_approved_item_becomes_Active()
    {
        var id = await PendingAsync("Microcrystalline cellulose");
        host.ItemListPlatform.Bearers.Clear();
        host.ServiceIdentity.Tenants.Clear();

        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();

        Assert.Equal(ItemLifecycleStatus.Active, (await ItemAsync(id)).LifecycleStatus);
        Assert.Equal(ItemWorkflowCheckpoint.Completed, (await OperationAsync(id)).Checkpoint);
        Assert.NotEmpty(host.ItemListPlatform.Bearers);
        Assert.All(host.ItemListPlatform.Bearers, bearer => Assert.Equal(FakeItemListPlatform.ServiceToken, bearer));
        // S2 FIX2 X2: the identity is the OPERATION's tenant's (never another id of the operation).
        Assert.NotEmpty(host.ServiceIdentity.Tenants);
        Assert.All(host.ServiceIdentity.Tenants, tenant => Assert.Equal(_tenant, tenant));
    }

    // Platform refuses MDM's service identity: a named CONFIGURATION error — stuck (needing_a_person), never a wait — and
    // the approved item is not Active (fail closed). A 401 is retried once with a refreshed identity first.
    [Theory]
    [InlineData(401, 2)]
    [InlineData(403, 1)]
    public async Task Fix1_Item1_A_refused_service_identity_is_a_named_configuration_error_and_the_item_is_not_Active(
        int refusal, int calls)
    {
        var discovery = new ItemWorkflowTenantPartitionDiscoveryRepository(host.Database);
        var personBefore = await discovery.CountNeedingAPersonAsync(host.Clock.GetUtcNow().UtcTicks);
        var id = await PendingAsync("Hypromellose " + refusal);
        host.ItemListPlatform.RefuseServiceToken = refusal;
        host.ItemListPlatform.Bearers.Clear();

        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();

        var operation = await OperationAsync(id);
        // S2 FIX2 (minor M4): held on a long, uncounted retry (not parked for the maker), still a person's.
        Assert.Equal(ItemWorkflowCheckpoint.DecisionObserved, operation.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, operation.RecoveryDisposition);
        Assert.Equal(ItemReferenceEvidence.ProviderConfigurationError, operation.LastFailureCode);
        Assert.Equal(0, operation.FailedAttemptCount);
        Assert.InRange(operation.NextAttemptAtUtcTicksV1!.Value - host.Clock.GetUtcNow().UtcTicks,
            TimeSpan.FromMinutes(59).Ticks, TimeSpan.FromMinutes(61).Ticks);
        Assert.Equal(ItemLifecycleStatus.PendingActivation, (await ItemAsync(id)).LifecycleStatus);
        Assert.Equal(calls, host.ItemListPlatform.Bearers.Count);
        Assert.Equal(personBefore + 1, await discovery.CountNeedingAPersonAsync(host.Clock.GetUtcNow().UtcTicks));
        // S2 lane merge (minor): the park is one Error line (the processor's), not two.
        Assert.Single(host.Logs.Entries, entry => entry.Level == LogLevel.Error
            && entry.Message.Contains(operation.OperationId.ToString(), StringComparison.Ordinal)
            && entry.Message.Contains(ItemReferenceEvidence.ProviderConfigurationError, StringComparison.Ordinal));

        // The configuration is fixed: the next ask, an hour on, applies the approval — no one had to submit again.
        host.ItemListPlatform.RefuseServiceToken = null;
        using (host.Clock.Shift(TimeSpan.FromMinutes(61))) await CycleAsync();
        Assert.Equal(ItemLifecycleStatus.Active, (await ItemAsync(id)).LifecycleStatus);
    }

    // ------------------------------------------------------------------------------------------ item 2

    // The approval on and no worker carrying it: counted once it has been untouched past the threshold; a running worker
    // touches it (an open approval is asked again) and it is no longer counted.
    [Fact]
    public async Task Fix1_Item2_An_approval_nobody_processes_is_counted_and_a_running_worker_clears_it()
    {
        var id = await PendingAsync("Colloidal silica");
        var after = new ItemActivationGaugeOptions().UnprocessedAfter;
        // S2 FIX3 item 2: the default covers the 1-hour configuration retry.
        Assert.Equal(TimeSpan.FromMinutes(90), after);

        using (host.Clock.Shift(after + TimeSpan.FromMinutes(1)))
        {
            Assert.Equal(1, await UnprocessedOfThisTenantAsync(after));
            await CycleAsync();
            Assert.Equal(0, await UnprocessedOfThisTenantAsync(after));
        }
        Assert.Equal(ItemWorkflowCheckpoint.AwaitingDecision, (await OperationAsync(id)).Checkpoint);
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public async Task Fix1_Item2_Approval_on_with_this_instances_worker_off_is_warned_at_start_never_refused(
        bool approval, bool worker, bool warned)
    {
        var logs = new Recording();
        var warning = new ItemActivationWorkerOffWarning(
            Options.Create(new ItemActivationWorkflowOptions { Enabled = approval }),
            Options.Create(new ItemActivationWorkflowWorkerOptions { Enabled = worker }),
            logs);

        await warning.StartAsync(CancellationToken.None);

        Assert.Equal(warned, logs.Entries.Any(x => x.Level == LogLevel.Warning && x.Message == ItemActivationWorkerOffWarning.Message));
        Assert.Contains(typeof(ItemActivationWorkerOffWarning), host.RegisteredHostedServices);
    }

    // ------------------------------------------------------------------------------------------ item 3

    // An Active item takes a name edit between the decision's write and the processor's check of it: the approval is
    // still recognised by what it wrote (binding + terminal decision + status), and completes.
    [Fact]
    public async Task Fix1_Item3_A_name_edit_between_the_decision_write_and_its_check_still_completes()
    {
        var id = await PendingAsync("Mannitol");
        host.AfterDecisionWrite = async (_, written) =>
        {
            host.AfterDecisionWrite = null;
            await EditNameAsync(id, 2, "Mannitol (renamed)");
            return written;
        };

        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();

        var item = await ItemAsync(id);
        Assert.Equal((ItemLifecycleStatus.Active, 3, "Mannitol (renamed)"), (item.LifecycleStatus, item.Version, item.Name));
        Assert.Equal(ItemWorkflowCheckpoint.Completed, (await OperationAsync(id)).Checkpoint);
    }

    // The decision was written, its answer lost, then the item was renamed: the replay is recognised by the decision it
    // wrote (not an exact version) — the operation completes instead of retrying into a permanent conflict.
    [Fact]
    public async Task Fix1_Item3_A_replayed_decision_is_recognised_after_a_name_edit()
    {
        var id = await PendingAsync("Sorbitan oleate");
        host.AfterDecisionWrite = async (_, _) =>
        {
            host.AfterDecisionWrite = null;
            await EditNameAsync(id, 2, "Sorbitan oleate (renamed)");
            return new ItemWorkflowWriteResult(false, false, null, ProductIdentityStore.Unavailable);
        };

        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();
        Assert.Equal(ItemWorkflowCheckpoint.ApprovalValidated, (await OperationAsync(id)).Checkpoint);
        using (host.Clock.Shift(TimeSpan.FromMinutes(1))) await CycleAsync();

        var item = await ItemAsync(id);
        Assert.Equal((ItemLifecycleStatus.Active, 3), (item.LifecycleStatus, item.Version));
        Assert.Equal(ItemWorkflowCheckpoint.Completed, (await OperationAsync(id)).Checkpoint);
    }

    // ------------------------------------------------------------------------------------------ T4: the gate, twice

    // The item moved outside MDM's rules between the approval's validation and its write (lot control cleared): the
    // gate is checked again as the decision is applied — not applied, the item goes back with the named code.
    [Fact]
    public async Task Fix1_T4_The_activation_gate_is_checked_again_when_the_decision_is_applied()
    {
        var id = await PendingAsync("Calcium phosphate");
        host.ItemListPlatform.OnNextResolve = () => RawItems().UpdateOneAsync(Filter(id),
            new BsonDocument("$set", new BsonDocument(nameof(Item.LotControlled), BsonNull.Value)));

        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();

        var operation = await OperationAsync(id);
        Assert.Equal(ItemWorkflowCheckpoint.DecisionNotApplicable, operation.Checkpoint);
        Assert.Equal(ItemActivationGate.StockFieldsRequired, operation.LastFailureCode);
        Assert.Equal(ItemLifecycleStatus.Draft, (await ItemAsync(id)).LifecycleStatus);
    }

    // ------------------------------------------------------------------------------------------ item 5

    // The start's answer was lost and the lookup failed once: the WORKER learns the start and puts the item pending — an
    // audited move (path b), the system's act.
    [Fact]
    public async Task Fix1_Item5_The_item_the_worker_puts_pending_is_audited()
    {
        var id = await CreatedAsync("Stearic acid");
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "FAKE_ENGINE_TIMEOUT", true);
        host.Engine.NextLookupFailure = (ProductIdentityWorkflowTransportOutcome.Retryable, "FAKE_LOOKUP_RETRY");

        var submit = await PostAsync(id, "submit", Lifecycle(0));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, submit.StatusCode);
        Assert.Equal(ItemLifecycleStatus.Draft, (await ItemAsync(id)).LifecycleStatus);
        using (host.Clock.Shift(TimeSpan.FromMinutes(1))) await CycleAsync();

        Assert.Equal(ItemLifecycleStatus.PendingActivation, (await ItemAsync(id)).LifecycleStatus);
        var record = Assert.Single(Trail(id, nameof(ApplyItemActivationDecisionCommand)));
        Assert.Equal(1, record.Outcome);
        Assert.True(record.SystemActor);
        Assert.Equal("Pending", record.Metadata![ItemAudit.Decision]);
        Assert.Equal(_maker.ToString("D"), record.Metadata[ItemAudit.MakerId]);
        Assert.Equal(("Draft", "PendingActivation"),
            ((string?)record.BeforeState![nameof(Item.LifecycleStatus)], (string?)record.AfterState![nameof(Item.LifecycleStatus)]));
    }

    // The engine answered the maker's cancel but the request ended before the item moved: the WORKER finishes the
    // withdrawal — an audited move, the system's act, the item back to Draft.
    [Fact]
    public async Task Fix1_Item5_A_withdrawal_the_worker_finishes_is_audited()
    {
        var id = await PendingAsync("Glyceryl behenate");
        var operation = await OperationAsync(id);
        var instance = host.Engine.Open("Item", id.ToString("D"));
        var commandId = Guid.NewGuid();
        // The state a withdrawal reaches when the engine's cancel was answered (WithdrawalObserved), its item move not made.
        await RawOperations().UpdateOneAsync(x => x.Id == operation.Id, Builders<ItemWorkflowOperation>.Update
            .Set(x => x.Checkpoint, ItemWorkflowCheckpoint.WithdrawalObserved)
            .Set(x => x.WithdrawalCommandId, commandId)
            .Set(x => x.WithdrawalFingerprint, new string('a', 64))
            .Set(x => x.WithdrawalRequesterSubjectId, _maker)
            .Set(x => x.WithdrawalExpectedItemVersion, 1)
            .Set(x => x.WithdrawalReasonCode, ItemActivationWorkflowProcessor.WithdrawalReasonCode)
            .Set(x => x.WithdrawalExpectedWorkflowInstanceVersion, 1)
            .Set(x => x.WithdrawalExpectedApprovalTaskVersion, 1)
            .Set(x => x.WithdrawalTransitionLogId, Guid.NewGuid())
            .Set(x => x.WithdrawalObservedAtUtcTicksV1, host.Clock.GetUtcNow().UtcTicks)
            .Set(x => x.WithdrawalTransitionSequence, instance.Sequence + 1)
            .Set(x => x.WithdrawalResultWorkflowInstanceVersion, 2)
            .Set(x => x.WithdrawalResultApprovalTaskVersion, 2)
            .Set(x => x.WithdrawalTaskStatus, "Cancelled")
            .Set(x => x.WithdrawalInstanceStatus, "Cancelled")
            .Set(x => x.WithdrawalObjectRef, operation.ObjectRef)
            .Set(x => x.NextAttemptAtUtcTicksV1, null)
            .Set(x => x.LeaseOwner, null));

        await CycleAsync();

        Assert.Equal(ItemLifecycleStatus.Draft, (await ItemAsync(id)).LifecycleStatus);
        var record = Assert.Single(Trail(id, nameof(ApplyItemActivationDecisionCommand)));
        Assert.Equal(1, record.Outcome);
        Assert.True(record.SystemActor);
        Assert.Equal("Withdrawn", record.Metadata![ItemAudit.Decision]);
        Assert.Equal(_maker.ToString("D"), record.Metadata[ItemAudit.RequesterId]);
    }

    // The decision's write is refused attempt after attempt (the item moved under it): ONE failed record for the
    // operation (and one per change of code) — never one per retry.
    [Fact]
    public async Task Fix1_Item5_A_decision_refused_again_and_again_is_recorded_once()
    {
        var id = await PendingAsync("Sodium starch glycolate");
        await RawItems().UpdateOneAsync(Filter(id), new BsonDocument("$inc", new BsonDocument(nameof(Item.Version), 5)));

        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();
        for (var attempt = 1; attempt <= 3; attempt++)
            using (host.Clock.Shift(TimeSpan.FromMinutes(attempt * 20))) await CycleAsync();

        Assert.True((await OperationAsync(id)).FailedAttemptCount >= 3);
        Assert.Single(Trail(id, nameof(ApplyItemActivationDecisionCommand)), record => record.Outcome == 2);
    }

    // The approver's decision applied during the MAKER's replay (the same submit, a retry due) is the system's act — the
    // record never names the maker as its actor; who decided travels in its metadata.
    [Fact]
    public async Task Fix1_Item5_A_decision_applied_in_the_makers_replay_is_the_systems_act_not_the_makers()
    {
        var wire = CaptureTheProductionForwarder();
        var id = await PendingAsync("Polysorbate 80");
        await RawItems().UpdateOneAsync(Filter(id), new BsonDocument("$inc", new BsonDocument(nameof(Item.Version), 5)));
        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();
        Assert.Equal(ItemWorkflowCheckpoint.ApprovalValidated, (await OperationAsync(id)).Checkpoint);
        await RawItems().UpdateOneAsync(Filter(id), new BsonDocument("$inc", new BsonDocument(nameof(Item.Version), -5)));

        using (host.Clock.Shift(TimeSpan.FromMinutes(1)))
        {
            var replay = await PostAsync(id, "submit", Lifecycle(0));
            Assert.True(replay.IsSuccessStatusCode, await replay.Content.ReadAsStringAsync());
        }

        Assert.Equal(ItemLifecycleStatus.Active, (await ItemAsync(id)).LifecycleStatus);
        var record = Assert.Single(Trail(id, nameof(ApplyItemActivationDecisionCommand)), x => x.Outcome == 1);
        Assert.True(record.SystemActor);
        Assert.Equal(_approver.ToString("D"), record.Metadata![ItemAudit.DecisionActorId]);
        var body = wire.Select(text => JsonDocument.Parse(text).RootElement)
            .Last(x => x.GetProperty("requestType").GetString() == nameof(ApplyItemActivationDecisionCommand)
                       && x.GetProperty("outcome").GetInt32() == 1);
        Assert.Equal(4, body.GetProperty("actorType").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("actorId").ValueKind);
    }

    // ------------------------------------------------------------------------------------------ item 6

    // CT decision A: the reactivation's reason is kept with its request (once, in its fingerprint: another reason under
    // the same request is 409 IDEMPOTENCY); the item's DETAIL shows it while the reactivation is pending — never a list,
    // never another tenant, hidden once decided; the audit record carries only "given" and its length.
    [Fact]
    public async Task Fix1_Item6_The_reactivation_reason_is_kept_shown_in_the_detail_only_while_pending_and_never_audited()
    {
        const string reason = "Supplier requalified";
        var id = await DeactivatedAsync("Lactitol");
        var version = (await ItemAsync(id)).Version;

        var requested = await PostAsync(id, "reactivation-requests", Lifecycle(version, reason));

        Assert.True(requested.StatusCode == HttpStatusCode.Accepted, await requested.Content.ReadAsStringAsync());
        Assert.Equal(reason, (await OperationAsync(id)).RequestReason);
        Assert.Equal(reason, await DetailReasonAsync(Client(), id));
        var list = await Envelope.ReadAsync(await Client().GetAsync($"{Items}?pageSize=100"));
        Assert.DoesNotContain(reason, list.GetRawText(), StringComparison.Ordinal);
        var stranger = host.Client(host.Token(Guid.NewGuid(), Guid.NewGuid(), All));
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{Items}/{id}")).StatusCode);
        var noRead = host.Client(host.Token(_tenant, _maker, "mdm.items.create"));
        Assert.Equal(HttpStatusCode.Forbidden, (await noRead.GetAsync($"{Items}/{id}")).StatusCode);

        var other = await PostAsync(id, "reactivation-requests", Lifecycle(version, "Another reason"));
        Assert.Equal(HttpStatusCode.Conflict, other.StatusCode);
        Assert.Equal(ItemActivationWorkflowProcessor.IdempotencyConflict, await FirstErrorAsync(other));

        var record = Trail(id, nameof(RequestItemReactivationCommand)).First(x => x.Outcome == 1);
        Assert.Equal(true, record.Metadata![ItemAudit.ReasonProvided]);
        Assert.Equal(reason.Length, record.Metadata[ItemAudit.ReasonLength]);
        Assert.DoesNotContain(host.Audit.Forwarded, x => JsonSerializer.Serialize(x).Contains(reason, StringComparison.Ordinal));

        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();
        Assert.Equal(ItemLifecycleStatus.Active, (await ItemAsync(id)).LifecycleStatus);
        Assert.Null(await DetailReasonAsync(Client(), id));
        // S2 FIX2 (minor M6): the text leaves the item document with the decision; the operation keeps it.
        Assert.Null((await ItemAsync(id)).ActivationRequestReason);
        Assert.Equal(reason, (await OperationAsync(id)).RequestReason);
    }

    [Fact]
    public async Task Fix1_Item6_A_reason_with_a_control_character_or_over_the_limit_is_refused()
    {
        var id = await DeactivatedAsync("Maltitol");
        var version = (await ItemAsync(id)).Version;

        Assert.Equal(HttpStatusCode.BadRequest,
            (await PostAsync(id, "reactivation-requests", Lifecycle(version, "line\nbreak"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await PostAsync(id, "reactivation-requests", Lifecycle(version, new string('r', 501)))).StatusCode);
        Assert.Equal(ItemLifecycleStatus.Deactivated, (await ItemAsync(id)).LifecycleStatus);
    }

    private static async Task<string?> DetailReasonAsync(HttpClient client, Guid id)
    {
        var detail = await Envelope.ReadAsync(await client.GetAsync($"{Items}/{id}"));
        return detail.GetProperty("data").GetProperty("pendingReactivationReason").GetString();
    }

    // ------------------------------------------------------------------------------------------ minor

    // An S1 row never stored its storage condition's kind: the submit freezes the kind the list gives, and the approval is
    // held to it — the list changing that kind is a named refusal, not an unseen pass.
    [Fact]
    public async Task Fix1_Minor_An_S1_item_without_stored_kinds_is_held_to_the_kind_frozen_at_the_submit()
    {
        var id = await CreatedAsync("Benzyl alcohol");
        await RawItems().UpdateOneAsync(Filter(id), new BsonDocument("$unset",
            new BsonDocument(nameof(Item.StorageConditionKinds), "")));
        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync(id, "submit", Lifecycle(0))).StatusCode);
        var frozen = Assert.Single((await OperationAsync(id)).ReferenceEvidence!, x => x.ValueCode == "AMBIENT_15_25");
        Assert.Equal("TEMPERATURE", frozen.Kind);

        host.ItemReferences.KindOverride["AMBIENT_15_25"] = "PROTECTION";
        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();

        var operation = await OperationAsync(id);
        Assert.Equal(ItemWorkflowCheckpoint.DecisionNotApplicable, operation.Checkpoint);
        Assert.Equal(ItemReferenceEvidence.ValueChanged, operation.LastFailureCode);
    }

    // An approval still open is not a failure: asked again and again, it never moves toward the retry limit.
    [Fact]
    public async Task Fix1_Minor_An_open_approval_never_counts_toward_the_retry_limit()
    {
        var id = await PendingAsync("Ethylcellulose");

        for (var poll = 1; poll <= 4; poll++)
            using (host.Clock.Shift(TimeSpan.FromMinutes(poll * 20))) await CycleAsync();

        var operation = await OperationAsync(id);
        Assert.Equal(ItemWorkflowCheckpoint.AwaitingDecision, operation.Checkpoint);
        Assert.Equal(0, operation.FailedAttemptCount);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, operation.RecoveryDisposition);
        Assert.NotEqual(ItemActivationWorkflowProcessor.RetryLimitReached, operation.LastFailureCode);
    }

    [Theory]
    [InlineData(14)]
    [InlineData(525_601)]
    [InlineData(int.MaxValue)]
    public async Task Fix1_Minor_The_orphan_repair_age_outside_its_bounds_is_400_by_name(int minutes)
    {
        var repair = await Client().PostAsync($"{Items}/operator/orphan-code-repairs?limit=10&minimumAgeMinutes={minutes}", null);

        Assert.Equal(HttpStatusCode.BadRequest, repair.StatusCode);
        Assert.Equal("ITEM_ORPHAN_CODE_REPAIR_INVALID", await FirstErrorAsync(repair));
    }

    // The store holds maker ≠ approver by name (the argument guard) AND against the persisted maker in the write itself:
    // a caller that names the wrong maker cannot write the maker's own decision.
    [Fact]
    public async Task Fix1_Minor_The_store_refuses_the_makers_own_decision_by_name_and_against_the_persisted_maker()
    {
        var id = await PendingAsync("Shellac");
        var operation = await OperationAsync(id);
        var store = new ItemWorkflowOperationRepository(host.Database, new Tenant(_tenant));
        var claim = new ItemWorkflowClaim(_tenant, operation.OperationId, id, operation.OperationFingerprint, "store-test", 1,
            ItemWorkflowCheckpoint.ApprovalValidated, host.Clock.GetUtcNow().AddMinutes(1).UtcTicks);

        var named = await store.ApplyDecisionAsync(claim, operation, Decision(operation, _maker), host.Clock.GetUtcNow().UtcTicks);
        Assert.Equal("ITEM_ACTIVATION_DECISION_CONTRACT_INVALID", named.ErrorCode);

        operation.MakerSubjectId = Guid.NewGuid(); // the caller names the wrong maker
        var slipped = await store.ApplyDecisionAsync(claim, operation, Decision(operation, _maker), host.Clock.GetUtcNow().UtcTicks);
        Assert.False(slipped.Succeeded);
        var item = await ItemAsync(id);
        Assert.Equal(ItemLifecycleStatus.PendingActivation, item.LifecycleStatus);
        Assert.Null(item.ActivationWorkflowBinding!.TerminalDecision);
    }

    // Only the persisted maker withdraws: the store refuses another requester by name.
    [Fact]
    public async Task Fix1_Minor_The_store_refuses_a_withdrawal_by_anyone_but_the_maker_by_name()
    {
        var id = await PendingAsync("Carnauba wax");
        var operation = await OperationAsync(id);
        operation.WithdrawalCommandId = Guid.NewGuid();
        operation.WithdrawalExpectedItemVersion = 1;
        var store = new ItemWorkflowOperationRepository(host.Database, new Tenant(_tenant));
        var claim = new ItemWorkflowClaim(_tenant, operation.OperationId, id, operation.OperationFingerprint, "store-test", 1,
            ItemWorkflowCheckpoint.WithdrawalObserved, host.Clock.GetUtcNow().AddMinutes(1).UtcTicks);
        var evidence = new Diten.MdmService.Domain.ValueObjects.ProductIdentityWorkflowCancellationEvidence
        {
            WorkflowInstanceId = operation.WorkflowInstanceId!.Value, ApprovalTaskId = operation.ApprovalTaskId!.Value,
            RequesterSubjectId = Guid.NewGuid(), IdempotencyKey = operation.WithdrawalCommandId.Value.ToString("D"),
            TaskStatus = "Cancelled", InstanceStatus = "Cancelled", ReasonCode = ItemActivationWorkflowProcessor.WithdrawalReasonCode
        };

        var refused = await store.ApplyWithdrawalAsync(claim, operation, evidence, host.Clock.GetUtcNow().UtcTicks);

        Assert.Equal("ITEM_ACTIVATION_WITHDRAWAL_CONTRACT_INVALID", refused.ErrorCode);
        Assert.Equal(ItemLifecycleStatus.PendingActivation, (await ItemAsync(id)).LifecycleStatus);
    }

    // ================================================================================== S2 FIX2 (P-2026-10-10-FU04-S2-FIX2)

    // Item 1: the maker's repeat that applies the approver's decision — the request's record never says the decision's
    // move (Pending → Active); that move is the system's record only.
    [Fact]
    public async Task Fix2_Item1_A_request_record_never_carries_the_approvers_decision()
    {
        var id = await PendingAsync("Croscarmellose sodium");
        await RawItems().UpdateOneAsync(Filter(id), new BsonDocument("$inc", new BsonDocument(nameof(Item.Version), 5)));
        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();
        await RawItems().UpdateOneAsync(Filter(id), new BsonDocument("$inc", new BsonDocument(nameof(Item.Version), -5)));
        using (host.Clock.Shift(TimeSpan.FromMinutes(1)))
            Assert.True((await PostAsync(id, "submit", Lifecycle(0))).IsSuccessStatusCode);
        Assert.Equal(ItemLifecycleStatus.Active, (await ItemAsync(id)).LifecycleStatus);

        var requests = Trail(id, nameof(SubmitItemCommand)).ToList();
        Assert.True(requests.Count >= 2);
        Assert.All(requests, record =>
        {
            var after = record.AfterState?.GetValueOrDefault(nameof(Item.LifecycleStatus)) as string;
            Assert.True(after is null or "PendingActivation", $"a request record says {after}");
        });
        Assert.Single(Trail(id, nameof(ApplyItemActivationDecisionCommand)), x => x.Outcome == 1 && x.SystemActor);
    }

    // Item 2: an open approval is asked less often as it ages — half its age, from the retry delay up to the 15-minute
    // ceiling — never counted.
    [Fact]
    public async Task Fix2_Item2_An_open_approval_is_asked_less_often_as_it_ages_up_to_the_ceiling()
    {
        var id = await PendingAsync("Sodium lauryl sulfate");
        var started = (await OperationAsync(id)).WorkflowStartedAtUtcTicksV1!.Value;

        using (host.Clock.Shift(TimeSpan.FromSeconds(40))) await CycleAsync();
        var young = await OperationAsync(id);
        var youngAge = young.UpdatedAtUtcTicksV1 - started;
        Assert.InRange(young.NextAttemptAtUtcTicksV1!.Value - young.UpdatedAtUtcTicksV1, youngAge / 2 - TimeSpan.TicksPerSecond,
            youngAge / 2 + TimeSpan.TicksPerSecond);

        using (host.Clock.Shift(TimeSpan.FromHours(3))) await CycleAsync();
        var old = await OperationAsync(id);
        Assert.Equal(ItemActivationWorkflowProcessor.DecisionPollCeiling.Ticks,
            old.NextAttemptAtUtcTicksV1!.Value - old.UpdatedAtUtcTicksV1);
        Assert.Equal(0, old.FailedAttemptCount);
        Assert.Equal(ItemWorkflowCheckpoint.AwaitingDecision, old.Checkpoint);
    }

    // Item 2: the unprocessed threshold is checked against the worker's poll and lease at start (never clamped).
    [Fact]
    public void Fix2_Item2_The_unprocessed_threshold_is_validated_against_the_worker_at_start()
    {
        var worker = new ItemActivationWorkflowWorkerOptions { PollIntervalSeconds = 3600, LeaseSeconds = 10 };
        Assert.False(new ItemActivationGaugeOptions { UnprocessedAfterMinutes = 120 }.IsValid(worker));
        Assert.True(new ItemActivationGaugeOptions { UnprocessedAfterMinutes = 150 }.IsValid(worker));
        // S2 FIX3 item 2: with the default worker, the threshold must cover the 1-hour configuration retry.
        Assert.False(new ItemActivationGaugeOptions { UnprocessedAfterMinutes = 60 }.IsValid(new()));
        Assert.True(new ItemActivationGaugeOptions { UnprocessedAfterMinutes = 90 }.IsValid(new()));
        Assert.False(new ItemActivationGaugeOptions { UnprocessedAfterMinutes = 19 }.IsValid(new()));
        Assert.False(new ItemActivationGaugeOptions { UnprocessedAfterMinutes = 1441 }.IsValid(new()));
        // S2 lane merge (minor): the default is derived from the processor's waits; the worker's retry delay counts too.
        Assert.Equal(90, new ItemActivationGaugeOptions().UnprocessedAfterMinutes);
        Assert.Equal((int)Math.Ceiling(ItemActivationGaugeOptions.LongestPlannedWait.TotalMinutes * 1.5),
            ItemActivationGaugeOptions.DefaultUnprocessedAfterMinutes);
        Assert.Equal(TimeSpan.FromHours(2),
            ItemActivationGaugeOptions.LongestPlannedWaitFor(new ItemActivationWorkflowWorkerOptions { RetryDelaySeconds = 7200 }));
        Assert.Equal(ItemActivationGaugeOptions.LongestPlannedWait,
            ItemActivationGaugeOptions.LongestPlannedWaitFor(new ItemActivationWorkflowWorkerOptions { RetryDelaySeconds = 30 }));

        // The host's own start validation holds the same rule (a bad value would stop the start).
        var validators = host.Factory.Services.GetServices<IValidateOptions<ItemActivationGaugeOptions>>().ToList();
        Assert.Contains(validators, validator =>
            validator.Validate(Options.DefaultName, new ItemActivationGaugeOptions { UnprocessedAfterMinutes = 120 }).Failed);
    }

    // Item 2: a worker that claims and drops (crashes) every cycle is not progress — the operation is still counted.
    [Fact]
    public async Task Fix2_Item2_A_claim_without_a_move_is_not_progress()
    {
        var id = await PendingAsync("Cetyl alcohol");
        var after = TimeSpan.FromMinutes(90);
        using (host.Clock.Shift(after + TimeSpan.FromMinutes(1)))
        {
            var operation = await OperationAsync(id);
            var store = new ItemWorkflowOperationRepository(host.Database, new Tenant(_tenant));
            var now = host.Clock.GetUtcNow();
            Assert.NotNull(await store.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
                [operation.Checkpoint], "crashing-worker", now.UtcTicks, now.AddSeconds(10).UtcTicks)));

            Assert.Equal(1, await UnprocessedOfThisTenantAsync(after));
        }
    }

    // Item 2 / X1: the gauge records the unprocessed count (cutoff = now − threshold) and the oldest open approval's age.
    [Fact]
    public async Task Fix2_Item2_The_gauge_records_unprocessed_and_the_oldest_open_approvals_age()
    {
        var id = await PendingAsync("Glyceryl monostearate");
        var started = (await OperationAsync(id)).WorkflowStartedAtUtcTicksV1!.Value;
        var metrics = host.Factory.Services.GetRequiredService<Diten.MdmService.Api.Services.ProductItemSkuMaster.ProductIdentityRecoveryMetrics>();
        var after = host.Factory.Services.GetRequiredService<IOptions<ItemActivationGaugeOptions>>().Value.UnprocessedAfter;
        var discovery = new ItemWorkflowTenantPartitionDiscoveryRepository(host.Database);

        await host.RunAsync<ItemActivationGauge>(gauge => gauge.RecordAsync(CancellationToken.None));

        var now = host.Clock.GetUtcNow();
        Assert.Equal(await discovery.CountUnprocessedAsync((now - after).UtcTicks), metrics.Current(ItemActivationGauge.UnprocessedFlow));
        // S2 FIX3 item 3: on its own gauge (seconds), never on the stuck-operations series.
        Assert.True(metrics.OldestOpenApproval(ItemActivationWorkflowRecoveryRunner.Flow)
            >= (now.UtcTicks - started) / TimeSpan.TicksPerSecond - 1);
        Assert.Null(metrics.Current(ItemActivationGauge.OldestOpenApprovalFlow));
    }

    // M1 (CT decision: least privilege): the maker's HTTP repeat reads the lists with the USER's token — the service
    // identity is the worker's only.
    [Fact]
    public async Task Fix2_M1_The_makers_repeat_reads_the_lists_with_the_users_token_never_the_service_identity()
    {
        var id = await PendingAsync("Calcium carbonate");
        host.ItemReferences.Outage = true;
        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();
        Assert.Equal(ItemWorkflowCheckpoint.DecisionObserved, (await OperationAsync(id)).Checkpoint);
        host.ItemReferences.Outage = false;
        host.ItemListPlatform.Bearers.Clear();
        var userCalls = host.ItemReferences.Calls.Count;

        using (host.Clock.Shift(TimeSpan.FromMinutes(1)))
            Assert.True((await PostAsync(id, "submit", Lifecycle(0))).IsSuccessStatusCode);

        Assert.Equal(ItemLifecycleStatus.Active, (await ItemAsync(id)).LifecycleStatus);
        Assert.Empty(host.ItemListPlatform.Bearers);
        Assert.True(host.ItemReferences.Calls.Count > userCalls);
    }

    // M3: the item is deactivated right after the decision's write (inside the audit window) — still completes.
    [Fact]
    public async Task Fix2_M3_A_deactivation_right_after_the_decision_write_still_completes()
    {
        var id = await PendingAsync("Ascorbyl palmitate");
        host.AfterDecisionWrite = async (_, written) =>
        {
            host.AfterDecisionWrite = null;
            var deactivated = await PostAsync(id, "deactivate", Lifecycle(2, "withdrawn from use"));
            Assert.True(deactivated.IsSuccessStatusCode, await deactivated.Content.ReadAsStringAsync());
            return written;
        };

        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();

        Assert.Equal(ItemLifecycleStatus.Deactivated, (await ItemAsync(id)).LifecycleStatus);
        Assert.Equal(ItemWorkflowCheckpoint.Completed, (await OperationAsync(id)).Checkpoint);
    }

    // M7: the maker's withdrawal does not wait for a scheduled ask of the open approval (no 409 BUSY).
    [Fact]
    public async Task Fix2_M7_The_makers_withdrawal_does_not_wait_for_a_scheduled_ask()
    {
        var id = await PendingAsync("Shellac glaze");
        using (host.Clock.Shift(TimeSpan.FromMinutes(20)))
        {
            await CycleAsync();
            Assert.True((await OperationAsync(id)).NextAttemptAtUtcTicksV1 > host.Clock.GetUtcNow().UtcTicks);

            var withdrawn = await PostAsync(id, "withdraw", Lifecycle(1));

            Assert.True(withdrawn.IsSuccessStatusCode, await withdrawn.Content.ReadAsStringAsync());
            Assert.Equal(ItemLifecycleStatus.Draft, (await ItemAsync(id)).LifecycleStatus);
        }
    }

    // M8: the repair age's upper bound itself is allowed.
    [Fact]
    public async Task Fix2_M8_The_orphan_repair_age_at_its_upper_bound_is_allowed()
    {
        var repair = await Client().PostAsync($"{Items}/operator/orphan-code-repairs?limit=10&minimumAgeMinutes=525600", null);

        Assert.True(repair.IsSuccessStatusCode, await repair.Content.ReadAsStringAsync());
    }

    // X4: a replay is only THIS decision — another actor, task or approval under the same item is not a replay.
    [Theory]
    [InlineData("actor")]
    [InlineData("task")]
    [InlineData("start-key")]
    // S2 lane merge E1: the decision kind, the transition sequence and the instance pin the replay too. Each case passes
    // the contract check (the claim's checkpoint follows the kind; the operation names the other instance), so it is the
    // replay recognition itself that must refuse it.
    [InlineData("decision")]
    [InlineData("sequence")]
    [InlineData("instance")]
    public async Task Fix2_X4_A_different_decision_is_never_taken_for_a_replay(string differs)
    {
        var id = await PendingAsync("Polyvinyl alcohol " + differs);
        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();
        var item = await ItemAsync(id);
        var operation = await OperationAsync(id);
        var written = item.ActivationWorkflowBinding!.TerminalDecision!;
        var evidence = Decision(operation, written.DecisionActorSubjectId);
        evidence.ApprovalTaskId = written.ApprovalTaskId;
        evidence.TransitionSequence = written.TransitionSequence;
        if (differs == "actor") evidence.DecisionActorSubjectId = Guid.NewGuid();
        if (differs == "task") evidence.ApprovalTaskId = Guid.NewGuid();
        if (differs == "start-key") operation.StartIdempotencyKey = "item-activation:another-request";
        if (differs == "decision") evidence.Decision = ProductIdentityDecisionKind.Rejected;
        if (differs == "sequence") evidence.TransitionSequence = written.TransitionSequence + 1;
        if (differs == "instance")
        {
            var another = Guid.NewGuid();
            operation.WorkflowInstanceId = another;
            evidence.WorkflowInstanceId = another;
        }
        var store = new ItemWorkflowOperationRepository(host.Database, new Tenant(_tenant));
        var claim = new ItemWorkflowClaim(_tenant, operation.OperationId, id, operation.OperationFingerprint, "store-test", 1,
            differs == "decision" ? ItemWorkflowCheckpoint.DecisionObserved : ItemWorkflowCheckpoint.ApprovalValidated,
            host.Clock.GetUtcNow().AddMinutes(1).UtcTicks);

        var replay = await store.ApplyDecisionAsync(claim, operation, evidence, host.Clock.GetUtcNow().UtcTicks);

        Assert.False(replay.Succeeded);
        Assert.False(replay.IsReplay);
    }

    // X3: a system record never carries the request's user e-mail.
    [Fact]
    public async Task Fix2_X3_A_system_record_carries_no_user_email()
    {
        var wire = new ConcurrentQueue<string>();
        var http = new DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [
                new System.Security.Claims.Claim("sub", _maker.ToString("D")),
                new System.Security.Claims.Claim("actor_type", "tenant_user"),
                new System.Security.Claims.Claim("tenant_id", _tenant.ToString("D")),
                new System.Security.Claims.Claim("email", "maker@example.test")
            ], "Bearer"))
        };
        var forwarder = new PlatformAuditForwarder(new CapturingFactory(wire),
            Options.Create(new PlatformRegistrationOptions { BaseUrl = "http://platform.test", InternalApiKey = "test-only" }),
            new HttpContextAccessor { HttpContext = http }, NullLogger<PlatformAuditForwarder>.Instance);

        await forwarder.ForwardAsync(new Diten.MdmService.Application.Contracts.Audit.AuditForwardRequest(
            nameof(ApplyItemActivationDecisionCommand), 1, 4, "Item", "product-item-sku-master", Guid.NewGuid(), 1,
            SystemActor: true));

        var body = JsonDocument.Parse(Assert.Single(wire)).RootElement;
        Assert.Equal(4, body.GetProperty("actorType").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("actorId").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("actorEmail").ValueKind);
    }

    // ================================================================================== S2 FIX3 (P-2026-10-11-MDM-S2-LANE-MERGE-01 A)

    // Item 1: the maker's OWN token refused on their repeat is not MDM's configuration — the maker is answered 503, the
    // operation stays on its ordinary retry (never parked for the hour, never blamed on the service identity).
    // S2 lane merge E5: both refusals of the maker's token (401 and 403), the code the maker reads, and the operation's
    // failure series untouched (the maker's path is never counted — only the worker's is).
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task Fix3_Item1_The_makers_own_token_refused_is_503_and_the_operation_is_not_parked(int refusal)
    {
        var id = await PendingAsync("Sodium benzoate " + refusal);
        host.ItemReferences.Outage = true;
        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();
        Assert.Equal(ItemWorkflowCheckpoint.DecisionObserved, (await OperationAsync(id)).Checkpoint);
        host.ItemReferences.Outage = false;
        host.ItemReferences.RefuseStatus = refusal;
        var before = (await OperationAsync(id)).FailedAttemptCount;

        using (host.Clock.Shift(TimeSpan.FromMinutes(1)))
        {
            var replay = await PostAsync(id, "submit", Lifecycle(0));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, replay.StatusCode);
            using var body = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
            Assert.Equal("ITEM_REFERENCE_LIST_UNAVAILABLE",
                (body.RootElement.TryGetProperty("errors", out var errors) ? errors : body.RootElement.GetProperty("Errors"))[0].GetString());
            var operation = await OperationAsync(id);
            Assert.Equal(before, operation.FailedAttemptCount);
            Assert.Equal(ItemWorkflowCheckpoint.DecisionObserved, operation.Checkpoint);
            Assert.Equal(ItemReferenceEvidence.ProviderUnavailable, operation.LastFailureCode);
            Assert.True(operation.NextAttemptAtUtcTicksV1!.Value - host.Clock.GetUtcNow().UtcTicks < TimeSpan.FromMinutes(5).Ticks);
        }
        Assert.Equal(ItemLifecycleStatus.PendingActivation, (await ItemAsync(id)).LifecycleStatus);
    }

    // S2 lane merge (minor): the SUBMIT reads the lists with the maker's token too — its 401 / 403 answers as the maker's
    // repeat does (503, the list cannot be read), never as MDM's configuration; one coded Information line; nothing starts.
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task S2Merge_A_refused_maker_token_at_the_submit_is_the_list_unavailable_not_a_configuration_error(int refusal)
    {
        var id = await CreatedAsync("Sodium citrate " + refusal);
        host.ItemReferences.RefuseStatus = refusal;

        var submit = await PostAsync(id, "submit", Lifecycle(0));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, submit.StatusCode);
        using var body = JsonDocument.Parse(await submit.Content.ReadAsStringAsync());
        var errors = body.RootElement.TryGetProperty("errors", out var lower) ? lower : body.RootElement.GetProperty("Errors");
        Assert.Equal(ItemReferenceEvidence.ProviderUnavailable, errors[0].GetString());
        Assert.Equal(ItemLifecycleStatus.Draft, (await ItemAsync(id)).LifecycleStatus);
        Assert.Contains(host.Logs.Entries, entry => entry.Level == LogLevel.Information
            && entry.Message.Contains("maker's token", StringComparison.Ordinal)
            && entry.Message.Contains(ItemReferenceEvidence.ProviderConfigurationError, StringComparison.Ordinal));
    }

    // S2 lane merge FIX1 (CT addendum 3): WITHDRAW_APPROVAL is offered on a pending approval to the person who requested it
    // and holds mdm.items.withdraw — never to another holder of the key, never to the requester without it (GSKU pattern).
    [Fact]
    public async Task S2MergeFix1_Withdraw_is_offered_to_its_requester_with_the_key_only()
    {
        var id = await PendingAsync("Sodium lauryl sulfate");
        async Task<string[]> ActionsAsync(HttpClient client)
        {
            using var detail = JsonDocument.Parse(await client.GetStringAsync($"{Items}/{id}"));
            var data = detail.RootElement.TryGetProperty("data", out var lower) ? lower : detail.RootElement.GetProperty("Data");
            return data.GetProperty("availableActions").EnumerateArray().Select(x => x.GetString()!).ToArray();
        }

        Assert.Contains("WITHDRAW_APPROVAL", await ActionsAsync(Client()));
        Assert.DoesNotContain("WITHDRAW_APPROVAL",
            await ActionsAsync(host.Client(host.Token(_tenant, _maker, All.Where(x => x != "mdm.items.withdraw").ToArray()))));
        Assert.DoesNotContain("WITHDRAW_APPROVAL", await ActionsAsync(host.Client(host.Token(_tenant, Guid.NewGuid(), All))));

        // The offered action works: the requester withdraws, the item is back in Draft and the action is gone.
        var withdrawn = await PostAsync(id, "withdraw", Lifecycle(1));
        Assert.True(withdrawn.IsSuccessStatusCode, await withdrawn.Content.ReadAsStringAsync());
        Assert.DoesNotContain("WITHDRAW_APPROVAL", await ActionsAsync(Client()));
    }

    // Item 3: the gauge is the OLDEST open approval's age, in seconds. (The fake engine stamps its start with the real
    // clock, so the old one's start is set back five days — every other open approval in this host is younger.)
    [Fact]
    public async Task Fix3_Item3_The_oldest_open_approvals_age_is_the_oldest_in_seconds()
    {
        var oldId = await PendingAsync("Benzalkonium chloride");
        var old = await OperationAsync(oldId);
        var fiveDaysAgo = host.Clock.GetUtcNow().AddDays(-5).UtcTicks;
        await RawOperations().UpdateOneAsync(x => x.Id == old.Id,
            Builders<ItemWorkflowOperation>.Update.Set(x => x.WorkflowStartedAtUtcTicksV1, fiveDaysAgo));
        await PendingAsync("Chlorhexidine");
        var metrics = host.Factory.Services.GetRequiredService<Diten.MdmService.Api.Services.ProductItemSkuMaster.ProductIdentityRecoveryMetrics>();

        await host.RunAsync<ItemActivationGauge>(gauge => gauge.RecordAsync(CancellationToken.None));

        var age = metrics.OldestOpenApproval(ItemActivationWorkflowRecoveryRunner.Flow);
        Assert.InRange(age!.Value, (long)TimeSpan.FromDays(5).TotalSeconds - 60, (long)TimeSpan.FromDays(5).TotalSeconds + 600);
    }

    // S2 lane merge E4: the oldest-open-approval gauge's index is on the store, by name and with its keys (checkpoint, then
    // the approval's start) — without it the gauge scans every operation every five minutes.
    [Fact]
    public async Task S2Merge_E4_The_open_age_index_is_built_with_its_keys()
    {
        await PendingAsync("Sodium stearyl fumarate");
        var collection = RawOperations();
        var indexes = await (await collection.Indexes.ListAsync()).ToListAsync();
        var index = Assert.Single(indexes, document => document["name"] == "ix_mdm_item_workflow_open_age");
        var expected = Builders<ItemWorkflowOperation>.IndexKeys.Ascending(x => x.Checkpoint)
            .Ascending(x => x.WorkflowStartedAtUtcTicksV1)
            .Render(collection.DocumentSerializer, collection.Settings.SerializerRegistry);
        Assert.Equal(expected, index["key"].AsBsonDocument);
    }

    // Minor: a configured retry delay above the ceiling is honoured; the configuration wait counts as stuck.
    [Fact]
    public void Fix3_Minor_A_long_retry_delay_is_honoured_and_a_configuration_wait_is_stuck()
    {
        Assert.Equal(TimeSpan.FromMinutes(20),
            ItemActivationWorkflowProcessor.DecisionPollDelay(new ItemWorkflowOperation(), DateTimeOffset.UtcNow, TimeSpan.FromMinutes(20)));
        Assert.True(ItemActivationWorkflowRecoveryRunner.IsStuck(new ItemWorkflowOperation
        {
            Checkpoint = ItemWorkflowCheckpoint.DecisionObserved,
            RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.Retryable,
            LastFailureCode = ItemReferenceEvidence.ProviderConfigurationError
        }));
    }

    // Minor: the fault streak decays — one quiet ask keeps it, the second in a row resets it.
    [Fact]
    public async Task Fix3_Minor_Two_quiet_asks_in_a_row_reset_the_fault_streak()
    {
        var id = await PendingAsync("Propylparaben");
        var before = await OperationAsync(id);
        await RawOperations().UpdateOneAsync(x => x.Id == before.Id, Builders<ItemWorkflowOperation>.Update
            .Set(x => x.FailedAttemptCount, 3).Set(x => x.LastFailureCode, "FAKE_EARLIER_FAULT"));

        using (host.Clock.Shift(TimeSpan.FromMinutes(1))) await CycleAsync();
        Assert.Equal(3, (await OperationAsync(id)).FailedAttemptCount);
        using (host.Clock.Shift(TimeSpan.FromHours(1))) await CycleAsync();
        Assert.Equal(0, (await OperationAsync(id)).FailedAttemptCount);
    }

    // Minor (the M3 rule in the store): the decision's replay after the item was deactivated is still recognised.
    [Fact]
    public async Task Fix3_Minor_A_replay_after_a_deactivation_is_still_this_decision()
    {
        var id = await PendingAsync("Methylparaben");
        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();
        Assert.True((await PostAsync(id, "deactivate", Lifecycle(2, "withdrawn from use"))).IsSuccessStatusCode);
        var item = await ItemAsync(id);
        var operation = await OperationAsync(id);
        var written = item.ActivationWorkflowBinding!.TerminalDecision!;
        var store = new ItemWorkflowOperationRepository(host.Database, new Tenant(_tenant));
        var claim = new ItemWorkflowClaim(_tenant, operation.OperationId, id, operation.OperationFingerprint, "store-test", 1,
            ItemWorkflowCheckpoint.ApprovalValidated, host.Clock.GetUtcNow().AddMinutes(1).UtcTicks);

        var replay = await store.ApplyDecisionAsync(claim, operation, written, host.Clock.GetUtcNow().UtcTicks);

        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
    }

    // S2 lane merge E2 — the same rule through the PROCESSOR: the decision is written, its answer lost, the item is then
    // deactivated; the next recovery cycle re-applies, the store answers "replay", the processor recognises the decision
    // by what it wrote (not by the status now) and completes — never a quarantine, never a second write.
    [Fact]
    public async Task S2Merge_E2_A_lost_decision_answer_then_a_deactivation_completes_on_the_next_cycle()
    {
        var id = await PendingAsync("Propyl gallate");
        host.AfterDecisionWrite = async (_, _) =>
        {
            host.AfterDecisionWrite = null;
            return new ItemWorkflowWriteResult(false, false, null, ProductIdentityStore.Unavailable);
        };

        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();
        Assert.Equal(ItemWorkflowCheckpoint.ApprovalValidated, (await OperationAsync(id)).Checkpoint);
        var deactivated = await PostAsync(id, "deactivate", Lifecycle(2, "withdrawn from use"));
        Assert.True(deactivated.IsSuccessStatusCode, await deactivated.Content.ReadAsStringAsync());
        using (host.Clock.Shift(TimeSpan.FromMinutes(1))) await CycleAsync();

        var item = await ItemAsync(id);
        var operation = await OperationAsync(id);
        Assert.Equal((ItemLifecycleStatus.Deactivated, 3), (item.LifecycleStatus, item.Version));
        Assert.Equal(ItemWorkflowCheckpoint.Completed, operation.Checkpoint);
    }

    // Minor (M3's other branch): a rejection, then the maker's new submit inside the audit window — the rejected
    // operation still completes (the item has moved on to a later approval).
    [Fact]
    public async Task Fix3_Minor_A_new_submit_right_after_a_rejection_still_completes_the_rejected_operation()
    {
        var id = await PendingAsync("Butylated hydroxytoluene");
        var rejected = await OperationAsync(id);
        host.AfterDecisionWrite = async (_, written) =>
        {
            host.AfterDecisionWrite = null;
            var resubmit = await PostAsync(id, "submit", Lifecycle(2));
            Assert.True(resubmit.IsSuccessStatusCode, await resubmit.Content.ReadAsStringAsync());
            return written;
        };

        host.Engine.Reject(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();

        var first = await RawOperations().Find(x => x.Id == rejected.Id).SingleAsync();
        Assert.Equal(ItemWorkflowCheckpoint.Completed, first.Checkpoint);
        Assert.Equal(ItemLifecycleStatus.PendingActivation, (await ItemAsync(id)).LifecycleStatus);
    }

    // Minor: the withdrawal skips a scheduled ask, never a lease held elsewhere.
    [Fact]
    public async Task Fix3_Minor_A_withdrawal_never_takes_a_lease_held_elsewhere()
    {
        var id = await PendingAsync("Sodium metabisulfite");
        var operation = await OperationAsync(id);
        var store = new ItemWorkflowOperationRepository(host.Database, new Tenant(_tenant));
        var now = host.Clock.GetUtcNow();
        Assert.NotNull(await store.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [operation.Checkpoint], "another-worker", now.UtcTicks, now.AddMinutes(2).UtcTicks)));

        var withdrawn = await PostAsync(id, "withdraw", Lifecycle(1));

        Assert.Equal(HttpStatusCode.Conflict, withdrawn.StatusCode);
        Assert.Equal("another-worker", (await OperationAsync(id)).LeaseOwner);
    }

    // Minor (survivor): the worker's service identity is checked at start on an instance that runs the worker.
    [Fact]
    public void Fix3_Minor_The_service_identity_configuration_is_checked_at_start()
    {
        var validators = host.Factory.Services.GetServices<IValidateOptions<
            Diten.MdmService.Infrastructure.ReferenceData.AuthWorkflowVerifiedMarketServiceIdentityProviderOptions>>().ToList();

        Assert.Contains(validators, validator => validator.Validate(Options.DefaultName,
            new Diten.MdmService.Infrastructure.ReferenceData.AuthWorkflowVerifiedMarketServiceIdentityProviderOptions()).Failed);
    }

    // ------------------------------------------------------------------------------------------ helpers

    private ProductIdentityWorkflowDecisionEvidence Decision(ItemWorkflowOperation operation, Guid actor) => new()
    {
        Decision = ProductIdentityDecisionKind.Approved,
        WorkflowInstanceId = operation.WorkflowInstanceId!.Value,
        ApprovalTaskId = operation.ApprovalTaskId!.Value,
        WorkflowTemplateId = operation.WorkflowTemplateId!.Value,
        WorkflowTemplateVersionId = operation.WorkflowTemplateVersionId!.Value,
        ObjectType = operation.ObjectType,
        ObjectId = operation.ItemId,
        ObjectRef = operation.ObjectRef,
        DecisionActorSubjectId = actor,
        DecisionAtUtc = host.Clock.GetUtcNow(),
        TransitionSequence = 9,
        TaskStatus = "Approved",
        InstanceStatus = "Completed"
    };

    private async Task<long> UnprocessedOfThisTenantAsync(TimeSpan after) =>
        await RawOperations().CountDocumentsAsync(
            ItemWorkflowTenantPartitionDiscoveryRepository.Unprocessed((host.Clock.GetUtcNow() - after).UtcTicks)
            & Builders<ItemWorkflowOperation>.Filter.Eq(x => x.TenantId, _tenant));

    private async Task EditNameAsync(Guid id, int version, string name)
    {
        var body = Fields(name);
        body["expectedVersion"] = version;
        var edit = await Client().PutAsJsonAsync($"{Items}/{id}", body);
        Assert.True(edit.IsSuccessStatusCode, await edit.Content.ReadAsStringAsync());
    }

    private ConcurrentQueue<string> CaptureTheProductionForwarder()
    {
        var wire = new ConcurrentQueue<string>();
        host.Audit.Inner = new PlatformAuditForwarder(new CapturingFactory(wire),
            Options.Create(new PlatformRegistrationOptions { BaseUrl = "http://platform.test", InternalApiKey = "test-only" }),
            host.Factory.Services.GetRequiredService<IHttpContextAccessor>(), NullLogger<PlatformAuditForwarder>.Instance);
        return wire;
    }

    private HttpClient Client() => host.Client(host.Token(_tenant, _maker, All));

    private Task CycleAsync() => host.RunAsync<ItemActivationWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());

    private Task<HttpResponseMessage> PostAsync(Guid id, string door, object body) =>
        Client().PostAsJsonAsync($"{Items}/{id}/{door}", body);

    private async Task<Guid> CreatedAsync(string name)
    {
        var body = Fields(name);
        body["idempotencyKey"] = "ITEM-" + Guid.NewGuid().ToString("N");
        var response = await Client().PostAsJsonAsync(Items, body);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await Envelope.ReadAsync(response)).GetProperty("data").GetGuid();
    }

    private async Task<Guid> PendingAsync(string name)
    {
        var id = await CreatedAsync(name);
        var submit = await PostAsync(id, "submit", Lifecycle(0));
        Assert.True(submit.StatusCode == HttpStatusCode.Accepted, await submit.Content.ReadAsStringAsync());
        return id;
    }

    private async Task<Guid> DeactivatedAsync(string name)
    {
        var id = await PendingAsync(name);
        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();
        var version = (await ItemAsync(id)).Version;
        var deactivated = await PostAsync(id, "deactivate", Lifecycle(version, "not used"));
        Assert.True(deactivated.StatusCode == HttpStatusCode.OK, await deactivated.Content.ReadAsStringAsync());
        return id;
    }

    private static Dictionary<string, object?> Fields(string name) => new()
    {
        ["name"] = name,
        ["description"] = null,
        ["materialType"] = "RAW_MATERIAL",
        ["baseUomCode"] = "KGM",
        ["uomConversions"] = new object[] { new { fromUom = "KGM", toUom = "GRM", numerator = "1000", denominator = "1" } },
        ["storageConditionCodes"] = new[] { "AMBIENT_15_25" },
        ["lotControlled"] = true,
        ["stockable"] = true,
        ["shelfLifeDays"] = 730,
        ["retestDays"] = null,
        ["minRemainingShelfLifeDays"] = null
    };

    private static object Lifecycle(int version, string? reason = null) => new { expectedVersion = version, reason };

    private IMongoCollection<BsonDocument> RawItems() => host.Database.GetCollection<BsonDocument>(ItemRepository.CollectionName);

    private IMongoCollection<ItemWorkflowOperation> RawOperations() =>
        host.Database.GetCollection<ItemWorkflowOperation>(ItemWorkflowOperationRepository.CollectionName);

    private static FilterDefinition<BsonDocument> Filter(Guid id) =>
        new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard));

    private Task<Item> ItemAsync(Guid id) =>
        host.Database.GetCollection<Item>(ItemRepository.CollectionName).Find(x => x.Id == id).SingleAsync();

    private Task<ItemWorkflowOperation> OperationAsync(Guid itemId) =>
        RawOperations().Find(x => x.TenantId == _tenant && x.ItemId == itemId).SortByDescending(x => x.CreatedAtUtcTicksV1)
            .FirstAsync();

    private IEnumerable<Diten.MdmService.Application.Contracts.Audit.AuditForwardRequest> Trail(Guid id, string type) =>
        host.Audit.Forwarded.Where(x => x.EntityId == id && x.RequestType == type);

    private static async Task<string?> FirstErrorAsync(HttpResponseMessage response) =>
        Envelope.FirstError(await Envelope.ReadAsync(response));

    private sealed class Tenant(Guid id) : ITenantContext
    {
        public Guid TenantId { get; private set; } = id;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }

    private sealed class Recording : ILogger<ItemActivationWorkerOffWarning>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class CapturingFactory(ConcurrentQueue<string> wire) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Capturing(wire));
    }

    private sealed class Capturing(ConcurrentQueue<string> wire) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            wire.Enqueue(await request.Content!.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
