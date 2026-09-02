namespace Diten.MdmService.Domain.Repositories;

/// <summary>
/// Provides tenant-bound discovery and one-transaction recovery for product identity workflow operations.
/// Implementations must enforce tenant isolation, soft-delete exclusion, exact compare-and-set predicates,
/// immutable replay identity and atomic aggregate/audit/admission-fence updates.
/// </summary>
public interface IProductIdentityWorkflowOperationRecoveryRepository
{
    Task<ProductIdentityWorkflowOperationRecoveryCandidate?> GetCandidateAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<ProductIdentityWorkflowOperationRecoveryWriteResult> RecoverAsync(
        ProductIdentityWorkflowOperationRecoveryMutation mutation,
        CancellationToken cancellationToken = default);
}
