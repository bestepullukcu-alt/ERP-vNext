using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Repositories;

public interface ILskuIdentityWorkflowOperationRepository
{
    Task<LskuIdentityWorkflowReserveResult> ReserveAsync(
        LskuIdentityWorkflowOperation operation,
        CancellationToken cancellationToken = default);

    Task<LskuIdentityWorkflowOperation?> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<LskuIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(
        string startIdempotencyKey,
        CancellationToken cancellationToken = default);

    Task<LskuIdentityWorkflowClaim?> TryClaimAsync(
        LskuIdentityWorkflowClaimRequest request,
        CancellationToken cancellationToken = default);

    Task<LskuIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks,
        int limit,
        LskuIdentityWorkflowRecoveryCursor? after = null,
        CancellationToken cancellationToken = default);

    Task<bool> AdvanceAsync(
        LskuIdentityWorkflowClaim claim,
        LskuIdentityWorkflowCheckpointMutation mutation,
        CancellationToken cancellationToken = default);

    Task<LskuIdentityWithdrawalWriteResult> ApplyWithdrawalAsync(
        LskuIdentityWorkflowClaim claim,
        LskuIdentityWorkflowOperation operation,
        ProductIdentityWorkflowCancellationEvidence cancellationEvidence,
        LocalAuditIntent auditIntent,
        long updatedAtUtcTicks,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new LskuIdentityWithdrawalWriteResult(
            false, false, null, "LSKU_IDENTITY_WITHDRAWAL_NOT_IMPLEMENTED"));
}
