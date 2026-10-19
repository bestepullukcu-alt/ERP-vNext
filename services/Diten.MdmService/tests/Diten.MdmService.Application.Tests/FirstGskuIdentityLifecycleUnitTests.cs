using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Commands;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Validators;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class FirstGskuIdentityLifecycleUnitTests
{
    [Fact]
    public void Start_plan_uses_exact_gsku_surface_and_fingerprints_pair_facts()
    {
        var revision = Revision();
        var gsku = Gsku();
        var factory = Factory();

        var first = factory.Create(TenantId, revision, gsku, OperationId, gsku.Version, MakerId);
        gsku.PackQuantity = 2m;
        var drifted = factory.Create(TenantId, revision, gsku, OperationId, gsku.Version, MakerId);

        Assert.Equal("gsku", first.TransportRequest.ObjectType);
        Assert.Equal(GskuId.ToString("D"), first.TransportRequest.ObjectId);
        Assert.Equal($"first-gsku-identity:{TenantId:D}:{OperationId:D}", first.Operation.StartIdempotencyKey);
        Assert.NotEqual(first.Operation.OperationFingerprint, drifted.Operation.OperationFingerprint);
        Assert.NotSame(gsku.PackApplicabilitySelection, first.Operation.PackApplicabilitySelection);
    }

    [Fact]
    public void Start_validator_rejects_missing_pair_identity()
    {
        var result = new StartFirstGskuIdentityWorkflowValidator().Validate(
            new StartFirstGskuIdentityWorkflowCommand(
                new StartFirstGskuIdentityWorkflowRequest(Guid.Empty, -1, Guid.Empty)));

        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void Submit_audit_is_deterministic_and_pair_specific()
    {
        var revision = Revision();
        var binding = Binding();

        var first = FirstGskuIdentityLifecycleAuditIntentFactory.CreateRevisionSubmit(revision, 0, binding);
        var replay = FirstGskuIdentityLifecycleAuditIntentFactory.CreateRevisionSubmit(revision, 0, binding);
        var gsku = FirstGskuIdentityLifecycleAuditIntentFactory.CreateGskuSubmit(Gsku(), 0, binding);

        Assert.Equal(first.IntentId, replay.IntentId);
        Assert.Equal(first.EvidenceHash, replay.EvidenceHash);
        Assert.NotEqual(first.IntentId, gsku.IntentId);
        Assert.Equal(ProductAuditOperation.ProductDefinitionRevisionIdentitySubmitted, first.Operation);
        Assert.Equal(ProductAuditOperation.GskuIdentitySubmitted, gsku.Operation);
    }

    [Fact]
    public async Task Enforced_rollout_defers_background_retirement_to_fresh_maker_replay()
    {
        var operation = new FirstGskuIdentityRetirementOperation
        {
            Id = Guid.NewGuid(), TenantId = TenantId, OperationId = OperationId
        };
        var rollout = ProductLegalEntityScopeRolloutState.CreatePreparation(
            TenantId, Guid.NewGuid(), MakerId, Now);
        rollout.Mode = ProductLegalEntityScopeRolloutMode.Enforced;
        var runner = new FirstGskuIdentityRetirementRecoveryRunner(
            new RetirementOperationRepository(operation),
            new ScopeRolloutRepository(rollout),
            null!);

        var result = await runner.RunAsync(
            OperationId, "test-worker", TimeSpan.FromSeconds(30));

        Assert.False(result.Succeeded);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("FIRST_GSKU_RETIREMENT_MAKER_REPLAY_REQUIRED", result.ErrorCode);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay,
            result.Operation!.RecoveryDisposition);
    }

    private static FirstGskuIdentityWorkflowStartRequestFactory Factory() => new(
        new(TemplateId, null, [ApproverId], "IDENTITY_APPROVAL", true, true, TimeSpan.FromDays(1)),
        new FixedTimeProvider(Now));

    private static ProductDefinitionRevision Revision() => new()
    {
        Id = RevisionId,
        TenantId = TenantId,
        GlobalProductId = ProductId,
        RevisionIdentifier = "REV-001",
        CreationCommandId = "CREATE-1",
        Version = 0,
        LifecycleStatus = ProductIdentityLifecycleStatus.Draft
    };

    private static Gsku Gsku() => new()
    {
        Id = GskuId,
        TenantId = TenantId,
        ProductDefinitionRevisionId = RevisionId,
        CanonicalCode = "GS-0001",
        CreationCommandId = "CREATE-1",
        Version = 0,
        LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
        PackApplicabilityCode = "SCALAR_QUANTITY_APPLIES",
        PackQuantity = 1m,
        PackUomCode = "EA",
        PackApplicabilitySelection = Selection("pack-applicability", "SCALAR_QUANTITY_APPLIES"),
        PackUomSelection = Selection("uom", "EA")
    };

    private static FirstGskuIdentityWorkflowBinding Binding() => new()
    {
        WorkflowInstanceId = Guid.Parse("a0000000-0000-0000-0000-000000000001"),
        WorkflowTemplateId = TemplateId,
        WorkflowTemplateVersionId = Guid.Parse("a0000000-0000-0000-0000-000000000002"),
        ApprovalTaskId = Guid.Parse("a0000000-0000-0000-0000-000000000003"),
        AssignmentSnapshotId = Guid.Parse("a0000000-0000-0000-0000-000000000004"),
        StartTransitionLogId = Guid.Parse("a0000000-0000-0000-0000-000000000005"),
        ObjectType = "gsku",
        GskuId = GskuId,
        ProductDefinitionRevisionId = RevisionId,
        ObjectRef = "GS-0001",
        SubmitterSubjectId = MakerId,
        StartIdempotencyKey = $"first-gsku-identity:{TenantId:D}:{OperationId:D}",
        StartRequestFingerprint = new string('a', 64),
        SubmittedAtUtc = Now,
        DueAtUtc = Now.AddDays(1)
    };

    private static ReferenceCatalogSelection Selection(string set, string code) => new()
    {
        SetCode = set,
        ValueCode = code,
        CatalogVersionId = Guid.Parse("a0000000-0000-0000-0000-000000000006"),
        CatalogVersionNumber = 1,
        ResolutionMode = ReferenceCatalogResolutionMode.Latest,
        ResolvedAtUtc = Now
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RetirementOperationRepository(FirstGskuIdentityRetirementOperation operation)
        : IFirstGskuIdentityRetirementOperationRepository
    {
        public Task<FirstGskuIdentityRetirementOperation?> GetByOperationIdAsync(
            Guid operationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<FirstGskuIdentityRetirementOperation?>(
                operation.OperationId == operationId ? operation : null);
        public Task<FirstGskuIdentityRetirementReserveResult> ReserveAsync(
            FirstGskuIdentityRetirementOperation value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<FirstGskuIdentityRetirementClaim?> TryClaimAsync(
            FirstGskuIdentityRetirementClaimRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> AdvanceAsync(FirstGskuIdentityRetirementClaim claim,
            FirstGskuIdentityRetirementMutation mutation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<FirstGskuIdentityRetirementRecoverablePage> DiscoverRecoverableAsync(
            long nowUtcTicks, int limit, FirstGskuIdentityRetirementRecoveryCursor? after = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 29, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid RevisionId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid GskuId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid OperationId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid MakerId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid ApproverId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid TemplateId = Guid.Parse("80000000-0000-0000-0000-000000000001");
}
