using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;

public sealed class RetireGlobalProductIdentityHandler
    : IRequestHandler<RetireGlobalProductIdentityCommand, Response<GlobalProductIdentityLifecycleResult>>
{
    private readonly IGlobalProductRepository _products;
    private readonly IProductIdentityLifecycleActorContext _actorContext;
    private readonly TimeProvider _clock;

    public RetireGlobalProductIdentityHandler(
        IGlobalProductRepository products,
        IProductIdentityLifecycleActorContext actorContext,
        TimeProvider clock)
    {
        _products = products;
        _actorContext = actorContext;
        _clock = clock;
    }

    public async Task<Response<GlobalProductIdentityLifecycleResult>> Handle(
        RetireGlobalProductIdentityCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var input = request.Request;
        if (!_actorContext.TryResolveCanonicalHumanSubject(out var actorId)
            || !_actorContext.HasPermission(ProductIdentityLifecyclePermissions.GlobalProductRetire))
        {
            return Fail("PRODUCT_IDENTITY_LIFECYCLE_FORBIDDEN", 403);
        }

        if (input is null || input.GlobalProductId == Guid.Empty || input.ExpectedVersion < 0
            || input.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(input.ReasonCode))
        {
            return Fail("PRODUCT_IDENTITY_RETIRE_REQUEST_INVALID", 400);
        }

        var product = await _products.GetByIdAsync(input.GlobalProductId, cancellationToken);
        if (product is null)
        {
            return Fail("PRODUCT_IDENTITY_NOT_FOUND", 404);
        }

        var auditIntent = ProductIdentityLifecycleAuditIntentFactory.CreateRetire(
            product,
            input.ExpectedVersion,
            input.OperationId,
            actorId,
            input.ReasonCode,
            input.Comment,
            _clock.GetUtcNow());
        var result = await _products.RetireIdentityAsync(
            input.GlobalProductId,
            input.ExpectedVersion,
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

    private static Response<GlobalProductIdentityLifecycleResult> Fail(string code, int statusCode) =>
        Response<GlobalProductIdentityLifecycleResult>.Fail(code, statusCode);
}
