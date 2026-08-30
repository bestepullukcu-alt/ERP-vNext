using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;

public sealed class RetireFinishedGoodIdentityHandler(
    IFinishedGoodRepository finishedGoods,
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions,
    IGlobalProductRepository products,
    IProductLegalEntityScopeRolloutStateRepository rolloutStates,
    IProductLegalEntityScopePolicyRepository scopePolicies,
    ProductLegalEntityScopeCandidateFacade scopeCandidates,
    ITenantContext tenantContext,
    IProductIdentityLifecycleActorContext actorContext,
    TimeProvider clock)
    : IRequestHandler<RetireFinishedGoodIdentityCommand, Response<FinishedGoodIdentityLifecycleResult>>
{
    public async Task<Response<FinishedGoodIdentityLifecycleResult>> Handle(
        RetireFinishedGoodIdentityCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var input = command.Request;
        if (!actorContext.TryResolveCanonicalHumanSubject(out var actor)
            || !actorContext.HasPermission(FinishedGoodIdentityLifecyclePermissions.Retire))
            return Fail("FINISHED_GOOD_IDENTITY_LIFECYCLE_FORBIDDEN", 403);
        if (input is null || input.FinishedGoodId == Guid.Empty || input.ExpectedVersion < 0
            || input.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(input.ReasonCode))
            return Fail("FINISHED_GOOD_IDENTITY_RETIRE_REQUEST_INVALID", 400);

        var scopeGuard = new ProductLegalEntityScopeConsumerGuard(
            rolloutStates, scopePolicies, scopeCandidates, tenantContext);
        var scope = await scopeGuard.ResolveContextAsync(
            FinishedGoodIdentityLifecyclePermissions.Retire, cancellationToken);
        if (!scope.IsSuccessful) return Fail(scope.FailureCode!, scope.StatusCode);

        var finishedGood = await finishedGoods.GetByIdAsync(input.FinishedGoodId, cancellationToken);
        if (finishedGood is null) return Fail("FINISHED_GOOD_IDENTITY_NOT_FOUND", 404);
        var gsku = await gskus.GetByIdAsync(finishedGood.GskuId, cancellationToken);
        var revision = gsku is null ? null
            : await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        var product = revision is null ? null
            : await products.GetByIdAsync(revision.GlobalProductId, cancellationToken);
        if (gsku is null || revision is null || product is null)
            return Fail("FINISHED_GOOD_IDENTITY_NOT_FOUND", 404);
        var decision = await scopeGuard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        if (!decision.Allowed) return Fail("FINISHED_GOOD_IDENTITY_NOT_FOUND", 404);
        var audit = FinishedGoodIdentityLifecycleAuditIntentFactory.CreateRetire(
            finishedGood, input.ExpectedVersion, input.OperationId, actor,
            input.ReasonCode, input.Comment, clock.GetUtcNow());
        var result = await finishedGoods.RetireIdentityAsync(
            input.FinishedGoodId, input.ExpectedVersion, audit, cancellationToken);
        if (!result.Succeeded || result.FinishedGood is null)
            return Fail(result.ErrorCode ?? "FINISHED_GOOD_IDENTITY_STATE_CONFLICT", StatusFor(result.ErrorCode));
        var updated = result.FinishedGood;
        return Response<FinishedGoodIdentityLifecycleResult>.Success(new(
            updated.Id, updated.LifecycleStatus, updated.Version,
            updated.IdentityWorkflowBinding?.WorkflowInstanceId, result.IsReplay));
    }

    private static int StatusFor(string? code) => code switch
    {
        "FINISHED_GOOD_IDENTITY_NOT_FOUND" => 404,
        "AUDIT_INTENT_CAPACITY_EXCEEDED" => 503,
        _ => 409
    };

    private static Response<FinishedGoodIdentityLifecycleResult> Fail(string code, int status) =>
        Response<FinishedGoodIdentityLifecycleResult>.Fail(code, status);
}
