using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GlobalProductRetirementRequestWorkflowProcessorTests
{
    [Fact]
    public async Task Invalid_interactive_facts_fail_before_repository_or_workflow_access()
    {
        var processor = new GlobalProductRetirementRequestWorkflowProcessor(null!, null!, null!, null!,
            TimeProvider.System);

        var result = await processor.StartInteractiveAsync(Guid.Empty, Guid.NewGuid(), 0, Guid.NewGuid(),
            Guid.NewGuid(), "Reason", "token", new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("GLOBAL_PRODUCT_RETIREMENT_REQUEST_INVALID", result.ErrorCode);
    }

    [Fact]
    public async Task Completed_replay_uses_persisted_snapshot_even_when_current_start_config_drifted()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var maker = Guid.NewGuid();
        var persisted = new GlobalProductRetirementRequestOperation
        {
            TenantId = tenantId, OperationId = operationId, GlobalProductId = productId,
            BaseProductVersion = 3, MakerSubjectId = maker, RequestReason = "Business reason",
            WorkflowTemplateId = Guid.NewGuid(), CandidatePrincipalIds = [Guid.NewGuid()],
            ReasonCode = "PERSISTED", CommentRequired = true, EvidenceRequired = true,
            ConfiguredDueAfterSeconds = 3600, DueAtUtcTicksV1 = DateTimeOffset.UtcNow.AddHours(1).UtcTicks,
            ObjectType = "GlobalProductRetirement", ObjectId = operationId.ToString("D"),
            ObjectRef = "GP-000000000001", StartIdempotencyKey = $"retirement:{operationId:D}",
            OperationFingerprint = new string('a', 64),
            Checkpoint = GlobalProductRetirementRequestCheckpoint.Completed
        };
        var driftedFactory = new GlobalProductRetirementRequestWorkflowStartRequestFactory(new(
            Guid.NewGuid(), null, [Guid.NewGuid()], "DRIFTED", false, false, null,
            Guid.NewGuid(), null, Guid.NewGuid(), null), TimeProvider.System);
        var processor = new GlobalProductRetirementRequestWorkflowProcessor(new ExistingOperation(persisted),
            null!, null!, driftedFactory, TimeProvider.System);

        var result = await processor.StartInteractiveAsync(tenantId, productId, 3, operationId, maker,
            "Business reason", "delegated-token", new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.IsReplay);
        Assert.Same(persisted, result.Operation);
    }

    [Fact]
    public async Task Manual_checkpoint_is_not_entered_when_required_manual_audit_cannot_be_persisted()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var binding = new GlobalProductActiveLifecycleOperationBinding(
            GlobalProductLifecycleOperationKind.Retirement, operationId, 0);
        var product = new GlobalProduct
        {
            Id = productId, TenantId = tenantId, CanonicalCode = "GP-000000000001",
            GlobalProductName = "Product", GlobalProductNameNormalized = "PRODUCT",
            LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved, Version = 1,
            ActiveLifecycleOperation = binding
        };
        var operation = new GlobalProductRetirementRequestOperation
        {
            TenantId = tenantId, OperationId = operationId, GlobalProductId = productId,
            BaseProductVersion = 0, MakerSubjectId = Guid.NewGuid(), RequestReason = "Business reason",
            OperationFingerprint = new string('a', 64),
            Checkpoint = GlobalProductRetirementRequestCheckpoint.DecisionObserved,
            DecisionKind = ProductIdentityDecisionKind.Approved,
            DecisionActorSubjectId = Guid.NewGuid(), DecisionTaskStatus = "Approved"
        };
        var operationRepository = new MutableOperation(operation);
        var products = new AuditFailureProducts(product);
        var factory = new GlobalProductRetirementRequestWorkflowStartRequestFactory(new(
            Guid.NewGuid(), null, [Guid.NewGuid()], "RETIRE", false, false, null,
            Guid.NewGuid(), null, Guid.NewGuid(), null), TimeProvider.System);
        var processor = new GlobalProductRetirementRequestWorkflowProcessor(operationRepository,
            products, null!, factory, TimeProvider.System);

        var result = await processor.RecoverAsync(operation, "worker",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(GlobalProductRetirementRequestCheckpoint.DecisionObserved, operation.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, operation.RecoveryDisposition);
        Assert.Equal("GLOBAL_PRODUCT_RETIREMENT_MANUAL_AUDIT_NOT_PERSISTED", operation.LastFailureCode);
        Assert.Equal(1, products.ManualAuditAttempts);
    }

    private sealed class ExistingOperation(GlobalProductRetirementRequestOperation operation)
        : IGlobalProductRetirementRequestOperationRepository
    {
        public Task<GlobalProductRetirementRequestOperation?> GetByOperationIdAsync(Guid operationId,
            CancellationToken cancellationToken = default) => Task.FromResult<GlobalProductRetirementRequestOperation?>(operation);
        public Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverTenantPartitionsAsync(Guid? afterTenantId,
            int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GlobalProductRetirementRequestReserveResult> ReserveAsync(GlobalProductRetirementRequestOperation value,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GlobalProductRetirementRequestClaim?> TryClaimAsync(Guid operationId, string fingerprint,
            IReadOnlyCollection<GlobalProductRetirementRequestCheckpoint> checkpoints, string leaseOwner,
            long nowUtcTicks, long leaseUntilUtcTicks, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> AdvanceAsync(GlobalProductRetirementRequestClaim claim,
            GlobalProductRetirementRequestMutation mutation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<GlobalProductRetirementRequestRecoverablePage> DiscoverRecoverableAsync(long nowUtcTicks,
            int limit, Guid? afterOperationId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class MutableOperation(GlobalProductRetirementRequestOperation operation)
        : IGlobalProductRetirementRequestOperationRepository
    {
        public Task<GlobalProductRetirementRequestOperation?> GetByOperationIdAsync(Guid operationId,
            CancellationToken cancellationToken = default) => Task.FromResult<GlobalProductRetirementRequestOperation?>(operation);
        public Task<GlobalProductRetirementRequestClaim?> TryClaimAsync(Guid operationId, string fingerprint,
            IReadOnlyCollection<GlobalProductRetirementRequestCheckpoint> checkpoints, string leaseOwner,
            long nowUtcTicks, long leaseUntilUtcTicks, CancellationToken cancellationToken = default) =>
            Task.FromResult<GlobalProductRetirementRequestClaim?>(new(operation.TenantId, operation.OperationId,
                operation.OperationFingerprint, leaseOwner, 1, operation.Checkpoint));
        public Task<bool> AdvanceAsync(GlobalProductRetirementRequestClaim claim,
            GlobalProductRetirementRequestMutation mutation, CancellationToken cancellationToken = default)
        {
            operation.Checkpoint = mutation.NextCheckpoint;
            operation.RecoveryDisposition = mutation.RecoveryDisposition;
            operation.NextAttemptAtUtcTicksV1 = mutation.NextAttemptAtUtcTicksV1;
            operation.LastFailureCode = mutation.LastFailureCode;
            return Task.FromResult(true);
        }
        public Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverTenantPartitionsAsync(Guid? afterTenantId,
            int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GlobalProductRetirementRequestReserveResult> ReserveAsync(GlobalProductRetirementRequestOperation value,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GlobalProductRetirementRequestRecoverablePage> DiscoverRecoverableAsync(long nowUtcTicks,
            int limit, Guid? afterOperationId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class AuditFailureProducts(GlobalProduct product) : IGlobalProductRepository
    {
        public int ManualAuditAttempts { get; private set; }
        public Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<GlobalProduct?>(product);
        public Task<GlobalProductLifecycleWriteResult> ApplyRetirementDecisionAsync(Guid id, int expectedVersion,
            GlobalProductActiveLifecycleOperationBinding binding, bool approved, LocalAuditIntent auditIntent,
            CancellationToken cancellationToken = default) => Task.FromResult(
                new GlobalProductLifecycleWriteResult(false, product, "DEPENDENT_IDENTITIES_EXIST"));
        public Task<GlobalProductLifecycleWriteResult> RecordRetirementConflictAsync(Guid id, int expectedVersion,
            GlobalProductActiveLifecycleOperationBinding binding, LocalAuditIntent auditIntent,
            CancellationToken cancellationToken = default)
        {
            ManualAuditAttempts++;
            return Task.FromResult(new GlobalProductLifecycleWriteResult(false, product,
                "GLOBAL_PRODUCT_CONCURRENCY_CONFLICT"));
        }
        public Task<GlobalProduct?> GetByReservationIdAsync(Guid reservationId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> NameExistsAsync(string normalizedName,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<GlobalProductPage> GetPageAsync(int pageNumber, int pageSize, string? normalizedSearch,
            ProductIdentityLifecycleStatus? lifecycleStatus, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<GlobalProductCreateResult> CreateDraftAsync(GlobalProduct globalProduct,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
