using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public interface IProductLegalEntityScopeRolloutStateRepository
{
    Task<ProductLegalEntityScopeRolloutState?> GetAsync(
        CancellationToken cancellationToken = default);

    Task<ProductLegalEntityScopeRolloutState?> GetByCreationCommandIdAsync(
        Guid creationCommandId,
        CancellationToken cancellationToken = default);

    Task<ProductLegalEntityScopeRolloutStateWriteResult> CreateAsync(
        ProductLegalEntityScopeRolloutState state,
        CancellationToken cancellationToken = default);

    Task<ProductLegalEntityScopeRolloutStateWriteResult> UpdateAsync(
        ProductLegalEntityScopeRolloutState state,
        int expectedVersion,
        CancellationToken cancellationToken = default);

    Task<ProductLegalEntityScopeWriterLeaseResult> AcquireWriterLeaseAsync(
        ProductLegalEntityScopeWriterLease requested,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProductLegalEntityScopeWriterLeaseResult(false, null, null, FailureCode: "PRODUCT_SCOPE_WRITE_ADMISSION_NOT_SUPPORTED"));

    Task<bool> BindWriterLeaseBaselineAsync(
        Guid token,
        long generation,
        string preWriteStateHash,
        CancellationToken cancellationToken = default) => Task.FromResult(false);

    Task<bool> ReleaseWriterLeaseAsync(
        Guid token,
        long generation,
        CancellationToken cancellationToken = default) => Task.FromResult(false);

    Task<ProductLegalEntityScopeFenceResult> AcquireFenceAsync(
        ProductLegalEntityScopeActivationFence requested,
        Guid expectedRolloutStateId,
        int expectedVersion,
        ProductLegalEntityScopeRolloutMode expectedMode,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProductLegalEntityScopeFenceResult(false, null, null, "PRODUCT_SCOPE_FENCE_NOT_SUPPORTED"));

    Task<bool> BindFenceSnapshotsAsync(
        string fenceToken,
        ProductLegalEntityScopeInventorySnapshot first,
        ProductLegalEntityScopeInventorySnapshot second,
        CancellationToken cancellationToken = default) => Task.FromResult(false);

    Task<ProductLegalEntityScopeRolloutStateWriteResult> CommitTransitionAsync(
        string fenceToken,
        ProductLegalEntityScopeRolloutState requested,
        int expectedVersion,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProductLegalEntityScopeRolloutStateWriteResult(false, null, VersionConflict: true));
}

public sealed record ProductLegalEntityScopeRolloutStateWriteResult(
    bool Succeeded,
    ProductLegalEntityScopeRolloutState? State,
    bool VersionConflict = false,
    bool WriteOutcomeAmbiguous = false);

public sealed record ProductLegalEntityScopeWriterLeaseResult(
    bool Acquired,
    ProductLegalEntityScopeWriterLease? Lease,
    ProductLegalEntityScopeRolloutState? Rollout,
    bool LegacyBypass = false,
    string? FailureCode = null);

public sealed record ProductLegalEntityScopeFenceResult(
    bool Acquired,
    ProductLegalEntityScopeActivationFence? Fence,
    ProductLegalEntityScopeRolloutState? Rollout,
    string? FailureCode = null);
