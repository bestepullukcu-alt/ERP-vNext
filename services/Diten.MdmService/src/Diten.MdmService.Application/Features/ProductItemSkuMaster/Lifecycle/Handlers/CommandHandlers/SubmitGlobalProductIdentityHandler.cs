using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;

public sealed class SubmitGlobalProductIdentityHandler
    : IRequestHandler<SubmitGlobalProductIdentityCommand, Response<GlobalProductIdentityLifecycleResult>>
{
    private readonly IGlobalProductRepository _products;
    private readonly IProductIdentityLifecycleActorContext _actorContext;

    public SubmitGlobalProductIdentityHandler(
        IGlobalProductRepository products,
        IProductIdentityLifecycleActorContext actorContext)
    {
        _products = products;
        _actorContext = actorContext;
    }

    public async Task<Response<GlobalProductIdentityLifecycleResult>> Handle(
        SubmitGlobalProductIdentityCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var input = request.Request;
        if (!_actorContext.TryResolveCanonicalHumanSubject(out var actorId)
            || !_actorContext.HasPermission(ProductIdentityLifecyclePermissions.GlobalProductSubmit))
        {
            return Fail("PRODUCT_IDENTITY_LIFECYCLE_FORBIDDEN", 403);
        }

        if (input?.WorkflowBinding is null
            || input.GlobalProductId == Guid.Empty
            || input.ExpectedVersion < 0
            || input.WorkflowBinding.SubmitterSubjectId != actorId
            || input.WorkflowBinding.ObjectId != input.GlobalProductId
            || !string.Equals(input.WorkflowBinding.ObjectType, "GlobalProduct", StringComparison.Ordinal))
        {
            return Fail("WORKFLOW_BINDING_CONTRACT_INVALID", 400);
        }

        var product = await _products.GetByIdAsync(input.GlobalProductId, cancellationToken);
        if (product is null)
        {
            return Fail("PRODUCT_IDENTITY_NOT_FOUND", 404);
        }

        var auditIntent = ProductIdentityLifecycleAuditIntentFactory.CreateSubmit(
            product,
            input.ExpectedVersion,
            input.WorkflowBinding);
        var result = await _products.SubmitIdentityAsync(
            input.GlobalProductId,
            input.ExpectedVersion,
            input.WorkflowBinding,
            auditIntent,
            cancellationToken);
        return ToResponse(result);
    }

    private static Response<GlobalProductIdentityLifecycleResult> ToResponse(
        GlobalProductLifecycleWriteResult result)
    {
        if (!result.Succeeded || result.GlobalProduct is null)
        {
            return Fail(result.ErrorCode ?? "PRODUCT_IDENTITY_STATE_CONFLICT", StatusFor(result.ErrorCode));
        }

        var product = result.GlobalProduct;
        return Response<GlobalProductIdentityLifecycleResult>.Success(
            new(product.Id, product.LifecycleStatus, product.Version,
                product.WorkflowBinding?.WorkflowInstanceId, result.IsReplay));
    }

    internal static int StatusFor(string? code) => code switch
    {
        "PRODUCT_IDENTITY_NOT_FOUND" => 404,
        "AUDIT_INTENT_CAPACITY_EXCEEDED" => 503,
        "WORKFLOW_BINDING_CONTRACT_INVALID" or "AUDIT_INTENT_CONTRACT_INVALID" => 400,
        _ => 409
    };

    private static Response<GlobalProductIdentityLifecycleResult> Fail(string code, int statusCode) =>
        Response<GlobalProductIdentityLifecycleResult>.Fail(code, statusCode);
}
