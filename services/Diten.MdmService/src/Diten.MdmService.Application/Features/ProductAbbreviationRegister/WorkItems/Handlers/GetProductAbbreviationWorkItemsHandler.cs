using System.Globalization;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.Services;
using Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Queries;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Handlers.QueryHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.MdmService.Application.Features.ProductAbbreviationRegister.WorkItems.Handlers;

public sealed class GetProductAbbreviationWorkItemsHandler
    : IRequestHandler<GetProductAbbreviationWorkItemsQuery,
        ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemProjectionResponse>>
{
    private readonly IProductAbbreviationRegisterRepository _register;
    private readonly IGlobalProductRepository _globalProducts;
    private readonly IProductAbbreviationActorContext _actor;
    private readonly ProductAbbreviationAuthorization _authorization;
    private readonly ProductLegalEntityScopeConsumerGuard _scopeGuard;
    private readonly IProductLegalEntityScopeRolloutStateRepository _rolloutStates;
    private readonly IProductLegalEntityScopePolicyRepository _policies;
    private readonly ProductLegalEntityScopeCandidateFacade _candidates;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetProductAbbreviationWorkItemsHandler> _logger;

    /// <summary>
    /// FIX1 S8 / FIX2 A3 — Platform's remote-provider contract has no paging and no "more" flag (one GET, one list;
    /// measured in RemoteWorkItemProviderOptions / HttpWorkItemProvider / the projection envelope). So the pages are
    /// read HERE, in the order work started waiting (oldest first, a stored integer — never the SHA-derived id), until
    /// the reader's visible items fill the answer. Stopping with more work left — at the item limit or the page
    /// budget — is logged, never silent. Each page costs a fixed number of reads, whatever its size.
    /// </summary>
    public const int PageSize = 100;
    public const int MaximumPages = 20;

    public GetProductAbbreviationWorkItemsHandler(
        IProductAbbreviationRegisterRepository register,
        IGlobalProductRepository globalProducts,
        IProductAbbreviationActorContext actor,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext,
        ILogger<GetProductAbbreviationWorkItemsHandler>? logger = null)
    {
        _logger = logger ?? NullLogger<GetProductAbbreviationWorkItemsHandler>.Instance;
        _register = register;
        _globalProducts = globalProducts;
        _actor = actor;
        _authorization = new ProductAbbreviationAuthorization(actor);
        _rolloutStates = rolloutStates;
        _policies = policies;
        _candidates = candidates;
        _tenantContext = tenantContext;
        _scopeGuard = new ProductLegalEntityScopeConsumerGuard(rolloutStates, policies, candidates, tenantContext);
    }

    public async Task<ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemProjectionResponse>> Handle(
        GetProductAbbreviationWorkItemsQuery request,
        CancellationToken cancellationToken)
    {
        var authorization = _authorization.Demand(ProductAbbreviationPermissions.Read);
        if (!authorization.Succeeded)
        {
            return ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemProjectionResponse>.Fail(
                authorization.StatusCode,
                authorization.ErrorCode!);
        }

        var scope = await _scopeGuard.ResolveContextAsync(ProductAbbreviationPermissions.Read, cancellationToken);
        if (!scope.IsSuccessful)
        {
            return ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemProjectionResponse>.Fail(
                scope.StatusCode,
                scope.FailureCode!);
        }

        var items = new List<ProductAbbreviationWorkItemProjection>();
        ProductAbbreviationPendingCursor? after = null;
        var pages = 0;
        var moreLeft = false;
        while (pages < MaximumPages)
        {
            var page = await _register.GetPendingWorkItemsPageAsync(after, PageSize, cancellationToken);
            pages++;
            if (page.Count == 0)
            {
                break;
            }

            after = new ProductAbbreviationPendingCursor(page[^1].PendingSinceUtcTicks, page[^1].Id);
            var consumed = await ProjectPageAsync(page, scope.Context!, request.Limit, items, cancellationToken);
            if (items.Count >= request.Limit)
            {
                moreLeft = consumed < page.Count || page.Count == PageSize;
                break;
            }

            if (page.Count < PageSize)
            {
                break;
            }

            moreLeft = pages == MaximumPages;
        }

        if (moreLeft)
        {
            _logger.LogWarning(
                "ABB work-item projection for tenant {TenantId} answered {Count} items (limit {Limit}, {Pages} pages read); older items are shown first and later pending items are not on this answer.",
                _actor.TenantId,
                items.Count,
                request.Limit,
                pages);
        }

        return ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemProjectionResponse>.Success(
            new(ProductAbbreviationWorkItemContract.ContractVersion, items.ToArray()));
    }

    /// <summary>
    /// FIX1 B3(b) — one bad row is ONE row: it is left out and logged (entry id and reason code only), and every other
    /// row of the tenant is still projected. A correction whose code is no longer active is not bad, it is an
    /// orphan: it is projected with reject / cancel only, so it can be cleaned up from the Task Center.
    /// </summary>
    /// <returns>How many entries of the page were looked at before the answer was full.</returns>
    private async Task<int> ProjectPageAsync(
        IReadOnlyList<ProductAbbreviationRegisterEntry> page,
        ProductLegalEntityScopeConsumerContext scope,
        int limit,
        List<ProductAbbreviationWorkItemProjection> items,
        CancellationToken cancellationToken)
    {
        var consistent = new List<ProductAbbreviationRegisterEntry>();
        foreach (var entry in page)
        {
            if (IsInconsistent(entry))
            {
                Skip(entry, "ABBREVIATION_WORK_ITEM_SOURCE_INCONSISTENT");
                continue;
            }

            consistent.Add(entry);
        }

        // FIX2 A3: the codes the page's corrections replace, in ONE read — never one read per correction.
        var corrections = consistent.Where(x => x.ReplacesEntryId.HasValue
                                                && x.LifecycleStatus == ProductAbbreviationLifecycleStatus.REQUESTED)
            .ToList();
        var formers = corrections.Count == 0
            ? new Dictionary<Guid, ProductAbbreviationRegisterEntry>()
            : (await _register.GetByIdsAsync(
                    corrections.Select(x => x.ReplacesEntryId!.Value).Distinct().ToArray(),
                    cancellationToken))
                .ToDictionary(x => x.Id);
        var orphans = new HashSet<Guid>();
        foreach (var correction in corrections)
        {
            var former = formers.GetValueOrDefault(correction.ReplacesEntryId!.Value);
            if (former is null
                || former.GlobalProductId != correction.GlobalProductId
                || former.LifecycleStatus != ProductAbbreviationLifecycleStatus.ACTIVE)
            {
                orphans.Add(correction.Id);
            }
        }

        var productIds = consistent.Select(x => x.GlobalProductId).Distinct().ToArray();
        var products = productIds.Length == 0
            ? []
            : await _globalProducts.GetByIdsAsync(productIds, cancellationToken);
        var productsById = products.ToDictionary(x => x.Id);
        var known = productIds.Where(productsById.ContainsKey).ToArray();
        // FIX2 A3: in an enforced rollout the scope rule reads each product's policy; the page's policies are read in
        // ONE query and the guard evaluates against them (the guard itself is unchanged: same rule, same answers).
        var guard = scope.RolloutMode == ProductLegalEntityScopeRolloutMode.Enforced && known.Length > 0
            ? new ProductLegalEntityScopeConsumerGuard(
                _rolloutStates,
                await PrefetchPoliciesAsync(known, cancellationToken),
                _candidates,
                _tenantContext)
            : _scopeGuard;
        var visibleProductIds = new HashSet<Guid>();
        foreach (var productId in known)
        {
            var decision = await guard.EvaluateAsync(scope, productId, cancellationToken);
            if (decision.Allowed)
            {
                visibleProductIds.Add(productId);
            }
        }

        var consumed = page.Count - consistent.Count;
        foreach (var entry in consistent)
        {
            if (items.Count >= limit)
            {
                return consumed;
            }

            consumed++;

            if (!productsById.TryGetValue(entry.GlobalProductId, out var product))
            {
                Skip(entry, "ABBREVIATION_WORK_ITEM_PRODUCT_MISSING");
                continue;
            }

            if (visibleProductIds.Contains(entry.GlobalProductId))
            {
                items.Add(Project(entry, product, orphans.Contains(entry.Id)));
            }
        }

        return consumed;
    }

    /// <summary>
    /// FIX3 item 7 — the page's policies in one read. When that read fails because a policy document cannot be read
    /// (one unreadable document fails the whole query), each product is read on its own instead: a product whose policy
    /// cannot be read is hidden (the guard answers "invalid scope evidence" for it), every other product is still shown.
    /// FIX4 item 2: only a DATA failure hides a product. The database or the path to it failing is not "this product's
    /// policy is unreadable" — it goes up, so the Task Center says the source did not answer instead of drawing an
    /// empty board as if there were no work.
    /// </summary>
    private async Task<PrefetchedPolicies> PrefetchPoliciesAsync(
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        try
        {
            return new PrefetchedPolicies(
                _policies,
                await _policies.GetByGlobalProductIdsAsync(productIds, cancellationToken),
                new HashSet<Guid>());
        }
        catch (Exception exception) when (IsUnreadablePolicy(exception))
        {
            _logger.LogWarning(
                "mdm.product_abbreviation.work_item_policy_batch_failed ProductCount={ProductCount} ExceptionType={ExceptionType}",
                productIds.Count,
                exception.GetType().Name);
        }

        var found = new List<ProductLegalEntityScopePolicy>();
        var unreadable = new HashSet<Guid>();
        foreach (var productId in productIds)
        {
            try
            {
                if (await _policies.GetByGlobalProductIdAsync(productId, cancellationToken) is { } policy)
                {
                    found.Add(policy);
                }
            }
            catch (Exception exception) when (IsUnreadablePolicy(exception))
            {
                unreadable.Add(productId);
                _logger.LogWarning(
                    "mdm.product_abbreviation.work_item_policy_unreadable GlobalProductId={GlobalProductId} ExceptionType={ExceptionType}",
                    productId,
                    exception.GetType().Name);
            }
        }

        return new PrefetchedPolicies(_policies, found, unreadable);
    }

    /// <summary>
    /// A policy document that cannot be read as a policy: it does not deserialize (FormatException, or the BSON
    /// serializer's own exception — named, since this layer does not reference the driver) or its content is invalid
    /// (InvalidOperationException). Anything else — the database, the network, a timeout — is not about one product.
    /// </summary>
    private static bool IsUnreadablePolicy(Exception exception)
    {
        if (exception is FormatException or InvalidOperationException)
        {
            return true;
        }

        for (var type = exception.GetType(); type is not null; type = type.BaseType)
        {
            if (type.FullName == "MongoDB.Bson.BsonSerializationException")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The page's policies, read once; every other member goes to the real repository.</summary>
    private sealed class PrefetchedPolicies(
        IProductLegalEntityScopePolicyRepository inner,
        IReadOnlyList<ProductLegalEntityScopePolicy> policies,
        IReadOnlySet<Guid> unreadable) : IProductLegalEntityScopePolicyRepository
    {
        private readonly Dictionary<Guid, ProductLegalEntityScopePolicy> _byProduct =
            policies.GroupBy(x => x.GlobalProductId).ToDictionary(x => x.Key, x => x.First());

        public Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(
            Guid globalProductId,
            CancellationToken cancellationToken = default)
            // An unreadable policy is reported the way the guard isolates a bad one: for that product only.
            => unreadable.Contains(globalProductId)
                ? throw new InvalidOperationException("The product's scope policy could not be read.")
                : Task.FromResult(_byProduct.GetValueOrDefault(globalProductId));

        public Task<ProductLegalEntityScopePolicy?> GetByCreationCommandIdAsync(
            Guid creationCommandId,
            CancellationToken cancellationToken = default)
            => inner.GetByCreationCommandIdAsync(creationCommandId, cancellationToken);

        public Task<ProductLegalEntityScopePolicyWriteResult> CreateAsync(
            ProductLegalEntityScopePolicy policy,
            CancellationToken cancellationToken = default)
            => inner.CreateAsync(policy, cancellationToken);

        public Task<ProductLegalEntityScopePolicyWriteResult> UpdateAsync(
            ProductLegalEntityScopePolicy policy,
            int expectedVersion,
            CancellationToken cancellationToken = default)
            => inner.UpdateAsync(policy, expectedVersion, cancellationToken);

        public Task<IReadOnlyList<Guid>> GetConfiguredGlobalProductIdsAsync(
            IReadOnlyCollection<Guid> globalProductIds,
            CancellationToken cancellationToken = default)
            => inner.GetConfiguredGlobalProductIdsAsync(globalProductIds, cancellationToken);
    }

    private void Skip(ProductAbbreviationRegisterEntry entry, string reasonCode)
        => _logger.LogWarning(
            "ABB work item {EntryId} of tenant {TenantId} left out of the projection: {ReasonCode}.",
            entry.Id,
            _actor.TenantId,
            reasonCode);

    private ProductAbbreviationWorkItemProjection Project(
        ProductAbbreviationRegisterEntry entry,
        GlobalProduct product,
        bool orphan)
    {
        var isRetirement = entry.LifecycleStatus == ProductAbbreviationLifecycleStatus.ACTIVE;
        var isCorrection = entry.ReplacesEntryId.HasValue;
        var requester = isRetirement
            ? entry.RetirementRequestedByCanonicalSubjectId!
            : entry.RequestedByCanonicalSubjectId;
        var requesterIsActor = string.Equals(
            requester,
            _actor.CanonicalHumanSubjectId,
            StringComparison.Ordinal);
        IReadOnlyList<ProductAbbreviationWorkItemAction> actions = requesterIsActor
            ? isRetirement
                ? []
                : [Action("cancel", "WorkAggregation_Action_Cancel", requiresReason: false)]
            :
            [
                Action("approve", "WorkAggregation_Action_Approve", requiresReason: false),
                Action("reject", "WorkAggregation_Action_Reject", requiresReason: true)
            ];
        if (orphan)
        {
            // The code this correction would replace is no longer active: approving it is impossible, closing it is not.
            actions = actions.Where(action => action.Code != "approve").ToArray();
        }

        actions = actions.Where(action => _actor.GrantedPermissions.Contains(action.Code switch
        {
            "approve" => ProductAbbreviationPermissions.Approve,
            "reject" => ProductAbbreviationPermissions.Reject,
            "cancel" => ProductAbbreviationPermissions.Cancel,
            _ => throw new InvalidOperationException("Unexpected ABB action.")
        })).ToArray();
        var objectType = isRetirement
            ? ProductAbbreviationWorkItemContract.RetirementObjectType
            : isCorrection
                ? ProductAbbreviationWorkItemContract.CorrectionObjectType
                : ProductAbbreviationWorkItemContract.AllocationObjectType;

        return new(
            FixtureKind: "workItem",
            Id: entry.Id.ToString("D"),
            WorkIntent: "approval",
            AssignmentMode: "approval",
            OwnershipState: "notApplicable",
            AdmissionState: "notApplicable",
            NormalizedStatus: "Pending",
            TaskLifecycle: "notApplicable",
            ExecutionState: "notApplicable",
            TimerState: "notApplicable",
            SystemState: "fresh",
            ActionDepth: "inline",
            Title: ProductAbbreviationWorkItemLabel.Display(
                $"{entry.NormalizedAbbreviation} · {product.CanonicalCode}"),
            NativeStatus: new(
                "REQUESTED",
                ProductAbbreviationWorkItemLabel.Resource("WorkAggregation_NativeStatus_WaitingApproval")),
            Source: new(
                ProductAbbreviationWorkItemContract.ProviderCode,
                ProductAbbreviationWorkItemContract.ContractVersion,
                objectType,
                entry.Id.ToString("D"),
                $"/MDM/ProductAbbreviationRegister?globalProductId={entry.GlobalProductId:D}"),
            LifecycleOwner: ProductAbbreviationWorkItemContract.ProviderCode,
            WorkItemCapabilities: [],
            Actions: actions,
            Concurrency: new("version", entry.Version.ToString(CultureInfo.InvariantCulture)),
            PrimaryActionCode: actions.FirstOrDefault()?.Code,
            OverflowActionCodes: actions.Count > 1 ? actions.Skip(1).Select(action => action.Code).ToArray() : null,
            Requester: new(requester, IsCurrentUser: requesterIsActor));
    }

    private static bool IsInconsistent(ProductAbbreviationRegisterEntry entry)
    {
        if (entry.Id == Guid.Empty
            || entry.GlobalProductId == Guid.Empty
            || entry.Version < 0
            || string.IsNullOrWhiteSpace(entry.NormalizedAbbreviation))
        {
            return true;
        }

        if (entry.LifecycleStatus == ProductAbbreviationLifecycleStatus.REQUESTED)
        {
            return string.IsNullOrWhiteSpace(entry.RequestedByCanonicalSubjectId)
                   || entry.RetirementRequestId is not null
                   || entry.RetirementRequestedByCanonicalSubjectId is not null
                   || entry.RetirementRequestedAtUtc is not null;
        }

        return entry.LifecycleStatus != ProductAbbreviationLifecycleStatus.ACTIVE
               || string.IsNullOrWhiteSpace(entry.RetirementRequestId)
               || string.IsNullOrWhiteSpace(entry.RetirementRequestedByCanonicalSubjectId)
               || entry.RetirementRequestedAtUtc is null;
    }

    private static ProductAbbreviationWorkItemAction Action(
        string code,
        string labelKey,
        bool requiresReason)
        => new(
            Code: code,
            Label: ProductAbbreviationWorkItemLabel.Resource(labelKey),
            SemanticType: code,
            Enabled: true,
            Source: "provider",
            DisabledReasonCode: null,
            DisabledReason: null,
            RequiresConfirmation: true,
            RequiresReason: requiresReason,
            RequiresEvidence: false,
            SupportsBulk: false,
            RiskLevel: "high");
}
