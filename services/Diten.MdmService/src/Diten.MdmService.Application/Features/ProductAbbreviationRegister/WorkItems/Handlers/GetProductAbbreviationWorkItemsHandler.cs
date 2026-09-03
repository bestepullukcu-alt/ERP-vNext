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

    public GetProductAbbreviationWorkItemsHandler(
        IProductAbbreviationRegisterRepository register,
        IGlobalProductRepository globalProducts,
        IProductAbbreviationActorContext actor,
        IProductLegalEntityScopeRolloutStateRepository rolloutStates,
        IProductLegalEntityScopePolicyRepository policies,
        ProductLegalEntityScopeCandidateFacade candidates,
        ITenantContext tenantContext)
    {
        _register = register;
        _globalProducts = globalProducts;
        _actor = actor;
        _authorization = new ProductAbbreviationAuthorization(actor);
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

        var pending = await _register.GetInitialPendingWorkItemsAsync(
            ProductAbbreviationWorkItemContract.OverflowSentinelLimit,
            cancellationToken);
        if (pending.Count > ProductAbbreviationWorkItemContract.MaximumItems)
        {
            return ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemProjectionResponse>.Fail(
                503,
                "ABBREVIATION_WORK_ITEM_BOUND_EXCEEDED");
        }

        if (pending.Any(x => x.Id == Guid.Empty
                             || x.GlobalProductId == Guid.Empty
                             || x.Version < 0
                             || string.IsNullOrWhiteSpace(x.NormalizedAbbreviation)
                             || string.IsNullOrWhiteSpace(x.RequestedByCanonicalSubjectId)
                             || x.LifecycleStatus != ProductAbbreviationLifecycleStatus.REQUESTED
                             || x.ReplacesEntryId is not null))
        {
            return ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemProjectionResponse>.Fail(
                503,
                "ABBREVIATION_WORK_ITEM_SOURCE_INCONSISTENT");
        }

        var productIds = pending.Select(x => x.GlobalProductId).Distinct().ToArray();
        var products = await _globalProducts.GetByIdsAsync(productIds, cancellationToken);
        var productsById = products.ToDictionary(x => x.Id);
        if (productsById.Count != productIds.Length
            || productIds.Any(id => !productsById.ContainsKey(id)))
        {
            return ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemProjectionResponse>.Fail(
                503,
                "ABBREVIATION_WORK_ITEM_SOURCE_INCONSISTENT");
        }

        var scope = await _scopeGuard.ResolveContextAsync(ProductAbbreviationPermissions.Read, cancellationToken);
        if (!scope.IsSuccessful)
        {
            return ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemProjectionResponse>.Fail(
                scope.StatusCode,
                scope.FailureCode!);
        }

        var visibleProductIds = new HashSet<Guid>();
        foreach (var productId in productIds)
        {
            var decision = await _scopeGuard.EvaluateAsync(scope.Context!, productId, cancellationToken);
            if (decision.Allowed)
            {
                visibleProductIds.Add(productId);
            }
        }

        var items = pending
            .Where(x => visibleProductIds.Contains(x.GlobalProductId))
            .Take(request.Limit)
            .Select(x => Project(x, productsById[x.GlobalProductId]))
            .ToArray();

        return ProductAbbreviationWorkItemOperationResult<ProductAbbreviationWorkItemProjectionResponse>.Success(
            new(ProductAbbreviationWorkItemContract.ContractVersion, items));
    }

    private ProductAbbreviationWorkItemProjection Project(
        ProductAbbreviationRegisterEntry entry,
        GlobalProduct product)
    {
        var requesterIsActor = string.Equals(
            entry.RequestedByCanonicalSubjectId,
            _actor.CanonicalHumanSubjectId,
            StringComparison.Ordinal);
        IReadOnlyList<ProductAbbreviationWorkItemAction> actions = requesterIsActor
            ? [Action("cancel", "WorkAggregation_Action_Cancel", requiresReason: false)]
            :
            [
                Action("approve", "WorkAggregation_Action_Approve", requiresReason: false),
                Action("reject", "WorkAggregation_Action_Reject", requiresReason: true)
            ];

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
                ProductAbbreviationWorkItemContract.ObjectType,
                entry.Id.ToString("D"),
                $"/MDM/ProductAbbreviationRegister?globalProductId={entry.GlobalProductId:D}"),
            LifecycleOwner: ProductAbbreviationWorkItemContract.ProviderCode,
            WorkItemCapabilities: [],
            Actions: actions,
            Concurrency: new("version", entry.Version.ToString(CultureInfo.InvariantCulture)),
            PrimaryActionCode: requesterIsActor ? "cancel" : "approve",
            OverflowActionCodes: requesterIsActor ? null : ["reject"],
            Requester: new(entry.RequestedByCanonicalSubjectId, IsCurrentUser: requesterIsActor));
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
