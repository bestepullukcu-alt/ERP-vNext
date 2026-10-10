using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ItemMaster.Activation;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Persistence.Repositories;
using Xunit;

namespace Diten.MdmService.Application.Tests.ItemMaster;

/// <summary>
/// MOD-0290-FU04 S2 — the pure parts: the activation gate (R-04, O-25, the chain rule), the shared binding part the GP /
/// GSKU / item flows read the engine with, the item store's checkpoint table (the accepted LSKU table, value for value)
/// and the start request (the QA Manager position as the only candidate, configuration-borne).
/// </summary>
public sealed class ItemActivationUnitTests
{
    // ------------------------------------------------------------------------------------------ the gate

    [Fact]
    public void Gate_passes_a_complete_item()
    {
        Assert.Empty(ItemActivationGate.MissingFields(Complete()));
    }

    [Fact]
    public void Gate_names_an_empty_lot_flag()
    {
        var item = Complete();
        item.LotControlled = null;
        Assert.Equal([ItemActivationGate.LotControlledField], ItemActivationGate.MissingFields(item));
    }

    [Fact]
    public void Gate_names_a_missing_base_unit()
    {
        var item = Complete();
        item.BaseUomCode = "";
        item.UomConversions = [];
        Assert.Equal([ItemActivationGate.BaseUomField], ItemActivationGate.MissingFields(item));
    }

    [Theory]
    [InlineData(ItemMaterialType.RawMaterial, true)]
    [InlineData(ItemMaterialType.ActiveIngredient, true)]
    [InlineData(ItemMaterialType.Excipient, true)]
    [InlineData(ItemMaterialType.Intermediate, true)]
    [InlineData(ItemMaterialType.PackagingPrimary, false)]
    [InlineData(ItemMaterialType.PackagingSecondary, false)]
    public void Gate_needs_a_storage_condition_except_for_packaging(ItemMaterialType type, bool required)
    {
        var item = Complete();
        item.MaterialType = type;
        item.StorageConditionCodes = [];
        Assert.Equal(required, ItemActivationGate.MissingFields(item).Contains(ItemActivationGate.StorageConditionsField));
    }

    [Fact]
    public void Gate_runs_the_chain_rule_on_the_conversions()
    {
        var item = Complete();
        item.UomConversions = [Conv("KGM", "GRM"), Conv("GRM", "MGM"), Conv("MGM", "MC"), Conv("MC", "LBR"), Conv("LBR", "ONZ")];
        Assert.Equal([ItemActivationGate.UomConversionsField], ItemActivationGate.MissingFields(item));
        item.UomConversions = [Conv("KGM", "GRM"), Conv("GRM", "MGM"), Conv("MGM", "MC"), Conv("MC", "LBR")];
        Assert.Empty(ItemActivationGate.MissingFields(item));
        item.UomConversions = [new() { FromUom = "KGM", ToUom = "GRM", Numerator = "0", Denominator = "1" }];
        Assert.Equal([ItemActivationGate.UomConversionsField], ItemActivationGate.MissingFields(item));
    }

    // ------------------------------------------------------------------------------------------ the evidence rule (IU)

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    public void Evidence_held_whatever_the_catalog_version(int version)
    {
        var item = Complete();
        item.StorageConditionKinds = new() { ["AMBIENT_15_25"] = "TEMPERATURE" };
        var requests = ItemReferenceEvidence.Requests(item);
        var answer = Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceResolveResult.Success(
            requests.Select(r => new Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceSelection(
                r.SetCode, r.ValueCode, Guid.NewGuid(), version, "LATEST", DateTimeOffset.UtcNow, false, true,
                r.SetCode == "storage-condition"
                    ? new Dictionary<string, string?> { ["ConditionKind"] = "TEMPERATURE" }
                    : new Dictionary<string, string?>())).ToList());
        Assert.Equal(ItemReferenceEvidence.Verdict.Held, ItemReferenceEvidence.Judge(item, requests, answer).Verdict);
    }

    [Fact]
    public void Evidence_removed_changed_outage_and_contract_are_each_named()
    {
        var item = Complete();
        item.StorageConditionKinds = new() { ["AMBIENT_15_25"] = "TEMPERATURE" };
        var requests = ItemReferenceEvidence.Requests(item);
        var removed = ItemReferenceEvidence.Judge(item, requests,
            Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceResolveResult.Missing(
                [new("storage-condition", "AMBIENT_15_25")]));
        Assert.Equal((ItemReferenceEvidence.Verdict.NotApplicable, ItemReferenceEvidence.ValueRemoved), (removed.Verdict, removed.Code));
        var changed = ItemReferenceEvidence.Judge(item, requests,
            Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceResolveResult.Success(requests.Select(r =>
                new Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceSelection(r.SetCode, r.ValueCode,
                    Guid.NewGuid(), 2, "LATEST", DateTimeOffset.UtcNow, false, true,
                    new Dictionary<string, string?> { ["ConditionKind"] = "PROTECTION" })).ToList()));
        Assert.Equal((ItemReferenceEvidence.Verdict.NotApplicable, ItemReferenceEvidence.ValueChanged), (changed.Verdict, changed.Code));
        Assert.Equal(ItemReferenceEvidence.Verdict.Wait, ItemReferenceEvidence.Judge(item, requests,
            Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceResolveResult.Fail(503, "DOWN")).Verdict);
        Assert.Equal(ItemReferenceEvidence.Verdict.Stuck, ItemReferenceEvidence.Judge(item, requests,
            Diten.MdmService.Application.Contracts.ReferenceData.ItemReferenceResolveResult.Success([])).Verdict);
    }

    // ------------------------------------------------------------------------------------------ the shared binding part

    [Theory]
    [InlineData("Approve", true, ProductIdentityDecisionKind.Approved)]
    [InlineData("Reject", true, ProductIdentityDecisionKind.Rejected)]
    [InlineData("approve", false, default(ProductIdentityDecisionKind))]
    [InlineData("Cancel", false, default(ProductIdentityDecisionKind))]
    public void Shared_binding_maps_only_the_two_terminal_actions(string action, bool mapped, ProductIdentityDecisionKind expected)
    {
        Assert.Equal(mapped, IdentityApprovalBinding.TryMapDecision(action, out var decision));
        Assert.Equal(expected, decision);
    }

    [Theory]
    [InlineData(ProductIdentityDecisionKind.Approved, "Approved", "Completed", true)]
    [InlineData(ProductIdentityDecisionKind.Approved, "Approved", "Rejected", false)]
    [InlineData(ProductIdentityDecisionKind.Rejected, "Rejected", "Rejected", true)]
    [InlineData(ProductIdentityDecisionKind.Rejected, "Rejected", "Completed", false)]
    public void Shared_binding_holds_each_decision_to_its_statuses(ProductIdentityDecisionKind decision, string task,
        string instance, bool exact)
    {
        Assert.Equal(exact, IdentityApprovalBinding.ExactTerminalStatuses(decision, task, instance));
    }

    [Fact]
    public void Shared_binding_says_nothing_was_processed_only_for_a_readable_refusal()
    {
        Assert.True(IdentityApprovalBinding.RefusedBeforeProcessing(ProductIdentityWorkflowTransportOutcome.Invalid, "X"));
        Assert.True(IdentityApprovalBinding.RefusedBeforeProcessing(ProductIdentityWorkflowTransportOutcome.Forbidden, null));
        Assert.False(IdentityApprovalBinding.RefusedBeforeProcessing(ProductIdentityWorkflowTransportOutcome.Invalid,
            ProductIdentityWorkflowFaultPolicy.ResponseUnreadable));
        Assert.False(IdentityApprovalBinding.RefusedBeforeProcessing(ProductIdentityWorkflowTransportOutcome.Invalid,
            ProductIdentityWorkflowFaultPolicy.ResponseInvalid));
        Assert.False(IdentityApprovalBinding.RefusedBeforeProcessing(ProductIdentityWorkflowTransportOutcome.Timeout, null));
    }

    // ------------------------------------------------------------------------------------------ the store's table

    // The accepted LSKU table, value for value — with ONE named difference: LSKU resumes a stuck approval straight to
    // DecisionObserved for its market's REFERENCE_CONTRACT_MISMATCH; the item has no such proof — its stuck decision is put
    // back to AwaitingDecision (the engine is read again, the gate and the evidence are checked again).
    private static readonly (ItemWorkflowCheckpoint From, ItemWorkflowCheckpoint To)[] NotInTheItemTable =
        [(ItemWorkflowCheckpoint.ManualReconciliationRequired, ItemWorkflowCheckpoint.DecisionObserved)];

    [Fact]
    public void The_item_checkpoint_table_is_the_accepted_LSKU_table_but_for_one_named_move()
    {
        var values = Enum.GetValues<ItemWorkflowCheckpoint>();
        Assert.Equal(Enum.GetValues<LskuIdentityWorkflowCheckpoint>().Select(x => (int)x), values.Select(x => (int)x));
        foreach (var from in values)
        foreach (var to in values)
        {
            var lsku = LskuIdentityWorkflowOperationRepository.IsAllowedTransition((LskuIdentityWorkflowCheckpoint)(int)from,
                (LskuIdentityWorkflowCheckpoint)(int)to);
            var item = ItemWorkflowOperationRepository.IsAllowedTransition(from, to);
            if (NotInTheItemTable.Contains((from, to)))
                Assert.True(lsku && !item, $"{from} -> {to} is the named difference");
            else
                Assert.True(lsku == item, $"{from} -> {to} differs from the LSKU table");
        }
    }

    // ------------------------------------------------------------------------------------------ the start

    [Fact]
    public void The_start_names_the_QA_Manager_position_as_its_only_candidate()
    {
        var position = Guid.NewGuid();
        var factory = new ItemActivationWorkflowStartRequestFactory(
            new(null, "ITEM-ACTIVATION", position, "ITEM_ACTIVATION_APPROVAL", false, false, null), TimeProvider.System);
        var item = Complete();

        var plan = factory.Create(item.TenantId, item, ItemActivationKind.Activation, Guid.NewGuid(), 0, Guid.NewGuid());

        Assert.Equal(["position:" + position.ToString("D")], plan.TransportRequest.CandidatePrincipalIds);
        Assert.Equal("Item", plan.TransportRequest.ObjectType);
        Assert.Equal(position, plan.Operation.ApproverPositionId);
        Assert.Equal(0, plan.Operation.LastStartSentAtUtcTicksV1);
    }

    [Fact]
    public void A_configuration_without_an_approver_position_is_invalid()
    {
        Assert.False(ItemActivationWorkflowStartRequestFactory.IsValid(
            new(null, "ITEM-ACTIVATION", Guid.Empty, "ITEM_ACTIVATION_APPROVAL", false, false, null)));
        Assert.False(ItemActivationWorkflowStartRequestFactory.IsValid(
            new(null, null, Guid.NewGuid(), "ITEM_ACTIVATION_APPROVAL", false, false, null)));
        Assert.True(ItemActivationWorkflowStartRequestFactory.IsValid(
            new(null, "ITEM-ACTIVATION", Guid.NewGuid(), "ITEM_ACTIVATION_APPROVAL", false, false, null)));
    }

    [Fact]
    public void One_operation_per_item_version_and_kind()
    {
        var tenant = Guid.NewGuid();
        var item = Guid.NewGuid();
        Assert.Equal(ItemActivationWorkflowStartRequestFactory.OperationIdFor(tenant, item, 3, ItemActivationKind.Activation),
            ItemActivationWorkflowStartRequestFactory.OperationIdFor(tenant, item, 3, ItemActivationKind.Activation));
        Assert.NotEqual(ItemActivationWorkflowStartRequestFactory.OperationIdFor(tenant, item, 3, ItemActivationKind.Activation),
            ItemActivationWorkflowStartRequestFactory.OperationIdFor(tenant, item, 4, ItemActivationKind.Activation));
        Assert.NotEqual(ItemActivationWorkflowStartRequestFactory.OperationIdFor(tenant, item, 3, ItemActivationKind.Activation),
            ItemActivationWorkflowStartRequestFactory.OperationIdFor(tenant, item, 3, ItemActivationKind.Reactivation));
    }

    private static Item Complete() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        CanonicalCode = "IT-000000000042",
        Name = "Lactose",
        MaterialType = ItemMaterialType.Excipient,
        BaseUomCode = "KGM",
        UomConversions = [Conv("KGM", "GRM")],
        StorageConditionCodes = ["AMBIENT_15_25"],
        LotControlled = true,
        LifecycleStatus = ItemLifecycleStatus.Draft
    };

    private static ItemUomConversion Conv(string from, string to) =>
        new() { FromUom = from, ToUom = to, Numerator = "1000", Denominator = "1" };
}
