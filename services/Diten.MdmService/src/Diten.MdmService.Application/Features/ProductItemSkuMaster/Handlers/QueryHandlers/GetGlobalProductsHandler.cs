using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Queries;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;

public sealed class GetGlobalProductsHandler : IRequestHandler<GetGlobalProductsQuery, Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductListItemDto>>>
{
    private readonly IGlobalProductRepository _repository;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;

    public GetGlobalProductsHandler(
        IGlobalProductRepository repository,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _scopeGuard = new ProductLegalEntityScopeConsumerGuard(
            rolloutStates,
            policies,
            candidates,
            tenantContext);
    }

    public async Task<Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductListItemDto>>> Handle(
        GetGlobalProductsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var search = NormalizeSearch(request.Search);
        var scope = await _scopeGuard.ResolveContextAsync(
            ProductLegalEntityScopeConsumerGuard.GlobalProductReadPermission,
            cancellationToken);
        if (!scope.IsSuccessful)
        {
            return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductListItemDto>>
                .Fail(scope.FailureCode!, scope.StatusCode);
        }

        var page = scope.Context!.RolloutMode switch
        {
            ProductLegalEntityScopeRolloutMode.Preparation => await _repository.GetPageAsync(
                request.PageNumber,
                request.PageSize,
                search,
                request.LifecycleStatus,
                cancellationToken),
            ProductLegalEntityScopeRolloutMode.Enforced => await _repository.GetEnforcedLegalEntityScopePageAsync(
                request.PageNumber,
                request.PageSize,
                search,
                request.LifecycleStatus,
                referenceableOnly: false,
                scope.Context.EffectiveCandidateLegalEntityIds,
                scope.Context.ServerNowUtc,
                cancellationToken),
            _ => new GlobalProductPage([], 0)
        };
        var items = page.Items.Select(x => new ProductItemSkuMasterModels.GlobalProductListItemDto(
            x.Id,
            x.CanonicalCode,
            x.GlobalProductName,
            x.LifecycleStatus)).ToList();

        return Response<ProductItemSkuMasterModels.PagedResult<ProductItemSkuMasterModels.GlobalProductListItemDto>>.Success(
            new(items, request.PageNumber, request.PageSize, page.TotalCount));
    }

    private static string? NormalizeSearch(string? search)
        => string.IsNullOrWhiteSpace(search) ? null : GlobalProductNameRules.NormalizeDuplicateKey(search);

}

public sealed class ProductLegalEntityScopeConsumerGuard
{
    public const string ProductItemSkuMasterModuleCode = "product-item-sku-master";
    public const string GlobalProductReadPermission = "mdm.global-products.read";

    private readonly IProductLegalEntityScopeRolloutStateRepository _rolloutStates;
    private readonly IProductLegalEntityScopePolicyRepository _policies;
    private readonly ProductLegalEntityScopeCandidateFacade _candidates;
    private readonly ITenantContext _tenantContext;
    private readonly ProductLegalEntityScopeEvaluator _evaluator = new();

    public ProductLegalEntityScopeConsumerGuard(
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext)
    {
        _rolloutStates = rolloutStates;
        _policies = policies;
        _candidates = candidates;
        _tenantContext = tenantContext;
    }

    public async Task<ProductLegalEntityScopeConsumerContextResult> ResolveContextAsync(
        string permissionKey,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved || _tenantContext.TenantId == Guid.Empty)
        {
            return ProductLegalEntityScopeConsumerContextResult.Fail(400, "TENANT_CONTEXT_REQUIRED");
        }

        var rollout = await _rolloutStates.GetAsync(cancellationToken);
        if (rollout is null)
        {
            return ProductLegalEntityScopeConsumerContextResult.Success(new(
                _tenantContext.TenantId,
                ProductLegalEntityScopeRolloutMode.Preparation,
                null,
                DateTimeOffset.UtcNow,
                []));
        }

        try
        {
            rollout.EnsureValid();
        }
        catch (InvalidOperationException)
        {
            return ProductLegalEntityScopeConsumerContextResult.Fail(
                503,
                "PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID");
        }
        if (rollout.IsDeleted || rollout.TenantId != _tenantContext.TenantId)
        {
            return ProductLegalEntityScopeConsumerContextResult.Fail(
                503,
                "PRODUCT_LEGAL_ENTITY_SCOPE_ROLLOUT_INVALID");
        }

        var now = DateTimeOffset.UtcNow;
        if (rollout.Mode != ProductLegalEntityScopeRolloutMode.Enforced)
        {
            return ProductLegalEntityScopeConsumerContextResult.Success(new(
                _tenantContext.TenantId,
                rollout.Mode,
                rollout,
                now,
                []));
        }

        var candidates = await _candidates.ResolveAsync(
            ProductItemSkuMasterModuleCode,
            permissionKey,
            cancellationToken);
        if (!candidates.IsSuccessful)
        {
            return ProductLegalEntityScopeConsumerContextResult.Fail(
                candidates.StatusCode,
                candidates.FailureCode!);
        }

        return ProductLegalEntityScopeConsumerContextResult.Success(new(
            _tenantContext.TenantId,
            rollout.Mode,
            rollout,
            now,
            candidates.LegalEntityIds));
    }

    public async Task<ProductLegalEntityScopeConsumerDecision> EvaluateAsync(
        ProductLegalEntityScopeConsumerContext context,
        Guid globalProductId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TenantId != _tenantContext.TenantId || globalProductId == Guid.Empty)
        {
            return ProductLegalEntityScopeConsumerDecision.Denied(
                ProductLegalEntityScopeDecisionReason.InvalidScopeEvidence);
        }
        if (context.RolloutMode == ProductLegalEntityScopeRolloutMode.Preparation)
        {
            return ProductLegalEntityScopeConsumerDecision.AllowedDecision(
                ProductLegalEntityScopeDecisionReason.PreparationExistingTenantAccessAllowed);
        }
        if (context.RolloutMode != ProductLegalEntityScopeRolloutMode.Enforced)
        {
            return ProductLegalEntityScopeConsumerDecision.Denied(
                ProductLegalEntityScopeDecisionReason.FailClosedSuspended);
        }

        try
        {
            var policy = await _policies.GetByGlobalProductIdAsync(globalProductId, cancellationToken);
            if (context.RolloutState is null)
            {
                return ProductLegalEntityScopeConsumerDecision.Denied(
                    ProductLegalEntityScopeDecisionReason.InvalidScopeEvidence);
            }
            var evaluation = _evaluator.Evaluate(new ProductLegalEntityScopeEvaluationRequest(
                context.TenantId,
                globalProductId,
                context.RolloutState,
                policy,
                context.ServerNowUtc,
                ExistingTenantAccessAllowed: true,
                context.EffectiveCandidateLegalEntityIds,
                context.EffectiveCandidateLegalEntityIds));
            return new(evaluation.Allowed, evaluation.Reason);
        }
        catch (InvalidOperationException)
        {
            return ProductLegalEntityScopeConsumerDecision.Denied(
                ProductLegalEntityScopeDecisionReason.InvalidScopeEvidence);
        }
        catch (ArgumentException)
        {
            return ProductLegalEntityScopeConsumerDecision.Denied(
                ProductLegalEntityScopeDecisionReason.InvalidScopeEvidence);
        }
        catch (FormatException)
        {
            return ProductLegalEntityScopeConsumerDecision.Denied(
                ProductLegalEntityScopeDecisionReason.InvalidScopeEvidence);
        }
        catch (OverflowException)
        {
            return ProductLegalEntityScopeConsumerDecision.Denied(
                ProductLegalEntityScopeDecisionReason.InvalidScopeEvidence);
        }
        catch (Exception exception) when (
            string.Equals(
                exception.GetType().FullName,
                "MongoDB.Bson.Serialization.BsonSerializationException",
                StringComparison.Ordinal))
        {
            return ProductLegalEntityScopeConsumerDecision.Denied(
                ProductLegalEntityScopeDecisionReason.InvalidScopeEvidence);
        }
    }
}

public sealed record ProductLegalEntityScopeConsumerContext(
    Guid TenantId,
    ProductLegalEntityScopeRolloutMode RolloutMode,
    ProductLegalEntityScopeRolloutState? RolloutState,
    DateTimeOffset ServerNowUtc,
    IReadOnlyList<Guid> EffectiveCandidateLegalEntityIds);

public sealed record ProductLegalEntityScopeConsumerContextResult(
    bool IsSuccessful,
    int StatusCode,
    string? FailureCode,
    ProductLegalEntityScopeConsumerContext? Context)
{
    public static ProductLegalEntityScopeConsumerContextResult Success(
        ProductLegalEntityScopeConsumerContext context) => new(true, 200, null, context);

    public static ProductLegalEntityScopeConsumerContextResult Fail(
        int statusCode,
        string failureCode) => new(false, statusCode, failureCode, null);
}

public sealed record ProductLegalEntityScopeConsumerDecision(
    bool Allowed,
    ProductLegalEntityScopeDecisionReason Reason)
{
    public static ProductLegalEntityScopeConsumerDecision AllowedDecision(
        ProductLegalEntityScopeDecisionReason reason) => new(true, reason);

    public static ProductLegalEntityScopeConsumerDecision Denied(
        ProductLegalEntityScopeDecisionReason reason) => new(false, reason);
}
