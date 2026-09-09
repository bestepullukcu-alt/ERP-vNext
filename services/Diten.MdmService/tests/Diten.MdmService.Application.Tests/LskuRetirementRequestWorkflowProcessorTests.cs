using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LskuRetirementRequestWorkflowProcessorTests
{
    [Fact]
    public async Task Invalid_interactive_request_fails_before_dependency_access()
    {
        var processor = new LskuRetirementRequestWorkflowProcessor(null!, null!, null!, null!, TimeProvider.System);
        var result = await processor.StartInteractiveAsync(Guid.Empty, Guid.NewGuid(), 1, Guid.NewGuid(),
            Guid.NewGuid(), "Reason", "token", new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)), default);
        Assert.False(result.Succeeded);
        Assert.Equal(400, result.StatusCode);
    }

    [Theory]
    [InlineData(true, ProductIdentityLifecycleStatus.Retired)]
    [InlineData(false, ProductIdentityLifecycleStatus.IdentityApproved)]
    public async Task Terminal_decision_is_applied_once_and_replay_is_stable(bool approved,
        ProductIdentityLifecycleStatus expectedStatus)
    {
        var tenant = Guid.NewGuid(); var id = Guid.NewGuid(); var operationId = Guid.NewGuid();
        var maker = Guid.NewGuid(); var approver = Guid.NewGuid(); var workflowId = Guid.NewGuid();
        var taskId = Guid.NewGuid(); var template = Guid.NewGuid(); var templateVersion = Guid.NewGuid();
        var operation = new LskuRetirementRequestOperation
        {
            TenantId = tenant, OperationId = operationId, LskuId = id, BaseLskuVersion = 4,
            MakerSubjectId = maker, RequestReason = "Obsolete identity", OperationFingerprint = new string('a', 64),
            ObjectType = "LskuRetirementRequest", ObjectId = operationId.ToString("D"), ObjectRef = "LS-1",
            WorkflowInstanceId = workflowId, ApprovalTaskId = taskId, WorkflowTemplateId = template,
            WorkflowTemplateVersionId = templateVersion, Checkpoint = LskuRetirementRequestCheckpoint.AwaitingDecision
        };
        var operations = new MutableOperations(operation);
        var lsku = new Lsku { Id = id, TenantId = tenant, CanonicalCode = "LS-1", GskuId = Guid.NewGuid(),
            MarketCode = "TR", LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved, Version = 5,
            ActiveLifecycleOperation = new(LskuLifecycleOperationKind.Retirement, operationId, 4) };
        var lskus = new DecisionLskus(lsku);
        var evidence = new ProductIdentityWorkflowTerminalEvidence(workflowId, taskId, template, templateVersion,
            operation.ObjectType, operation.ObjectId, operation.ObjectRef, approved ? "Approve" : "Reject",
            approver.ToString("D"), "DECISION", new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero), 3,
            approved ? "Approved" : "Rejected", approved ? "Completed" : "Rejected", null);
        var processor = new LskuRetirementRequestWorkflowProcessor(operations, lskus, new TerminalWorkflow(evidence),
            new(new(template, null, [approver], "RETIRE", false, false, null), TimeProvider.System),
            TimeProvider.System);

        var result = await processor.RecoverAsync(operation, "worker",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);
        var replay = await processor.RecoverAsync(operation, "worker",
            new(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)), default);

        Assert.True(result.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(LskuRetirementRequestCheckpoint.Completed, operation.Checkpoint);
        Assert.Equal(expectedStatus, lsku.LifecycleStatus);
        Assert.Equal(1, lskus.ApplyCount);
        Assert.Null(lsku.ActiveLifecycleOperation);
    }

    private sealed class MutableOperations(LskuRetirementRequestOperation operation)
        : ILskuRetirementRequestOperationRepository
    {
        public Task<LskuRetirementRequestOperation?> GetByOperationIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<LskuRetirementRequestOperation?>(operation);
        public Task<LskuRetirementRequestClaim?> TryClaimAsync(Guid id, string fingerprint,
            IReadOnlyCollection<LskuRetirementRequestCheckpoint> checkpoints, string owner, long now, long until,
            CancellationToken ct = default) => Task.FromResult<LskuRetirementRequestClaim?>(
                new(operation.TenantId, operation.OperationId, fingerprint, owner, ++operation.LeaseGeneration,
                    operation.Checkpoint, until));
        public Task<bool> AdvanceAsync(LskuRetirementRequestClaim claim, LskuRetirementRequestMutation mutation,
            CancellationToken ct = default)
        {
            operation.Checkpoint = mutation.NextCheckpoint; operation.RecoveryDisposition = mutation.RecoveryDisposition;
            operation.DecisionKind = mutation.DecisionKind ?? operation.DecisionKind;
            operation.DecisionActorSubjectId = mutation.DecisionActorSubjectId ?? operation.DecisionActorSubjectId;
            operation.DecisionReasonCode = mutation.DecisionReasonCode ?? operation.DecisionReasonCode;
            operation.DecisionAtUtcTicksV1 = mutation.DecisionAtUtcTicksV1 ?? operation.DecisionAtUtcTicksV1;
            operation.DecisionTransitionSequence = mutation.DecisionTransitionSequence ?? operation.DecisionTransitionSequence;
            operation.DecisionTaskStatus = mutation.DecisionTaskStatus ?? operation.DecisionTaskStatus;
            operation.DecisionInstanceStatus = mutation.DecisionInstanceStatus ?? operation.DecisionInstanceStatus;
            return Task.FromResult(true);
        }
        public Task<LskuRetirementRequestReserveResult> ReserveAsync(LskuRetirementRequestOperation value,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverTenantPartitionsAsync(Guid? after,
            int limit, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<LskuRetirementRequestPage> DiscoverRecoverableAsync(long now, int limit, Guid? after = null,
            CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class DecisionLskus(Lsku lsku) : ILskuRepository
    {
        public int ApplyCount { get; private set; }
        public Task<Lsku?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Lsku?>(lsku);
        public Task<LskuLifecycleWriteResult> ApplyRetirementDecisionAsync(Guid id, int expectedVersion,
            LskuActiveLifecycleOperationBinding binding, bool approved, LocalAuditIntent audit,
            CancellationToken ct = default)
        {
            ApplyCount++; lsku.LifecycleStatus = approved ? ProductIdentityLifecycleStatus.Retired
                : ProductIdentityLifecycleStatus.IdentityApproved;
            lsku.ActiveLifecycleOperation = null; lsku.Version++;
            lsku.AuditIntents.Add(audit);
            return Task.FromResult(new LskuLifecycleWriteResult(true, lsku));
        }
        public Task<Lsku?> GetByCreationCommandIdAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Lsku?> GetByReservationIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Lsku?> GetByIdentityKeyAsync(Guid id, string market, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<LskuCreateResult> CreateDraftAsync(Lsku value, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class TerminalWorkflow(ProductIdentityWorkflowTerminalEvidence evidence) : IProductIdentityWorkflowClient
    {
        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>> GetTerminalEvidenceAsync(
            Guid tenantId, ProductIdentityWorkflowTerminalEvidenceRequest request, CancellationToken ct = default) =>
            Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(evidence));
        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> StartAsync(Guid tenantId,
            ProductIdentityWorkflowStartRequest request, string token, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> GetStartResultAsync(Guid tenantId,
            ProductIdentityWorkflowStartResultRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
