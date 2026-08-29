using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;

public sealed class ReconcileGlobalProductIdentityDecisionHandler
    : IRequestHandler<ReconcileGlobalProductIdentityDecisionCommand,
        Response<GlobalProductIdentityLifecycleResult>>
{
    private readonly IGlobalProductRepository _products;

    public ReconcileGlobalProductIdentityDecisionHandler(IGlobalProductRepository products)
    {
        _products = products;
    }

    public async Task<Response<GlobalProductIdentityLifecycleResult>> Handle(
        ReconcileGlobalProductIdentityDecisionCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var input = request.Request;
        if (input?.DecisionEvidence is null || input.GlobalProductId == Guid.Empty
            || input.ExpectedVersion < 0 || input.ExpectedWorkflowInstanceId == Guid.Empty)
        {
            return Fail("WORKFLOW_DECISION_EVIDENCE_INVALID", 400);
        }

        var product = await _products.GetByIdAsync(input.GlobalProductId, cancellationToken);
        if (product is null)
        {
            return Fail("PRODUCT_IDENTITY_NOT_FOUND", 404);
        }

        var binding = product.WorkflowBinding;
        if (binding is null
            || binding.WorkflowInstanceId != input.ExpectedWorkflowInstanceId
            || !EvidenceMatches(binding, input.DecisionEvidence, input.GlobalProductId))
        {
            return Fail("WORKFLOW_BINDING_CONFLICT", 409);
        }

        if (binding.SubmitterSubjectId == input.DecisionEvidence.DecisionActorSubjectId)
        {
            return Fail("PRODUCT_IDENTITY_MAKER_CHECKER_VIOLATION", 403);
        }

        var auditIntent = ProductIdentityLifecycleAuditIntentFactory.CreateDecision(
            product,
            input.ExpectedVersion,
            input.DecisionEvidence);
        var result = await _products.ReconcileIdentityDecisionAsync(
            input.GlobalProductId,
            input.ExpectedVersion,
            input.DecisionEvidence,
            auditIntent,
            cancellationToken);
        if (!result.Succeeded || result.GlobalProduct is null)
        {
            return Fail(
                result.ErrorCode ?? "PRODUCT_IDENTITY_STATE_CONFLICT",
                SubmitGlobalProductIdentityHandler.StatusFor(result.ErrorCode));
        }

        var updated = result.GlobalProduct;
        return Response<GlobalProductIdentityLifecycleResult>.Success(
            new(updated.Id, updated.LifecycleStatus, updated.Version,
                updated.WorkflowBinding?.WorkflowInstanceId, result.IsReplay));
    }

    private static bool EvidenceMatches(
        ProductIdentityWorkflowBinding binding,
        ProductIdentityWorkflowDecisionEvidence evidence,
        Guid globalProductId)
    {
        var expectedStatus = evidence.Decision switch
        {
            ProductIdentityDecisionKind.Approved => "Approved",
            ProductIdentityDecisionKind.Rejected => "Rejected",
            _ => string.Empty
        };
        return expectedStatus.Length > 0
            && evidence.WorkflowInstanceId == binding.WorkflowInstanceId
            && evidence.ApprovalTaskId == binding.ApprovalTaskId
            && evidence.WorkflowTemplateId == binding.WorkflowTemplateId
            && evidence.WorkflowTemplateVersionId == binding.WorkflowTemplateVersionId
            && evidence.ObjectId == globalProductId
            && string.Equals(evidence.ObjectType, binding.ObjectType, StringComparison.Ordinal)
            && string.Equals(evidence.ObjectRef, binding.ObjectRef, StringComparison.Ordinal)
            && string.Equals(evidence.TaskStatus, expectedStatus, StringComparison.Ordinal)
            && string.Equals(evidence.InstanceStatus, expectedStatus, StringComparison.Ordinal)
            && evidence.TransitionSequence > 0
            && evidence.DecisionAtUtc.Offset == TimeSpan.Zero;
    }

    private static Response<GlobalProductIdentityLifecycleResult> Fail(string code, int statusCode) =>
        Response<GlobalProductIdentityLifecycleResult>.Fail(code, statusCode);
}
