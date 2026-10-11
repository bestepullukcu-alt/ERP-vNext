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

        var sort = request.ResolveSort();
        var page = scope.Context!.RolloutMode switch
        {
            ProductLegalEntityScopeRolloutMode.Preparation => await _repository.GetPageAsync(
                request.PageNumber,
                request.PageSize,
                search,
                request.LifecycleStatus,
                sort,
                cancellationToken),
            ProductLegalEntityScopeRolloutMode.Enforced => await _repository.GetEnforcedLegalEntityScopePageAsync(
                request.PageNumber,
                request.PageSize,
                search,
                request.LifecycleStatus,
                referenceableOnly: false,
                sort,
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
    private readonly TimeProvider _clock = TimeProvider.System;

    /// <summary>WP-MDM-SCOPE-ACCEPT-01 FIX1 S12 — the guard's "now" from a <see cref="TimeProvider"/> (tests set it).</summary>
    public ProductLegalEntityScopeConsumerGuard(
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext,
        TimeProvider clock)
        : this(rolloutStates, policies, candidates, tenantContext)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

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

        ProductLegalEntityScopeRolloutState? rollout;
        try
        {
            rollout = await _rolloutStates.GetAsync(cancellationToken);
        }
        // WP-MDM-SCOPE-INT-01 FIX1 M7: the caller going away is not a store that cannot be reached — a read cancelled by
        // its own client (the driver may surface it as a MongoException) propagates instead of answering a coded 503.
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested
                                          && ProductLegalEntityScopeWriteFenceCoordinator.ClassifyRolloutReadFailure(exception)
                                          != ProductLegalEntityScopeWriteFenceCoordinator.RolloutReadFailure.None)
        {
            // WP-MDM-SCOPE-ACCEPT-01 FIX1 B2 / WP-MDM-SCOPE-INT-01 BL-567: a rollout read that fails answers the same
            // coded 503 as the scope writes — the record cannot be read (ROLLOUT_INVALID: the repository's refusal, a
            // record the serializer cannot map) or the store cannot be reached (ROLLOUT_STORE_UNAVAILABLE: timeout,
            // MongoException). Before, only the repository's own refusal was mapped and the rest was a 500 on every
            // product read of the tenant (GP / GSKU / LSKU / FG lists and details).
            var (code, status) = ProductLegalEntityScopeWriteFenceCoordinator.ReadFailureAnswer(exception, "read_guard");
            return ProductLegalEntityScopeConsumerContextResult.Fail(status, code);
        }
        if (rollout is null)
        {
            return ProductLegalEntityScopeConsumerContextResult.Success(new(
                _tenantContext.TenantId,
                ProductLegalEntityScopeRolloutMode.Preparation,
                null,
                _clock.GetUtcNow(),
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

        var now = _clock.GetUtcNow();
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
            return Decide(context, globalProductId, policy);
        }
        catch (Exception exception) when (IsInvalidEvidence(exception))
        {
            return ProductLegalEntityScopeConsumerDecision.Denied(
                ProductLegalEntityScopeDecisionReason.InvalidScopeEvidence);
        }
    }

    /// <summary>
    /// MOD-0290-FU04 S4 FIX1 (R4) — <see cref="EvaluateAsync"/> for many Global Products with ONE policy read: the same
    /// decision per product (the same evaluator, the same refusals). When the batch read cannot be read, each product is
    /// decided by the single path (S4 FIX2 R2) — never the whole batch denied for one product's evidence.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, ProductLegalEntityScopeConsumerDecision>> EvaluateManyAsync(
        ProductLegalEntityScopeConsumerContext context,
        IReadOnlyCollection<Guid> globalProductIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ids = globalProductIds.Distinct().ToArray();
        var decisions = new Dictionary<Guid, ProductLegalEntityScopeConsumerDecision>();
        if (context.TenantId != _tenantContext.TenantId || ids.Any(id => id == Guid.Empty))
        {
            foreach (var id in ids)
                decisions[id] = ProductLegalEntityScopeConsumerDecision.Denied(
                    ProductLegalEntityScopeDecisionReason.InvalidScopeEvidence);
            return decisions;
        }
        if (context.RolloutMode != ProductLegalEntityScopeRolloutMode.Enforced || ids.Length == 0)
        {
            foreach (var id in ids) decisions[id] = await EvaluateAsync(context, id, cancellationToken);
            return decisions;
        }
        IReadOnlyDictionary<Guid, ProductLegalEntityScopePolicy> policies;
        try
        {
            policies = await _policies.GetByGlobalProductIdsAsync(ids, cancellationToken);
        }
        catch (Exception exception) when (IsInvalidEvidence(exception))
        {
            // S4 FIX2 (R2): one unreadable policy must not deny the whole batch — the single path denies only its own
            // product. S4 FIX3: the batch is read again in groups of FallbackGroupSize; only a group that fails again is
            // decided the single way (the same decision per product) — 200 ids with one bad policy: 1 + 10 group reads +
            // 20 single ones, not 1 + 200.
            foreach (var group in ids.Chunk(FallbackGroupSize))
            {
                IReadOnlyDictionary<Guid, ProductLegalEntityScopePolicy> groupPolicies;
                try
                {
                    groupPolicies = await _policies.GetByGlobalProductIdsAsync(group, cancellationToken);
                }
                catch (Exception groupException) when (IsInvalidEvidence(groupException))
                {
                    foreach (var id in group) decisions[id] = await EvaluateAsync(context, id, cancellationToken);
                    continue;
                }
                foreach (var id in group) decisions[id] = Decide(context, id, groupPolicies.GetValueOrDefault(id));
            }
            return decisions;
        }
        foreach (var id in ids) decisions[id] = Decide(context, id, policies.GetValueOrDefault(id));
        return decisions;
    }

    /// <summary>S4 FIX3: the group a failed batch policy read is retried in (measured: 31 reads vs 201 for 200 ids).</summary>
    public const int FallbackGroupSize = 20;

    private ProductLegalEntityScopeConsumerDecision Decide(
        ProductLegalEntityScopeConsumerContext context, Guid globalProductId, ProductLegalEntityScopePolicy? policy)
    {
        if (context.RolloutState is null)
        {
            return ProductLegalEntityScopeConsumerDecision.Denied(
                ProductLegalEntityScopeDecisionReason.InvalidScopeEvidence);
        }
        try
        {
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
        catch (Exception exception) when (IsInvalidEvidence(exception))
        {
            return ProductLegalEntityScopeConsumerDecision.Denied(
                ProductLegalEntityScopeDecisionReason.InvalidScopeEvidence);
        }
    }

    // The evidence a decision cannot rest on: the same five families the single read always denied on.
    private static bool IsInvalidEvidence(Exception exception) =>
        exception is InvalidOperationException or ArgumentException or FormatException or OverflowException
        || string.Equals(
            exception.GetType().FullName,
            "MongoDB.Bson.Serialization.BsonSerializationException",
            StringComparison.Ordinal);
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
