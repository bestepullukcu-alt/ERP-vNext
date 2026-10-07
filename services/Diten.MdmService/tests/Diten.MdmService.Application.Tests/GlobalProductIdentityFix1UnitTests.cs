using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Enums;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// BL-552 flow 3 FIX1 (P-2026-10-07-GP-FLOW3-FIX1) — the identity processor's rules measured on the in-memory harness:
/// a stuck submit's repeat with the transport unready (item 12), and the decision mapped before "its own maker" (item 8).
/// </summary>
public sealed partial class GlobalProductIdentityWorkflowProcessorTests
{
    // Item 12: a stuck submit's repeat reads the engine — not with the chain unready. The stored answer, no claim, no call.
    [Fact]
    public async Task A_stuck_submits_repeat_with_the_transport_unready_is_answered_from_the_store_without_a_call()
    {
        var harness = Harness.Create(transport: null, new FixedReadiness("PRODUCT_WORKFLOW_CLIENT_CONFIGURATION_INVALID"));
        var operation = harness.CreateOperation(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired);
        operation.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired;
        operation.LastFailureCode = GlobalProductIdentityWorkflowProcessor.StartProofInvalid;
        harness.Repository.Current = operation;

        var result = await harness.Processor.StartInteractiveAsync(TenantId, ProductId, 0, OperationId, MakerId,
            "maker-token", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("PRODUCT_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED", result.ErrorCode);
        Assert.Equal(0, harness.Repository.ClaimCalls);
        Assert.Equal(0, harness.Client.StartResultCalls);
        Assert.Equal(GlobalProductIdentityWorkflowProcessor.StartProofInvalid, harness.Repository.Current!.LastFailureCode);
    }

    // Item 8 (the GSKU E1 order): an action that is not a decision is evidence that does not fit — whoever the actor.
    // Its maker "withdrawing" through the decision evidence is not "its own maker decided": the same quarantine GSKU
    // gives it, the product left pending.
    [Fact]
    public async Task A_terminal_action_that_is_not_a_decision_is_not_its_own_makers_decision()
    {
        var harness = Harness.CreatePending();
        harness.Client.Terminal = (_, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(
                TerminalEvidence() with { TerminalAction = "Withdraw", ActorUserId = MakerId.ToString("D") }));

        await harness.Processor.RecoverAsync(harness.Repository.Current!, "worker-1", TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(30));

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired, harness.Repository.Current!.Checkpoint);
        Assert.Equal(GlobalProductIdentityWorkflowProcessor.EvidenceConflict, harness.Repository.Current.LastFailureCode);
        Assert.Equal(0, harness.Products.ReturnToDraftCount);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Product.LifecycleStatus);
    }

    // The same evidence with a real decision by its maker is still "its own maker decided" (back to draft).
    [Fact]
    public async Task A_decision_by_its_own_maker_still_returns_the_product_to_draft()
    {
        var harness = Harness.CreatePending();
        harness.Client.Terminal = (_, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(
                TerminalEvidence() with { ActorUserId = MakerId.ToString("D") }));

        await harness.Processor.RecoverAsync(harness.Repository.Current!, "worker-1", TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(30));

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Completed, harness.Repository.Current!.Checkpoint);
        Assert.Equal(GlobalProductIdentityWorkflowProcessor.SelfApprovalIgnored, harness.Repository.Current.LastFailureCode);
        Assert.Equal(1, harness.Products.ReturnToDraftCount);
    }
}
