using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Validators;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class GlobalProductLifecycleUnitTests
{
    [Fact]
    public async Task Submit_exact_human_binding_persists_pending_state_and_audit()
    {
        var product = LifecycleTestData.Product(ProductIdentityLifecycleStatus.Draft, version: 0);
        var actor = new LifecycleTestActor(LifecycleTestData.Maker,
            ProductIdentityLifecyclePermissions.GlobalProductSubmit);
        var repository = new LifecycleTestGlobalProductRepository(product)
        {
            SubmitResult = new(true, LifecycleTestData.Product(
                ProductIdentityLifecycleStatus.PendingIdentityApproval,
                version: 1,
                LifecycleTestData.Binding()))
        };
        var handler = new SubmitGlobalProductIdentityHandler(repository, actor);

        var response = await handler.Handle(new(new(
            product.Id,
            0,
            LifecycleTestData.Binding())), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, response.Data!.LifecycleStatus);
        Assert.NotNull(repository.LastAuditIntent);
        Assert.Equal(ProductAuditOperation.GlobalProductIdentitySubmitted, repository.LastAuditIntent!.Operation);
        Assert.Equal(LifecycleTestData.Maker.ToString("D"), repository.LastAuditIntent.ActorId);
        Assert.Equal(0, repository.LastAuditIntent.PreVersion);
        Assert.Equal(1, repository.LastAuditIntent.PostVersion);
        Assert.Equal(1, repository.LastAuditIntent.Sequence);
    }

    [Fact]
    public async Task Reconcile_distinct_approver_and_coherent_evidence_applies_decision()
    {
        var binding = LifecycleTestData.Binding();
        var product = LifecycleTestData.Product(
            ProductIdentityLifecycleStatus.PendingIdentityApproval,
            version: 1,
            binding);
        var repository = new LifecycleTestGlobalProductRepository(product)
        {
            ReconcileResult = new(true, LifecycleTestData.Product(
                ProductIdentityLifecycleStatus.IdentityApproved,
                version: 2,
                binding))
        };
        var handler = new ReconcileGlobalProductIdentityDecisionHandler(repository);

        var response = await handler.Handle(new(new(
            product.Id,
            1,
            binding.WorkflowInstanceId,
            LifecycleTestData.Decision(ProductIdentityDecisionKind.Approved))), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, response.Data!.LifecycleStatus);
        Assert.Equal(ProductAuditOperation.GlobalProductIdentityApproved, repository.LastAuditIntent!.Operation);
        Assert.Equal(LifecycleTestData.Approver.ToString("D"), repository.LastAuditIntent.ActorId);
    }

    [Fact]
    public async Task Reconcile_same_subject_fails_maker_checker_before_repository_write()
    {
        var binding = LifecycleTestData.Binding();
        var repository = new LifecycleTestGlobalProductRepository(LifecycleTestData.Product(
            ProductIdentityLifecycleStatus.PendingIdentityApproval,
            version: 1,
            binding));
        var evidence = LifecycleTestData.Decision(ProductIdentityDecisionKind.Approved);
        evidence.DecisionActorSubjectId = LifecycleTestData.Maker;
        var handler = new ReconcileGlobalProductIdentityDecisionHandler(repository);

        var response = await handler.Handle(new(new(
            LifecycleTestData.ProductId,
            1,
            binding.WorkflowInstanceId,
            evidence)), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(403, response.StatusCode);
        Assert.Equal(0, repository.ReconcileCalls);
    }

    [Fact]
    public async Task Retire_passes_bounded_reason_into_stable_audit_evidence()
    {
        var product = LifecycleTestData.Product(ProductIdentityLifecycleStatus.IdentityApproved, version: 2);
        var repository = new LifecycleTestGlobalProductRepository(product)
        {
            RetireResult = new(true, LifecycleTestData.Product(ProductIdentityLifecycleStatus.Retired, version: 3))
        };
        var actor = new LifecycleTestActor(LifecycleTestData.Approver,
            ProductIdentityLifecyclePermissions.GlobalProductRetire);
        var handler = new RetireGlobalProductIdentityHandler(repository, actor, TimeProvider.System);
        var operationId = Guid.Parse("8a000000-0000-0000-0000-00000000008a");

        var response = await handler.Handle(new(new(
            product.Id, 2, operationId, "IDENTITY_RETIRED", "bounded comment")), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal(ProductAuditOperation.GlobalProductIdentityRetired, repository.LastAuditIntent!.Operation);
        Assert.Equal(operationId.ToString("D"), repository.LastAuditIntent.IdempotencyKey);
        Assert.Equal(2, repository.LastAuditIntent.PreVersion);
        Assert.Equal(3, repository.LastAuditIntent.PostVersion);
    }

    [Fact]
    public void Retire_validator_rejects_unbounded_or_non_exact_reason_and_comment()
    {
        var validator = new RetireGlobalProductIdentityValidator();
        var command = new RetireGlobalProductIdentityCommand(new(
            LifecycleTestData.ProductId,
            2,
            Guid.NewGuid(),
            " " + new string('R', 128),
            new string('C', 2001)));
        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("ReasonCode", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("Comment", StringComparison.Ordinal));
    }

    [Fact]
    public void Submit_validator_rejects_non_utc_and_non_exact_workflow_binding()
    {
        var binding = LifecycleTestData.Binding();
        binding.ObjectRef = " GP-TEST";
        binding.SubmittedAtUtc = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.FromHours(3));
        var command = new SubmitGlobalProductIdentityCommand(new(
            LifecycleTestData.ProductId,
            0,
            binding));
        var result = new SubmitGlobalProductIdentityValidator().Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("ObjectRef", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("SubmittedAtUtc", StringComparison.Ordinal));
    }

    [Fact]
    public void Retire_audit_fact_encoding_distinguishes_delimiter_bearing_tuples()
    {
        var product = LifecycleTestData.Product(ProductIdentityLifecycleStatus.IdentityApproved, 2);
        var operationId = Guid.Parse("8d000000-0000-0000-0000-00000000008d");
        var timestamp = new DateTimeOffset(2030, 1, 3, 0, 0, 0, TimeSpan.Zero);

        var first = ProductIdentityLifecycleAuditIntentFactory.CreateRetire(
            product,
            2,
            operationId,
            LifecycleTestData.Approver,
            "A|B",
            "C",
            timestamp);
        var second = ProductIdentityLifecycleAuditIntentFactory.CreateRetire(
            product,
            2,
            operationId,
            LifecycleTestData.Approver,
            "A",
            "B|C",
            timestamp);

        Assert.Equal(first.IntentId, second.IntentId);
        Assert.Equal(first.IdempotencyKey, second.IdempotencyKey);
        Assert.NotEqual(first.EvidenceHash, second.EvidenceHash);
    }
}

internal static class LifecycleTestData
{
    public static readonly Guid TenantId = Guid.Parse("82000000-0000-0000-0000-000000000082");
    public static readonly Guid ProductId = Guid.Parse("83000000-0000-0000-0000-000000000083");
    public static readonly Guid Maker = Guid.Parse("84000000-0000-0000-0000-000000000084");
    public static readonly Guid Approver = Guid.Parse("85000000-0000-0000-0000-000000000085");

    public static GlobalProduct Product(
        ProductIdentityLifecycleStatus status,
        int version,
        ProductIdentityWorkflowBinding? binding = null) => new()
    {
        Id = ProductId,
        TenantId = TenantId,
        CanonicalCode = "GP-TEST",
        GlobalProductName = "Test Product",
        GlobalProductNameNormalized = "TEST PRODUCT",
        LifecycleStatus = status,
        Version = version,
        WorkflowBinding = binding
    };

    public static ProductIdentityWorkflowBinding Binding() => new()
    {
        WorkflowInstanceId = Guid.Parse("86000000-0000-0000-0000-000000000086"),
        WorkflowTemplateId = Guid.Parse("87000000-0000-0000-0000-000000000087"),
        WorkflowTemplateVersionId = Guid.Parse("88000000-0000-0000-0000-000000000088"),
        ApprovalTaskId = Guid.Parse("89000000-0000-0000-0000-000000000089"),
        AssignmentSnapshotId = Guid.Parse("8b000000-0000-0000-0000-00000000008b"),
        StartTransitionLogId = Guid.Parse("8c000000-0000-0000-0000-00000000008c"),
        ObjectType = "global-product",
        ObjectId = ProductId,
        ObjectRef = "GP-TEST",
        SubmitterSubjectId = Maker,
        StartIdempotencyKey = "global-product-submit:gp-test",
        StartRequestFingerprint = new string('A', 64),
        SubmittedAtUtc = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero)
    };

    public static ProductIdentityWorkflowDecisionEvidence Decision(ProductIdentityDecisionKind decision) => new()
    {
        Decision = decision,
        WorkflowInstanceId = Binding().WorkflowInstanceId,
        ApprovalTaskId = Binding().ApprovalTaskId,
        WorkflowTemplateId = Binding().WorkflowTemplateId,
        WorkflowTemplateVersionId = Binding().WorkflowTemplateVersionId,
        ObjectType = "global-product",
        ObjectId = ProductId,
        ObjectRef = "GP-TEST",
        DecisionActorSubjectId = Approver,
        ReasonCode = decision == ProductIdentityDecisionKind.Approved ? "APPROVED" : "REJECTED",
        DecisionAtUtc = new DateTimeOffset(2030, 1, 2, 0, 0, 0, TimeSpan.Zero),
        TransitionSequence = 2,
        TaskStatus = decision.ToString(),
        InstanceStatus = decision.ToString()
    };
}

internal sealed class LifecycleTestActor(Guid actorId, params string[] permissions)
    : IProductIdentityLifecycleActorContext
{
    public bool IsResolvable { get; init; } = true;

    public bool TryResolveCanonicalHumanSubject(out Guid subjectId)
    {
        subjectId = IsResolvable ? actorId : Guid.Empty;
        return IsResolvable;
    }

    public bool HasPermission(string permission) =>
        permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}

internal sealed class LifecycleTestGlobalProductRepository(GlobalProduct? product) : IGlobalProductRepository
{
    public GlobalProductLifecycleWriteResult? SubmitResult { get; init; }
    public GlobalProductLifecycleWriteResult? ReconcileResult { get; init; }
    public GlobalProductLifecycleWriteResult? RetireResult { get; init; }
    public LocalAuditIntent? LastAuditIntent { get; private set; }
    public int SubmitCalls { get; private set; }
    public int ReconcileCalls { get; private set; }
    public int RetireCalls { get; private set; }

    public Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(product?.Id == id ? product : null);

    public Task<GlobalProduct?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<GlobalProduct?>(null);

    public Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<GlobalProductPage> GetPageAsync(int pageNumber, int pageSize, string? normalizedSearch,
        ProductIdentityLifecycleStatus? lifecycleStatus, CancellationToken cancellationToken = default) =>
        Task.FromResult(new GlobalProductPage([], 0));

    public Task<GlobalProductCreateResult> CreateDraftAsync(GlobalProduct globalProduct,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new GlobalProductCreateResult(false, null, "NOT_USED"));

    public Task<GlobalProductLifecycleWriteResult> SubmitIdentityAsync(Guid id, int expectedVersion,
        ProductIdentityWorkflowBinding workflowBinding, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default)
    {
        SubmitCalls++;
        LastAuditIntent = auditIntent;
        return Task.FromResult(SubmitResult ?? new(false, product, "PRODUCT_IDENTITY_STATE_CONFLICT"));
    }

    public Task<GlobalProductLifecycleWriteResult> ReconcileIdentityDecisionAsync(Guid id, int expectedVersion,
        ProductIdentityWorkflowDecisionEvidence decisionEvidence, LocalAuditIntent auditIntent,
        CancellationToken cancellationToken = default)
    {
        ReconcileCalls++;
        LastAuditIntent = auditIntent;
        return Task.FromResult(ReconcileResult ?? new(false, product, "PRODUCT_IDENTITY_STATE_CONFLICT"));
    }

    public Task<GlobalProductLifecycleWriteResult> RetireIdentityAsync(Guid id, int expectedVersion,
        LocalAuditIntent auditIntent, CancellationToken cancellationToken = default)
    {
        RetireCalls++;
        LastAuditIntent = auditIntent;
        return Task.FromResult(RetireResult ?? new(false, product, "PRODUCT_IDENTITY_STATE_CONFLICT"));
    }

    public Task<ProductChildCreationAdmissionResult> AcquireChildCreationAdmissionAsync(Guid id,
        string creationCommandId, string requestFingerprint, DateTimeOffset acquiredAtUtc,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProductChildCreationAdmissionResult(true, product));

    public Task<ProductChildCreationAdmissionResult> CompleteChildCreationAdmissionAsync(Guid id,
        string creationCommandId, string requestFingerprint, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProductChildCreationAdmissionResult(true, product));

    public Task<GlobalProductScopeCompletenessInventory> GetProductLegalEntityScopeCompletenessInventoryAsync(
        DateTimeOffset serverNowUtc, int maximumMissingItems, CancellationToken cancellationToken = default) =>
        Task.FromResult(new GlobalProductScopeCompletenessInventory(0, 0, []));
}
