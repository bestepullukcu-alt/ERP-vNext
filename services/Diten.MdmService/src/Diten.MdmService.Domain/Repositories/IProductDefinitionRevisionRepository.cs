using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Repositories;

public interface IProductDefinitionRevisionRepository
{
    Task<ProductDefinitionRevision?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    async Task<IReadOnlyList<ProductDefinitionRevision>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ProductDefinitionRevision>();
        foreach (var id in ids)
        {
            var item = await GetByIdAsync(id, cancellationToken);
            if (item is not null)
            {
                results.Add(item);
            }
        }

        return results;
    }
    Task<ProductDefinitionRevision?> GetByCreationCommandIdAsync(
        string creationCommandId,
        CancellationToken cancellationToken = default);
    Task<FirstGskuPairAllocationResult> AllocateForFirstGskuAsync(
        Guid globalProductId,
        string creationCommandId,
        CancellationToken cancellationToken = default);
    Task<ProductDefinitionRevisionCreateResult> CreateForFirstGskuAsync(
        ProductDefinitionRevision revision,
        CancellationToken cancellationToken = default);
    Task<FirstGskuIdentityLifecycleMutationResult<ProductDefinitionRevision>> MarkIdentityPendingAsync(
        Guid id, int expectedVersion, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FIRST_GSKU_IDENTITY_LIFECYCLE_NOT_IMPLEMENTED");
    Task<FirstGskuIdentityLifecycleMutationResult<ProductDefinitionRevision>> ApproveIdentityAsync(
        Guid id, int expectedVersion, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FIRST_GSKU_IDENTITY_LIFECYCLE_NOT_IMPLEMENTED");
    Task<FirstGskuIdentityLifecycleMutationResult<ProductDefinitionRevision>> RestoreDraftAfterRejectionAsync(
        Guid id, int expectedVersion, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FIRST_GSKU_IDENTITY_LIFECYCLE_NOT_IMPLEMENTED");
    Task<FirstGskuIdentityRetirementWriteResult<ProductDefinitionRevision>> RetireIdentityAsync(
        Guid id, int expectedVersion, Guid operationId, string operationFingerprint,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FIRST_GSKU_RETIREMENT_NOT_IMPLEMENTED");
}

public sealed record ProductDefinitionRevisionCreateResult(
    bool Succeeded,
    ProductDefinitionRevision? Revision,
    string? ErrorCode = null);

public sealed record FirstGskuPairAllocationResult(
    Guid RevisionId,
    Guid GskuId,
    int RevisionOrdinal,
    string RevisionIdentifier);
