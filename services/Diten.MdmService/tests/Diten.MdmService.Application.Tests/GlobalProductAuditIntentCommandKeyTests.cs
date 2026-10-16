using System.Reflection;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// BL-552 flow 3 FIX2 K2 — the invariant the Global Product store's receipt replay rests on (flow 3 FIX1 item 10): every
/// intent a Global Product LIFECYCLE write carries has its command id equal to its idempotency key, so a receipt (which
/// keeps only the key and the intent id) still pins the command. Walked over every writer that reaches the replay check.
/// The draft creation is the one writer that differs (key = command + ":global-product-create"); it is an insert and
/// never meets the replay check — named here so a change on either side is seen.
/// </summary>
public sealed class GlobalProductAuditIntentCommandKeyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.Parse("41000000-0000-0000-0000-000000000001");

    private static GlobalProduct Product() => new()
    {
        Id = Guid.Parse("21000000-0000-0000-0000-000000000001"),
        TenantId = Guid.Parse("11000000-0000-0000-0000-000000000001"),
        CanonicalCode = "GP-000000000001", GlobalProductName = "Product", GlobalProductNameNormalized = "PRODUCT"
    };

    public static IEnumerable<object[]> LifecycleIntents()
    {
        var product = Product();
        var binding = new ProductIdentityWorkflowBinding
        {
            WorkflowInstanceId = Guid.NewGuid(), WorkflowTemplateId = Guid.NewGuid(), WorkflowTemplateVersionId = Guid.NewGuid(),
            ApprovalTaskId = Guid.NewGuid(), AssignmentSnapshotId = Guid.NewGuid(), StartTransitionLogId = Guid.NewGuid(),
            ObjectType = "GlobalProduct", ObjectId = product.Id, ObjectRef = "GP-000000000001", SubmitterSubjectId = Actor,
            StartIdempotencyKey = "start-key", StartRequestFingerprint = new string('a', 64), SubmittedAtUtc = Now
        };
        var decision = new ProductIdentityWorkflowDecisionEvidence
        {
            Decision = ProductIdentityDecisionKind.Approved, WorkflowInstanceId = binding.WorkflowInstanceId,
            ApprovalTaskId = binding.ApprovalTaskId, WorkflowTemplateId = binding.WorkflowTemplateId,
            WorkflowTemplateVersionId = binding.WorkflowTemplateVersionId, ObjectType = "GlobalProduct",
            ObjectId = product.Id, ObjectRef = binding.ObjectRef, DecisionActorSubjectId = Guid.NewGuid(),
            DecisionAtUtc = Now, TransitionSequence = 2, TaskStatus = "Approved", InstanceStatus = "Completed"
        };
        var cancellation = new ProductIdentityWorkflowCancellationEvidence
        {
            WorkflowInstanceId = binding.WorkflowInstanceId, ApprovalTaskId = binding.ApprovalTaskId,
            WorkflowTemplateId = binding.WorkflowTemplateId, WorkflowTemplateVersionId = binding.WorkflowTemplateVersionId,
            ObjectType = "GlobalProduct", ObjectId = product.Id, ObjectRef = binding.ObjectRef, RequesterSubjectId = Actor,
            ReasonCode = "REQUESTER_WITHDRAWAL", CancelledAtUtc = Now, TransitionSequence = 3, TransitionLogId = Guid.NewGuid(),
            TaskStatus = "Cancelled", InstanceStatus = "Cancelled", WorkflowInstanceVersion = 4, ApprovalTaskVersion = 6,
            IdempotencyKey = Guid.NewGuid().ToString("D")
        };
        yield return ["submit", ProductIdentityLifecycleAuditIntentFactory.CreateSubmit(product, 0, binding)];
        yield return ["decision", ProductIdentityLifecycleAuditIntentFactory.CreateDecision(product, 1, decision)];
        yield return ["retire", ProductIdentityLifecycleAuditIntentFactory.CreateRetire(product, 2, Guid.NewGuid(), Actor,
            "DISCONTINUED", null, Now)];
        yield return ["withdrawal", ProductIdentityLifecycleAuditIntentFactory.CreateWithdrawal(product, 1, cancellation)];
        yield return ["decision-not-applicable", ProductIdentityLifecycleAuditIntentFactory.CreateDecisionNotApplicable(
            product, 1, Guid.NewGuid(), binding.WorkflowInstanceId, binding.ApprovalTaskId, Actor, "CLOSED", Now)];
        foreach (var operation in new[]
                 {
                     ProductAuditOperation.GlobalProductCorrectionRequested, ProductAuditOperation.GlobalProductCorrectionApplied,
                     ProductAuditOperation.GlobalProductCorrectionRejected, ProductAuditOperation.GlobalProductCorrectionEnded
                 })
            yield return [$"correction:{operation}", GlobalProductCorrectionAuditIntentFactory.Create(product, 0, Guid.NewGuid(),
                Actor, operation, "Proposed", Now, operation == ProductAuditOperation.GlobalProductCorrectionEnded ? "WHY" : null)];
        foreach (var operation in new[]
                 {
                     ProductAuditOperation.GlobalProductRetirementRequested, ProductAuditOperation.GlobalProductIdentityRetired,
                     ProductAuditOperation.GlobalProductRetirementRejected, ProductAuditOperation.GlobalProductRetirementEnded
                 })
            yield return [$"retirement:{operation}", GlobalProductRetirementRequestAuditIntentFactory.Create(product, 0,
                Guid.NewGuid(), Actor, operation, "Reason", Now,
                operation == ProductAuditOperation.GlobalProductRetirementEnded ? "WHY" : null)];
        var draftUpdate = typeof(UpdateGlobalProductDraftHandler).GetMethod("CreateAuditIntent",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        yield return ["draft-update", (LocalAuditIntent)draftUpdate.Invoke(null,
            [product, 0, Guid.NewGuid(), Actor, "Renamed", "RENAMED", Now])!];
    }

    [Theory]
    [MemberData(nameof(LifecycleIntents))]
    public void Every_lifecycle_intent_carries_its_command_id_as_its_key(string writer, LocalAuditIntent intent)
    {
        Assert.False(string.IsNullOrWhiteSpace(intent.CommandId), writer);
        Assert.Equal(intent.IdempotencyKey, intent.CommandId);
    }
}
