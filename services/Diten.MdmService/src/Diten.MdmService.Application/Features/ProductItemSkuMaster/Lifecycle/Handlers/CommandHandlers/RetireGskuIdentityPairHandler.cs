using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;

public sealed class RetireGskuIdentityPairHandler(
    FirstGskuIdentityRetirementProcessor processor,
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions,
    IGlobalProductRepository products,
    IProductLegalEntityScopeRolloutStateRepository rolloutStates,
    IProductLegalEntityScopePolicyRepository scopePolicies,
    ProductLegalEntityScopeCandidateFacade scopeCandidates,
    ITenantContext tenantContext,
    IProductIdentityLifecycleActorContext actorContext)
    : IRequestHandler<RetireGskuIdentityPairCommand, Response<GskuPairRetirementResult>>
{
    public const string DirectRetirementIsSystemOnly = "GSKU_DIRECT_RETIREMENT_SYSTEM_ONLY";

    public async Task<Response<GskuPairRetirementResult>> Handle(
        RetireGskuIdentityPairCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // B5 (WP-MDM-GSKU-ACCEPT-01 FIX1): retiring a GSKU directly, without an approved request, is a system action
        // (separation of duties). A person — whatever permission they hold — is refused; a person retires through a
        // retirement REQUEST, which is approved, and whose approval the system carries out (the pair retirement).
        // No system actor calls this endpoint today: until an inbound service identity exists in MDM, it is closed.
        if (actorContext.TryResolveCanonicalHumanSubject(out _))
        {
            return Fail(DirectRetirementIsSystemOnly, 403);
        }
        Guid actorId = Guid.Empty;
        if (actorId == Guid.Empty || !actorContext.HasPermission(GskuPairRetirementPermissions.Retire))
        {
            return Fail("PRODUCT_IDENTITY_LIFECYCLE_FORBIDDEN", 403);
        }
        var input = request.Request;
        if (input is null)
        {
            return Fail("FIRST_GSKU_RETIREMENT_REQUEST_INVALID", 400);
        }
        var scopeGuard = new ProductLegalEntityScopeConsumerGuard(
            rolloutStates, scopePolicies, scopeCandidates, tenantContext);
        var scope = await scopeGuard.ResolveContextAsync(
            GskuPairRetirementPermissions.Retire, cancellationToken);
        if (!scope.IsSuccessful) return Fail(scope.FailureCode!, scope.StatusCode);
        var currentGsku = await gskus.GetByIdAsync(input.GskuId, cancellationToken);
        var currentRevision = currentGsku is null ? null
            : await revisions.GetByIdAsync(currentGsku.ProductDefinitionRevisionId, cancellationToken);
        var product = currentRevision is null ? null
            : await products.GetByIdAsync(currentRevision.GlobalProductId, cancellationToken);
        if (currentGsku is null || currentRevision is null || product is null)
            return Fail("GSKU_IDENTITY_NOT_FOUND", 404);
        var decision = await scopeGuard.EvaluateAsync(scope.Context!, product.Id, cancellationToken);
        if (!decision.Allowed) return Fail("GSKU_IDENTITY_NOT_FOUND", 404);
        var result = await processor.StartAsync(
            input.GskuId,
            input.ExpectedGskuVersion,
            input.OperationId,
            actorId,
            input.ReasonCode,
            $"interactive:{actorId:D}",
            TimeSpan.FromMinutes(1),
            cancellationToken);
        if (!result.Succeeded || result.Operation is null)
        {
            return Fail(result.ErrorCode ?? "FIRST_GSKU_RETIREMENT_RECONCILIATION_REQUIRED", result.StatusCode);
        }

        var operation = result.Operation;
        var gsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
        var revision = await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken);
        if (gsku is null || revision is null)
        {
            return Fail("FIRST_GSKU_RETIREMENT_RECONCILIATION_REQUIRED", 409);
        }
        return Response<GskuPairRetirementResult>.Success(new(
            revision.Id,
            gsku.Id,
            revision.LifecycleStatus,
            gsku.LifecycleStatus,
            revision.Version,
            gsku.Version,
            result.IsReplay));
    }

    private static Response<GskuPairRetirementResult> Fail(string code, int statusCode) =>
        Response<GskuPairRetirementResult>.Fail(code, statusCode);
}
