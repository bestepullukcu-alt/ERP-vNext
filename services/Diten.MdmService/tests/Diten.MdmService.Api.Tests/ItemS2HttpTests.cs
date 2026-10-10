using System.Net;
using System.Net.Http.Json;
using Diten.MdmService.Api.Services.ItemMaster;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ItemMaster;
using Diten.MdmService.Application.Features.ItemMaster.Activation;
using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Application.Features.ItemMaster.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU04 S2 (P-2026-10-09-FU04-S2) — the item activation approval over HTTP, the real store on a throwaway
/// replica-set mongod, the fake engine: the engine's three answers, the settle window, the write classes, the two gauges,
/// the store invariant, maker ≠ approver, a stuck approval holding the item, a reactivation's rejection back to
/// Deactivated, the activation gate (R-04), path b for every new write, the field-level item write and the orphan IT- repair.
/// </summary>
[Collection(ItemActivationHttpCollection.Name)]
public sealed class ItemS2HttpTests(ItemActivationHttpHost host) : IAsyncLifetime
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
        host.Engine.LookupFailure = null;
        host.Engine.NextStartFails = null;
        host.Engine.NextStartFailure = null;
        host.Engine.NextLookupFailure = null;
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ------------------------------------------------------------------------------------------ the happy path

    [Fact]
    public async Task S2_Activation_goes_to_the_QA_Manager_position_and_the_approval_makes_the_item_Active()
    {
        var id = await CreatedAsync("Lactose monohydrate");

        var submit = await PostAsync(id, "submit", Lifecycle(0));

        Assert.Equal(HttpStatusCode.Accepted, submit.StatusCode);
        var start = host.Engine.LastStartRequest!;
        Assert.Equal("Item", start.ObjectType);
        Assert.Equal("ITEM-ACTIVATION", start.TemplateCode);
        Assert.Equal(new[] { "position:" + ItemActivationHttpHost.QaManagerPosition.ToString("D") }, start.CandidatePrincipalIds);
        var pending = await ItemAsync(id);
        Assert.Equal((ItemLifecycleStatus.PendingActivation, 1), (pending.LifecycleStatus, pending.Version));
        Assert.Equal(ItemActivationKind.Activation, pending.ActivationKind);
        Assert.Null(pending.ActivationSubmitOperationId);
        Assert.Equal(_maker, pending.ActivationWorkflowBinding!.SubmitterSubjectId);

        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();

        var active = await ItemAsync(id);
        Assert.Equal((ItemLifecycleStatus.Active, 2), (active.LifecycleStatus, active.Version));
        Assert.Equal(_approver, active.ActivationWorkflowBinding!.TerminalDecision!.DecisionActorSubjectId);
        Assert.Equal(ItemWorkflowCheckpoint.Completed, (await OperationAsync(id)).Checkpoint);
    }

    // ------------------------------------------------------------------------------------------ maker ≠ approver

    [Fact]
    public async Task S2_The_maker_approving_their_own_item_is_not_applied_and_the_item_is_a_draft_again()
    {
        var id = await PendingAsync("Magnesium stearate");

        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _maker);
        await CycleAsync();

        var item = await ItemAsync(id);
        Assert.Equal(ItemLifecycleStatus.Draft, item.LifecycleStatus);
        Assert.Null(item.ActivationWorkflowBinding!.TerminalDecision);
        var operation = await OperationAsync(id);
        Assert.Equal(ItemWorkflowCheckpoint.DecisionNotApplicable, operation.Checkpoint);
        Assert.Equal(ItemActivationWorkflowProcessor.SelfApprovalIgnored, operation.LastFailureCode);
    }

    // ------------------------------------------------------------------------------------------ the engine's three answers

    [Fact]
    public async Task S2_Engine_open_means_wait_and_the_item_stays_pending()
    {
        var id = await PendingAsync("Talc");

        await CycleAsync();

        Assert.Equal(ItemLifecycleStatus.PendingActivation, (await ItemAsync(id)).LifecycleStatus);
        var operation = await OperationAsync(id);
        Assert.Equal(ItemWorkflowCheckpoint.AwaitingDecision, operation.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, operation.RecoveryDisposition);
    }

    [Fact]
    public async Task S2_Engine_closed_without_a_decision_returns_the_item_to_draft()
    {
        var id = await PendingAsync("Povidone");

        host.Engine.CloseWithoutDecision(host.Engine.Open("Item", id.ToString("D")));
        await CycleAsync();

        Assert.Equal(ItemLifecycleStatus.Draft, (await ItemAsync(id)).LifecycleStatus);
        var operation = await OperationAsync(id);
        Assert.Equal(ItemWorkflowCheckpoint.DecisionNotApplicable, operation.Checkpoint);
        Assert.Equal(ItemActivationWorkflowProcessor.ClosedWithoutDecision, operation.LastFailureCode);
    }

    [Fact]
    public async Task S2_Engine_contradicting_evidence_is_stuck_and_the_stuck_approval_holds_the_item()
    {
        var discovery = new ItemWorkflowTenantPartitionDiscoveryRepository(host.Database);
        var now = host.Clock.GetUtcNow().UtcTicks;
        var manualBefore = await discovery.CountManualReconciliationAsync(now);
        var personBefore = await discovery.CountNeedingAPersonAsync(now);
        var id = await PendingAsync("Croscarmellose");

        host.Engine.Open("Item", id.ToString("D")).EvidenceInconsistent = true;
        await CycleAsync();

        var operation = await OperationAsync(id);
        Assert.Equal(ItemWorkflowCheckpoint.ManualReconciliationRequired, operation.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, operation.RecoveryDisposition);
        // Held: the item stays pending, its edit and its withdrawal-free state are refused — nothing is released.
        var held = await ItemAsync(id);
        Assert.Equal((ItemLifecycleStatus.PendingActivation, 1), (held.LifecycleStatus, held.Version));
        Assert.Equal(ItemErrors.TransitionInvalid, await FirstErrorAsync(await PutAsync(id, UpdateBody("Croscarmellose", 1))));
        // Both gauges count it (its cause is not one the maker's repeat settles).
        now = host.Clock.GetUtcNow().UtcTicks;
        Assert.Equal(manualBefore + 1, await discovery.CountManualReconciliationAsync(now));
        Assert.Equal(personBefore + 1, await discovery.CountNeedingAPersonAsync(now));
        // The worker never touches it again.
        await CycleAsync();
        Assert.Equal(ItemWorkflowCheckpoint.ManualReconciliationRequired, (await OperationAsync(id)).Checkpoint);
        Assert.Equal(ItemLifecycleStatus.PendingActivation, (await ItemAsync(id)).LifecycleStatus);
    }

    // ------------------------------------------------------------------------------------------ reactivation

    [Fact]
    public async Task S2_A_rejected_reactivation_returns_the_item_to_Deactivated_never_to_Draft()
    {
        var id = await DeactivatedAsync("Microcrystalline cellulose");
        var version = (await ItemAsync(id)).Version;

        var request = await PostAsync(id, "reactivation-requests", Lifecycle(version, "back in use"));
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        var pending = await ItemAsync(id);
        Assert.Equal((ItemLifecycleStatus.PendingActivation, ItemActivationKind.Reactivation),
            (pending.LifecycleStatus, pending.ActivationKind));

        host.Engine.Reject(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();

        var after = await ItemAsync(id);
        Assert.Equal(ItemLifecycleStatus.Deactivated, after.LifecycleStatus);
        Assert.Equal(ProductIdentityDecisionKind.Rejected, after.ActivationWorkflowBinding!.TerminalDecision!.Decision);
    }

    [Fact]
    public async Task S2_A_rejected_first_activation_returns_the_item_to_Draft()
    {
        var id = await PendingAsync("Sodium starch glycolate");

        host.Engine.Reject(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();

        Assert.Equal(ItemLifecycleStatus.Draft, (await ItemAsync(id)).LifecycleStatus);
    }

    [Fact]
    public async Task S2_An_approved_reactivation_makes_the_item_Active_again()
    {
        var id = await DeactivatedAsync("Colloidal silica");
        var version = (await ItemAsync(id)).Version;
        await PostAsync(id, "reactivation-requests", Lifecycle(version, "needed again"));

        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();

        Assert.Equal(ItemLifecycleStatus.Active, (await ItemAsync(id)).LifecycleStatus);
    }

    // ------------------------------------------------------------------------------------------ withdraw

    [Fact]
    public async Task S2_Withdraw_takes_an_activation_back_to_Draft_and_a_reactivation_back_to_Deactivated()
    {
        var draft = await PendingAsync("Hydroxypropyl cellulose");
        var withdrawn = await PostAsync(draft, "withdraw", Lifecycle(1, "wrong lot rule"));
        Assert.True(withdrawn.StatusCode == HttpStatusCode.OK, await withdrawn.Content.ReadAsStringAsync());
        Assert.Equal(ItemLifecycleStatus.Draft, (await ItemAsync(draft)).LifecycleStatus);
        Assert.True(host.Engine.Instances("Item").Single(x => x.ObjectId == draft.ToString("D")).Cancelled);

        var deactivated = await DeactivatedAsync("Ethylcellulose");
        var version = (await ItemAsync(deactivated)).Version;
        await PostAsync(deactivated, "reactivation-requests", Lifecycle(version, "again"));
        var back = await PostAsync(deactivated, "withdraw", Lifecycle(version + 1));
        Assert.True(back.StatusCode == HttpStatusCode.OK, await back.Content.ReadAsStringAsync());
        Assert.Equal(ItemLifecycleStatus.Deactivated, (await ItemAsync(deactivated)).LifecycleStatus);
    }

    [Fact]
    public async Task S2_Only_the_maker_withdraws()
    {
        var id = await PendingAsync("Shellac");
        var other = host.Client(host.Token(_tenant, Guid.NewGuid(), All));

        var refused = await other.PostAsJsonAsync($"{Items}/{id}/withdraw", Lifecycle(1));

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("ITEM_ACTIVATION_WITHDRAWAL_NOT_REQUESTER", await FirstErrorAsync(refused));
        Assert.Equal(ItemLifecycleStatus.PendingActivation, (await ItemAsync(id)).LifecycleStatus);
    }

    // ------------------------------------------------------------------------------------------ the activation gate (R-04)

    [Fact]
    public async Task S2_An_item_with_an_empty_lot_flag_is_refused_at_submit_with_the_field_named()
    {
        var id = await CreatedAsync("Gelatin");
        await Raw().UpdateOneAsync(Filter(id), new BsonDocument("$unset", new BsonDocument("LotControlled", "")));

        var submit = await PostAsync(id, "submit", Lifecycle(0));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, submit.StatusCode);
        Assert.Equal(new[] { ItemActivationGate.StockFieldsRequired, ItemActivationGate.LotControlledField }, await ErrorsAsync(submit));
        Assert.Equal((ItemLifecycleStatus.Draft, 0), Status(await ItemAsync(id)));
        Assert.Empty(host.Engine.Instances("Item").Where(x => x.ObjectId == id.ToString("D")));
    }

    [Fact]
    public async Task S2_A_non_packaging_item_without_a_storage_condition_is_refused_at_submit()
    {
        var id = await CreatedAsync("Starch", stockable: false, storage: []);

        var submit = await PostAsync(id, "submit", Lifecycle(0));

        Assert.Equal(new[] { ItemActivationGate.StockFieldsRequired, ItemActivationGate.StorageConditionsField },
            await ErrorsAsync(submit));
    }

    [Fact]
    public async Task S2_A_packaging_item_needs_no_storage_condition()
    {
        var id = await CreatedAsync("Blister foil", materialType: "PACKAGING_PRIMARY", storage: [], shelfLife: null,
            stockable: false);

        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync(id, "submit", Lifecycle(0))).StatusCode);
    }

    [Fact]
    public async Task S2_The_gate_is_run_again_when_the_approval_is_applied_and_an_unmet_gate_is_not_applied()
    {
        var id = await PendingAsync("Mannitol");
        await Raw().UpdateOneAsync(Filter(id), new BsonDocument("$unset", new BsonDocument("LotControlled", "")));

        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();

        Assert.Equal(ItemLifecycleStatus.Draft, (await ItemAsync(id)).LifecycleStatus);
        var operation = await OperationAsync(id);
        Assert.Equal(ItemWorkflowCheckpoint.DecisionNotApplicable, operation.Checkpoint);
        Assert.Equal(ItemActivationGate.StockFieldsRequired, operation.LastFailureCode);
    }

    [Fact]
    public async Task S2_A_conversion_chain_longer_than_four_steps_is_refused_at_submit()
    {
        var id = await CreatedAsync("Sucrose");
        // Five edges from KGM: KGM-GRM-MGM-MC-LBR-ONZ (written past the item's pair-level check, as a legacy row could be).
        var chain = new BsonArray(new[] { ("KGM", "GRM"), ("GRM", "MGM"), ("MGM", "MC"), ("MC", "LBR"), ("LBR", "ONZ") }
            .Select(x => new BsonDocument { { "FromUom", x.Item1 }, { "ToUom", x.Item2 }, { "Numerator", "10" }, { "Denominator", "1" } }));
        await Raw().UpdateOneAsync(Filter(id), new BsonDocument("$set", new BsonDocument("UomConversions", chain)));

        var submit = await PostAsync(id, "submit", Lifecycle(0));

        Assert.Equal(new[] { ItemActivationGate.StockFieldsRequired, ItemActivationGate.UomConversionsField },
            await ErrorsAsync(submit));
    }

    // ------------------------------------------------------------------------------------------ "S2 kanıt karşılaştırma kuralı"

    // CT (IU review, 6626fbf47): the item lists move to ITEM-UNIVERSAL-V2 (item-uom version 1 → 2). An item whose evidence is
    // V1 is submitted, Platform moves to V2 while the approval is open, the approver approves → accepted: the frozen
    // evidence is compared (the code is still in its list, the same kind), never the fresh version number.
    [Fact]
    public async Task S2_A_V1_item_approved_after_the_lists_moved_to_V2_is_activated()
    {
        var id = await PendingAsync("Anhydrous lactose");
        Assert.Equal(1, (await ItemAsync(id)).BaseUomSelection!.CatalogVersionNumber);

        host.ItemReferences.CatalogVersion = 2;
        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();

        var active = await ItemAsync(id);
        Assert.Equal(ItemLifecycleStatus.Active, active.LifecycleStatus);
        Assert.Equal(1, active.BaseUomSelection!.CatalogVersionNumber); // the stored V1 evidence stands, untouched
    }

    [Fact]
    public async Task S2_A_code_really_removed_from_its_list_is_a_named_refusal_and_the_item_goes_back()
    {
        var id = await PendingAsync("Purified talc");

        host.ItemReferences.Removed.Add("AMBIENT_15_25");
        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();

        Assert.Equal(ItemLifecycleStatus.Draft, (await ItemAsync(id)).LifecycleStatus);
        var operation = await OperationAsync(id);
        Assert.Equal(ItemWorkflowCheckpoint.DecisionNotApplicable, operation.Checkpoint);
        Assert.Equal(ItemReferenceEvidence.ValueRemoved, operation.LastFailureCode);
    }

    [Fact]
    public async Task S2_A_storage_condition_whose_kind_changed_is_a_named_refusal()
    {
        var id = await PendingAsync("Sorbitol");

        host.ItemReferences.KindOverride["AMBIENT_15_25"] = "PROTECTION";
        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();

        var operation = await OperationAsync(id);
        Assert.Equal(ItemWorkflowCheckpoint.DecisionNotApplicable, operation.Checkpoint);
        Assert.Equal(ItemReferenceEvidence.ValueChanged, operation.LastFailureCode);
    }

    [Fact]
    public async Task S2_A_list_outage_at_the_decision_waits_and_keeps_the_approvers_decision()
    {
        var id = await PendingAsync("Xylitol");

        host.ItemReferences.Outage = true;
        host.Engine.Approve(host.Engine.Open("Item", id.ToString("D")), _approver);
        await CycleAsync();

        var waiting = await OperationAsync(id);
        Assert.Equal(ItemWorkflowCheckpoint.DecisionObserved, waiting.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, waiting.RecoveryDisposition);
        Assert.Equal(ItemLifecycleStatus.PendingActivation, (await ItemAsync(id)).LifecycleStatus);

        host.ItemReferences.Outage = false;
        using (host.Clock.Shift(TimeSpan.FromSeconds(5))) await CycleAsync();
        Assert.Equal(ItemLifecycleStatus.Active, (await ItemAsync(id)).LifecycleStatus);
    }

    // ------------------------------------------------------------------------------------------ (A): no orphan approval

    [Fact]
    public async Task S2_A_start_refused_before_processing_ends_the_submit_and_frees_the_draft_for_a_new_submit()
    {
        var id = await CreatedAsync("Citric acid");
        host.Engine.NextStartFails = ProductIdentityWorkflowTransportOutcome.Invalid;

        var refused = await PostAsync(id, "submit", Lifecycle(0));

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(ItemActivationRequests.SubmitAbandoned, await FirstErrorAsync(refused));
        var item = await ItemAsync(id);
        Assert.Equal((ItemLifecycleStatus.Draft, 1), Status(item));
        Assert.Null(item.ActivationSubmitOperationId);
        Assert.Equal(HttpStatusCode.Accepted, (await PostAsync(id, "submit", Lifecycle(1))).StatusCode);
    }

    [Fact]
    public async Task S2_A_lost_start_answer_is_looked_up_and_the_engines_approval_is_never_orphaned()
    {
        var id = await CreatedAsync("Sodium chloride");
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "FAKE_ENGINE_TIMEOUT", true);

        var submit = await PostAsync(id, "submit", Lifecycle(0));

        Assert.Equal(HttpStatusCode.Accepted, submit.StatusCode);
        Assert.Equal(ItemLifecycleStatus.PendingActivation, (await ItemAsync(id)).LifecycleStatus);
        Assert.Single(host.Engine.Instances("Item").Where(x => x.ObjectId == id.ToString("D")));
    }

    [Fact]
    public async Task S2_Nothing_under_the_key_is_trusted_only_after_the_settle_window()
    {
        var id = await CreatedAsync("Potassium sorbate");
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "FAKE_ENGINE_TIMEOUT", false);
        host.Engine.NextLookupFailure = (ProductIdentityWorkflowTransportOutcome.Conflict, "FAKE_LOOKUP_CONFLICT");

        var stuck = await PostAsync(id, "submit", Lifecycle(0));
        Assert.Equal(HttpStatusCode.Conflict, stuck.StatusCode);
        Assert.Equal(ItemWorkflowCheckpoint.ManualReconciliationRequired, (await OperationAsync(id)).Checkpoint);
        // Inside the window the maker's repeat finds nothing and still does not trust it: the request stays stuck, held.
        var inside = await PostAsync(id, "submit", Lifecycle(0));
        Assert.Equal(HttpStatusCode.Conflict, inside.StatusCode);
        Assert.Equal(ItemWorkflowCheckpoint.ManualReconciliationRequired, (await OperationAsync(id)).Checkpoint);
        Assert.NotNull((await ItemAsync(id)).ActivationSubmitOperationId);

        using (host.Clock.Shift(TimeSpan.FromSeconds(5)))
        {
            var after = await PostAsync(id, "submit", Lifecycle(0));
            Assert.Equal(HttpStatusCode.Conflict, after.StatusCode);
            Assert.Equal(ItemActivationRequests.SubmitAbandoned, await FirstErrorAsync(after));
        }
        var operation = await OperationAsync(id);
        Assert.Equal(ItemWorkflowCheckpoint.AbandonedBeforeWorkflowStart, operation.Checkpoint);
        Assert.Equal(ItemActivationWorkflowProcessor.ReconciledNothingStarted, operation.LastFailureCode);
        var item = await ItemAsync(id);
        Assert.Equal((ItemLifecycleStatus.Draft, 1), Status(item));
        Assert.Null(item.ActivationSubmitOperationId);
    }

    [Fact]
    public async Task S2_A_submit_in_flight_holds_the_draft_against_edits()
    {
        var id = await CreatedAsync("Benzyl alcohol");
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Timeout, "FAKE_ENGINE_TIMEOUT", false);
        host.Engine.NextLookupFailure = (ProductIdentityWorkflowTransportOutcome.Conflict, "FAKE_LOOKUP_CONFLICT");
        await PostAsync(id, "submit", Lifecycle(0));

        var edit = await PutAsync(id, UpdateBody("Benzyl alcohol", 0, description: "changed"));

        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal(ItemRepository.ActivationSubmitInFlight, await FirstErrorAsync(edit));
    }

    // ------------------------------------------------------------------------------------------ (B): write classes, invariant

    [Fact]
    public async Task S2_A_concurrent_write_is_retried_and_counted_and_another_refusal_is_stuck_at_once()
    {
        var moved = await PendingAsync("Polysorbate 80");
        await Raw().UpdateOneAsync(Filter(moved), new BsonDocument("$inc", new BsonDocument("Version", 1)));
        host.Engine.Reject(host.Engine.Open("Item", moved.ToString("D")), _approver);
        var gone = await PendingAsync("Polysorbate 20");
        await Raw().UpdateOneAsync(Filter(gone), new BsonDocument("$set", new BsonDocument("IsDeleted", true)));
        host.Engine.Reject(host.Engine.Open("Item", gone.ToString("D")), _approver);

        await CycleAsync();

        var counted = await OperationAsync(moved);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, counted.RecoveryDisposition);
        Assert.Equal("ITEM_CONCURRENCY_CONFLICT", counted.LastFailureCode);
        Assert.Equal(1, counted.FailedAttemptCount);
        var stuck = await OperationAsync(gone);
        Assert.Equal(ItemWorkflowCheckpoint.ManualReconciliationRequired, stuck.Checkpoint);
        Assert.Equal(ItemErrors.NotFound, stuck.LastFailureCode);
    }

    [Fact]
    public async Task S2_The_store_refuses_manual_reconciliation_that_is_retryable()
    {
        var id = await PendingAsync("Glyceryl behenate");
        var operations = new ItemWorkflowOperationRepository(host.Database, new Tenant(_tenant));
        var operation = await OperationAsync(id);
        var now = host.Clock.GetUtcNow();
        var claim = await operations.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [operation.Checkpoint], "invariant-test", now.UtcTicks, now.AddSeconds(30).UtcTicks));

        var written = await operations.AdvanceAsync(claim!, new(ItemWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks, now.AddSeconds(5).UtcTicks, "X",
            ReleaseLease: true));

        Assert.False(written);
        var after = await OperationAsync(id);
        Assert.Equal(ItemWorkflowCheckpoint.AwaitingDecision, after.Checkpoint);
        Assert.Null(after.LeaseOwner);
    }

    // ------------------------------------------------------------------------------------------ path b (§21)

    [Fact]
    public async Task S2_Every_new_item_write_is_forwarded_on_path_b_with_its_tenant_and_the_decisions_evidence()
    {
        var approved = await PendingAsync("Stearic acid");
        host.Engine.Approve(host.Engine.Open("Item", approved.ToString("D")), _approver);
        await CycleAsync();
        var withdrawn = await PendingAsync("Oleic acid");
        await PostAsync(withdrawn, "withdraw", Lifecycle(1));

        var submit = Assert.Single(Trail(approved, nameof(SubmitItemCommand)));
        Assert.Equal(1, submit.Outcome);
        var decision = Assert.Single(Trail(approved, nameof(ApplyItemActivationDecisionCommand)));
        Assert.Equal(1, decision.Outcome);
        Assert.Equal(_tenant, decision.TenantId);
        Assert.Equal("Approved", decision.Metadata![ItemAudit.Decision]);
        Assert.Equal(_approver.ToString("D"), decision.Metadata[ItemAudit.DecisionActorId]);
        Assert.Equal("Active", decision.AfterState![nameof(Item.LifecycleStatus)]);
        Assert.Single(Trail(withdrawn, nameof(WithdrawItemApprovalCommand)), x => x.Outcome == 1);
        Assert.All(host.Audit.Forwarded.Where(x => x.EntityId == approved || x.EntityId == withdrawn), x =>
        {
            Assert.Equal(ItemAudit.EntityType, x.EntityType);
            Assert.All(x.Metadata?.Keys ?? [], key => Assert.Contains(key, ItemAudit.MetadataKeys));
        });
    }

    // ------------------------------------------------------------------------------------------ carry-over b and c

    [Fact]
    public async Task S2_An_item_edit_is_field_level_and_keeps_a_field_a_later_binary_wrote()
    {
        var id = await CreatedAsync("Calcium carbonate");
        await Raw().UpdateOneAsync(Filter(id), new BsonDocument("$set", new BsonDocument("FieldOfALaterBinary", "kept")));

        var edit = await PutAsync(id, UpdateBody("Calcium carbonate", 0, description: "edited"));

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var raw = await Raw().Find(Filter(id)).SingleAsync();
        Assert.Equal("kept", raw["FieldOfALaterBinary"].AsString);
        Assert.Equal("edited", raw["Description"].AsString);
        Assert.Equal(1, raw["Version"].AsInt32);
    }

    [Fact]
    public async Task S2_An_orphan_IT_code_is_counted_and_the_operator_repair_burns_it()
    {
        var census = new ItemOrphanCodeCensus(host.Database);
        var codes = new CodeReservationRepository(host.Database, new Tenant(_tenant));
        var command = ItemRules.ReservationCommandPrefix + Guid.NewGuid().ToString("N").ToUpperInvariant();
        var reserved = await codes.ReserveAsync(CodeBearingEntityType.Item, command, "t", "t");
        var consumed = await codes.ConsumeForIdentityAsync(reserved.Id, CodeBearingEntityType.Item, Guid.NewGuid(),
            reserved.Version, command, "t", "t");
        Assert.True(consumed.Succeeded, consumed.ErrorCode);
        var before = await census.CountOrphanItemCodesAsync(host.Clock.GetUtcNow() - ItemOrphanCodes.MinimumAge);

        using (host.Clock.Shift(TimeSpan.FromMinutes(20)))
        {
            Assert.Equal(before + 1, await census.CountOrphanItemCodesAsync(host.Clock.GetUtcNow() - ItemOrphanCodes.MinimumAge));
            var repair = await Client().PostAsync($"{Items}/operator/orphan-code-repairs?limit=10&minimumAgeMinutes=15", null);
            Assert.True(repair.StatusCode == HttpStatusCode.OK, await repair.Content.ReadAsStringAsync());
            Assert.Equal(before, await census.CountOrphanItemCodesAsync(host.Clock.GetUtcNow() - ItemOrphanCodes.MinimumAge));
        }
        Assert.Equal(CodeReservationBindingState.Burned,
            (await host.Database.GetCollection<CodeReservation>("mdm_code_reservations").Find(x => x.Id == reserved.Id)
                .SingleAsync()).BindingState);
    }

    // ------------------------------------------------------------------------------------------ helpers

    private HttpClient Client() => host.Client(host.Token(_tenant, _maker, All));

    private Task CycleAsync() => host.RunAsync<ItemActivationWorkflowRecoveryRunner>(runner => runner.RunCycleAsync());

    private Task<HttpResponseMessage> PostAsync(Guid id, string door, object body) =>
        Client().PostAsJsonAsync($"{Items}/{id}/{door}", body);

    private Task<HttpResponseMessage> PutAsync(Guid id, object body) => Client().PutAsJsonAsync($"{Items}/{id}", body);

    private async Task<Guid> CreatedAsync(string name, string materialType = "RAW_MATERIAL", int? shelfLife = 730,
        string[]? storage = null, bool stockable = true)
    {
        var body = Fields(name, materialType, shelfLife, storage, stockable, null);
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

    private static Dictionary<string, object?> Fields(string name, string materialType, int? shelfLife, string[]? storage,
        bool stockable, string? description) => new()
    {
        ["name"] = name,
        ["description"] = description,
        ["materialType"] = materialType,
        ["baseUomCode"] = "KGM",
        ["uomConversions"] = new object[] { new { fromUom = "KGM", toUom = "GRM", numerator = "1000", denominator = "1" } },
        ["storageConditionCodes"] = storage ?? ["AMBIENT_15_25"],
        ["lotControlled"] = true,
        ["stockable"] = stockable,
        ["shelfLifeDays"] = shelfLife,
        ["retestDays"] = null,
        ["minRemainingShelfLifeDays"] = null
    };

    private static Dictionary<string, object?> UpdateBody(string name, int version, string? description = null)
    {
        var body = Fields(name, "RAW_MATERIAL", 730, null, true, description);
        body["expectedVersion"] = version;
        return body;
    }

    private static object Lifecycle(int version, string? reason = null) => new { expectedVersion = version, reason };

    private IMongoCollection<BsonDocument> Raw() => host.Database.GetCollection<BsonDocument>(ItemRepository.CollectionName);

    private static FilterDefinition<BsonDocument> Filter(Guid id) =>
        new BsonDocument("_id", new BsonBinaryData(id, GuidRepresentation.Standard));

    private Task<Item> ItemAsync(Guid id) =>
        host.Database.GetCollection<Item>(ItemRepository.CollectionName).Find(x => x.Id == id).SingleAsync();

    private Task<ItemWorkflowOperation> OperationAsync(Guid itemId) =>
        host.Database.GetCollection<ItemWorkflowOperation>(ItemWorkflowOperationRepository.CollectionName)
            .Find(x => x.TenantId == _tenant && x.ItemId == itemId).SortByDescending(x => x.CreatedAtUtcTicksV1).FirstAsync();

    private static (ItemLifecycleStatus, int) Status(Item item) => (item.LifecycleStatus, item.Version);

    private IEnumerable<Diten.MdmService.Application.Contracts.Audit.AuditForwardRequest> Trail(Guid id, string type) =>
        host.Audit.Forwarded.Where(x => x.EntityId == id && x.RequestType == type);

    private static async Task<string?> FirstErrorAsync(HttpResponseMessage response) =>
        Envelope.FirstError(await Envelope.ReadAsync(response));

    private static async Task<string[]> ErrorsAsync(HttpResponseMessage response) =>
        (await Envelope.ReadAsync(response)).GetProperty("errors").EnumerateArray().Select(x => x.GetString()!).ToArray();

    private sealed class Tenant(Guid id) : ITenantContext
    {
        public Guid TenantId { get; private set; } = id;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}

/// <summary>
/// MOD-0290-FU04 S2: the MDM HTTP host with the item approval switched on (the base host keeps it off — S1's answer,
/// 503 ITEM_ACTIVATION_APPROVAL_UNAVAILABLE, stays measured there). Its own throwaway mongod.
/// </summary>
public sealed class ItemActivationHttpHost : MdmHttpHost
{
    public static readonly Guid QaManagerPosition = Guid.Parse("5a5a5a5a-0000-4000-8000-00000000a0a0");

    protected override IReadOnlyDictionary<string, string?> AdditionalSettings { get; } = new Dictionary<string, string?>
    {
        ["ItemActivationWorkflow__Enabled"] = "true",
        ["ItemActivationWorkflow__TemplateCode"] = "ITEM-ACTIVATION",
        ["ItemActivationWorkflow__ReasonCode"] = "ITEM_ACTIVATION_APPROVAL",
        ["ItemActivationWorkflow__ApproverPositionId"] = QaManagerPosition.ToString("D"),
        ["ItemActivationWorkflowWorker__Enabled"] = "true",
        ["ItemActivationWorkflowWorker__LeaseOwner"] = "mdm-http-test-item",
        ["ItemActivationWorkflowWorker__PollIntervalSeconds"] = "3600",
        ["ItemActivationWorkflowWorker__LeaseSeconds"] = "10",
        ["ItemActivationWorkflowWorker__RetryDelaySeconds"] = "1",
        ["ItemActivationWorkflowWorker__StartSettleSeconds"] = "2"
    };
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ItemActivationHttpCollection : ICollectionFixture<ItemActivationHttpHost>
{
    public const string Name = "MDM HTTP host — item activation";
}
