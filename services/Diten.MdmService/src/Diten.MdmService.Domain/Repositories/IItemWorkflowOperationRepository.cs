using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Repositories;

// MOD-0290-FU04 S2: the item activation operation store (mdm_item_workflow_operations), tenant-bounded. Every write
// that also moves the item is ONE transaction with the operation's own move (the LSKU store's rule) — the item never
// shows a state its operation does not hold.
public interface IItemWorkflowOperationRepository
{
    Task<ItemWorkflowReserveResult> ReserveAsync(ItemWorkflowOperation operation, CancellationToken cancellationToken = default);

    Task<ItemWorkflowOperation?> GetByOperationIdAsync(Guid operationId, CancellationToken cancellationToken = default);

    Task<ItemWorkflowOperation?> GetByStartIdempotencyKeyAsync(string startIdempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ItemWorkflowClaim?> TryClaimAsync(ItemWorkflowClaimRequest request, CancellationToken cancellationToken = default);

    Task<ItemWorkflowRecoverablePage> DiscoverRecoverableAsync(long nowUtcTicks, int limit,
        ItemWorkflowRecoveryCursor? after = null, CancellationToken cancellationToken = default);

    Task<bool> AdvanceAsync(ItemWorkflowClaim claim, ItemWorkflowCheckpointMutation mutation,
        CancellationToken cancellationToken = default);

    // Gives back ONLY this claim's own lease (owner + generation); nothing else is written.
    Task<bool> ReleaseLeaseAsync(ItemWorkflowClaim claim, CancellationToken cancellationToken = default);

    // At WorkflowStarted (the claim held): the fenced item at its expected version goes PendingActivation, bound to this
    // approval, its fence released, its version + 1. The item only — the processor verifies it and then moves the
    // operation (the LSKU SubmitIdentityAsync rule); a repeat that finds the item already bound to it is a replay.
    Task<ItemWorkflowWriteResult> ApplyLocalPendingAsync(ItemWorkflowClaim claim, ItemWorkflowOperation operation,
        ProductIdentityWorkflowBinding binding, long updatedAtUtcTicks, CancellationToken cancellationToken = default);

    // At ApprovalValidated (approved) / DecisionObserved (rejected): the pending item of THIS approval takes the decision
    // — Active, or back to Draft (Deactivated for a reactivation) — with the evidence, version + 1. The item only; a
    // repeat that finds the same decision on it is a replay.
    Task<ItemWorkflowWriteResult> ApplyDecisionAsync(ItemWorkflowClaim claim, ItemWorkflowOperation operation,
        ProductIdentityWorkflowDecisionEvidence evidence, long updatedAtUtcTicks, CancellationToken cancellationToken = default);

    // WithdrawalObserved → WithdrawalApplied: the pending item goes back (Draft / Deactivated) with the cancellation evidence.
    Task<ItemWorkflowWriteResult> ApplyWithdrawalAsync(ItemWorkflowClaim claim, ItemWorkflowOperation operation,
        ProductIdentityWorkflowCancellationEvidence cancellationEvidence, long updatedAtUtcTicks,
        CancellationToken cancellationToken = default);

    // The engine is KNOWN to hold nothing for this submit: the item (never pending) gives its fence back and its version
    // moves (so the maker's resubmit is a new operation); the operation ends AbandonedBeforeWorkflowStart.
    Task<ItemWorkflowWriteResult> AbandonBeforeStartAsync(ItemWorkflowClaim claim, ItemWorkflowOperation operation,
        string reasonCode, long updatedAtUtcTicks, CancellationToken cancellationToken = default);

    // The engine decided and MDM does not apply it: the pending item of THIS approval goes back (Draft / Deactivated);
    // the operation ends DecisionNotApplicable with the reason.
    Task<ItemWorkflowWriteResult> ApplyDecisionNotApplicableAsync(ItemWorkflowClaim claim, ItemWorkflowOperation operation,
        string reasonCode, long updatedAtUtcTicks, CancellationToken cancellationToken = default);
}

// The two §8.4 (B) gauges, over all tenants (numbers only), and the tenants with due work.
public interface IItemWorkflowTenantPartitionDiscovery
{
    Task<ItemWorkflowTenantPartitionPage> DiscoverAsync(Guid? afterTenantId, long nowUtcTicks, int limit,
        CancellationToken cancellationToken = default);

    // Stuck = manual reconciliation, waiting for the maker's replay, or a wait at its retry limit.
    Task<long> CountManualReconciliationAsync(long nowUtcTicks, CancellationToken cancellationToken = default);

    // Of those, the ones the requester's repeat cannot settle — a person's (BL-530).
    Task<long> CountNeedingAPersonAsync(long nowUtcTicks, CancellationToken cancellationToken = default);

    // S2 FIX1 item 2: approvals the worker should be carrying (not ended, not parked for a person or the maker) that nobody
    // has touched since the cutoff — the flow is on and no worker is processing it.
    Task<long> CountUnprocessedAsync(long untouchedSinceUtcTicks, CancellationToken cancellationToken = default);
}
