using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;
using Obs = Diten.MdmService.Api.Tests.NoEligibleApproverObservations;

namespace Diten.MdmService.Api.Tests;

// WP-MDM-NO-ELIGIBLE-APPROVER-01 — the LSKU's two flows (identity: waits with its maker; retirement request: lock given
// back). See NoEligibleApproverGpGskuHttpTests for the rule.
public sealed partial class LskuFix1HttpTests
{
    private void NeapRefuseTheNextStart() =>
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Invalid, "WORKFLOW_NO_ELIGIBLE_APPROVER", false);

    [Fact]
    public async Task NEAP_LSKU_identity_submit_waits_with_its_maker_and_says_no_eligible_approver()
    {
        var gsku = await ApprovedGskuAsync();
        var lsku = await CreatedLskuAsync(gsku, "TR");
        var version = (await LskuAsync(lsku)).Version;
        NeapRefuseTheNextStart();

        var answer = await Obs.ReadAsync(await SubmitAsync(lsku, version));

        var op = Assert.Single(await host.Database.GetCollection<LskuIdentityWorkflowOperation>(
                LskuIdentityWorkflowOperationRepository.CollectionName)
            .Find(x => x.TenantId == _tenant && x.LskuId == lsku).ToListAsync());
        var stored = await LskuAsync(lsku);
        Obs.Record("LSKU kimlik", answer.Status, answer.Errors, op.Checkpoint, op.RecoveryDisposition, op.LastFailureCode,
            op.NextAttemptAtUtcTicksV1, $"LSKU {stored.LifecycleStatus} v{stored.Version} (önce v{version})");
        Assert.Equal(409, answer.Status);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.NoEligibleApprover, answer.Errors.FirstOrDefault());
        Assert.Equal(LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay, op.Checkpoint);
        Assert.NotEqual(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, op.RecoveryDisposition);
        Assert.Null(op.NextAttemptAtUtcTicksV1);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, stored.LifecycleStatus);
    }

    [Fact]
    public async Task NEAP_LSKU_retirement_request_gives_the_lock_back_and_says_no_eligible_approver()
    {
        var approved = await ApprovedLskuAsync("DE");
        NeapRefuseTheNextStart();

        var answer = await Obs.ReadAsync(await RequestRetirementAsync(approved.Id, approved.Version));

        var op = Assert.Single(await host.Database.GetCollection<LskuRetirementRequestOperation>(
                LskuRetirementRequestOperationRepository.CollectionName)
            .Find(x => x.TenantId == _tenant && x.LskuId == approved.Id).ToListAsync());
        var stored = await LskuAsync(approved.Id);
        Obs.Record("LSKU emeklilik", answer.Status, answer.Errors, op.Checkpoint, op.RecoveryDisposition, op.LastFailureCode,
            op.NextAttemptAtUtcTicksV1, $"LSKU {stored.LifecycleStatus} lock={(stored.ActiveLifecycleOperation is null ? "free" : "held")}");
        Assert.Equal(409, answer.Status);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.NoEligibleApprover, answer.Errors.FirstOrDefault());
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.None, op.RecoveryDisposition);
        Assert.Null(op.NextAttemptAtUtcTicksV1);
        Assert.Null(stored.ActiveLifecycleOperation);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, stored.LifecycleStatus);
    }
}

// WP-MDM-NO-ELIGIBLE-APPROVER-01 — the item activation (an identity flow: waits with its maker).
public sealed partial class ItemS2HttpTests
{
    [Fact]
    public async Task NEAP_Item_activation_submit_waits_with_its_maker_and_says_no_eligible_approver()
    {
        var id = await CreatedAsync("No eligible approver item " + Guid.NewGuid().ToString("N")[..8]);
        host.Engine.NextStartFailure = (ProductIdentityWorkflowTransportOutcome.Invalid, "WORKFLOW_NO_ELIGIBLE_APPROVER", false);

        var answer = await Obs.ReadAsync(await PostAsync(id, "submit", Lifecycle(0)));

        var op = await OperationAsync(id);
        var item = await ItemAsync(id);
        Obs.Record("Kalem etkinleştirme", answer.Status, answer.Errors, op.Checkpoint, op.RecoveryDisposition,
            op.LastFailureCode, op.NextAttemptAtUtcTicksV1, $"Item {item.LifecycleStatus} v{item.Version} (önce v0)");
        Assert.Equal(409, answer.Status);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.NoEligibleApprover, answer.Errors.FirstOrDefault());
        Assert.Equal(ItemWorkflowCheckpoint.AwaitingMakerReplay, op.Checkpoint);
        Assert.NotEqual(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, op.RecoveryDisposition);
        Assert.Null(op.NextAttemptAtUtcTicksV1);
        Assert.Equal(ItemLifecycleStatus.Draft, item.LifecycleStatus);
    }
}
